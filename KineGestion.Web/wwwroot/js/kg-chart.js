(function (global) {
    'use strict';

    var instances = {};
    var factories = {};
    var refreshScheduled = false;

    function themeHost() {
        return document.getElementById('kgBody') || document.body;
    }

    function token(name, fallback) {
        if (!global.getComputedStyle) return fallback;
        var value = global.getComputedStyle(themeHost()).getPropertyValue(name);
        value = (value || '').trim();
        return value.length > 0 ? value : fallback;
    }

    function palette() {
        return {
            accent: token('--kg-accent', '#1f7bbf'),
            accentDark: token('--kg-accent-dark', '#185f95'),
            success: token('--kg-success', '#20805a'),
            warning: token('--kg-warning', '#9f6b0a'),
            danger: token('--kg-danger', '#b43a3a'),
            info: token('--kg-info', '#5f6db3'),
            violet: token('--kg-tone-violet-fg', '#7c3aed'),
            cyan: token('--kg-tone-cyan-fg', '#0284c7'),
            green: token('--kg-tone-green-fg', '#16a34a'),
            amber: token('--kg-tone-amber-fg', '#d97706'),
            orange: token('--kg-tone-orange-fg', '#ea580c'),
            red: token('--kg-tone-red-fg', '#dc2626'),
            yellow: token('--kg-tone-yellow-fg', '#ca8a04'),
            text: token('--kg-text', '#1d2b3d'),
            muted: token('--kg-text-muted', '#5f7188'),
            grid: token('--kg-border', '#d7e0ea'),
            surface: token('--kg-card-bg', '#ffffff')
        };
    }

    function isDark() {
        var host = themeHost();
        return !!(host && host.classList && host.classList.contains('kg-theme-dark'));
    }

    function withAlpha(hex, alpha) {
        var value = (hex || '').trim();
        if (value.charAt(0) !== '#') return value;
        if (value.length === 4) {
            value = '#' + value.charAt(1) + value.charAt(1) + value.charAt(2) + value.charAt(2) + value.charAt(3) + value.charAt(3);
        }
        if (value.length !== 7) return value;
        var r = parseInt(value.substr(1, 2), 16);
        var g = parseInt(value.substr(3, 2), 16);
        var b = parseInt(value.substr(5, 2), 16);
        if (isNaN(r) || isNaN(g) || isNaN(b)) return value;
        return 'rgba(' + r + ',' + g + ',' + b + ',' + alpha + ')';
    }

    function axisDefaults(p) {
        return {
            grid: { color: withAlpha(p.grid, isDark() ? 0.55 : 0.9), drawBorder: false },
            border: { display: false },
            ticks: { color: p.muted, precision: 0 }
        };
    }

    function legendDefaults(p) {
        return {
            position: 'bottom',
            labels: { color: p.muted, usePointStyle: true, pointStyle: 'circle', boxWidth: 8, padding: 14 }
        };
    }

    function tooltipDefaults(p) {
        return {
            backgroundColor: p.surface,
            titleColor: p.text,
            bodyColor: p.muted,
            borderColor: p.grid,
            borderWidth: 1,
            padding: 10,
            displayColors: true,
            boxPadding: 4
        };
    }

    function applyDefaults(config, p) {
        config = config || {};
        config.options = config.options || {};
        config.options.responsive = config.options.responsive !== false;
        config.options.maintainAspectRatio = config.options.maintainAspectRatio !== false;
        config.options.animation = config.options.animation || { duration: 320 };
        config.options.plugins = config.options.plugins || {};
        if (!config.options.plugins.legend) config.options.plugins.legend = legendDefaults(p);
        if (!config.options.plugins.tooltip) config.options.plugins.tooltip = tooltipDefaults(p);

        var radialType = config.type === 'doughnut' || config.type === 'pie' || config.type === 'polarArea';
        if (!radialType) {
            var scales = config.options.scales || {};
            var x = scales.x || {};
            var y = scales.y || {};
            if (!x.grid) x.grid = axisDefaults(p).grid;
            if (!x.border) x.border = { display: false };
            if (!x.ticks) x.ticks = { color: p.muted };
            if (!y.grid) y.grid = axisDefaults(p).grid;
            if (!y.border) y.border = { display: false };
            if (!y.ticks) y.ticks = { color: p.muted, precision: 0 };
            if (!y.beginAtZero) y.beginAtZero = true;
            scales.x = x;
            scales.y = y;
            config.options.scales = scales;
        }

        return config;
    }

    function render(canvasId, factory) {
        if (!global.Chart) return null;

        var canvas = document.getElementById(canvasId);
        if (!canvas) return null;

        factories[canvasId] = factory;

        if (instances[canvasId]) {
            instances[canvasId].destroy();
            instances[canvasId] = null;
        }

        var p = palette();
        var config = typeof factory === 'function' ? factory(p) : factory;
        config = applyDefaults(config, p);

        instances[canvasId] = new global.Chart(canvas.getContext('2d'), config);
        return instances[canvasId];
    }

    function refreshAll() {
        Object.keys(factories).forEach(function (canvasId) {
            render(canvasId, factories[canvasId]);
        });
    }

    function scheduleRefresh() {
        if (refreshScheduled) return;
        refreshScheduled = true;
        var run = global.requestAnimationFrame || function (cb) { return global.setTimeout(cb, 0); };
        run(function () {
            refreshScheduled = false;
            refreshAll();
        });
    }

    function watchTheme() {
        if (!global.MutationObserver || !document.body) return;
        var observer = new global.MutationObserver(scheduleRefresh);
        observer.observe(document.body, { attributes: true, attributeFilter: ['class'] });
    }

    global.kgChart = {
        palette: palette,
        isDark: isDark,
        withAlpha: withAlpha,
        render: render,
        refreshAll: refreshAll,
        axisDefaults: axisDefaults,
        legendDefaults: legendDefaults,
        tooltipDefaults: tooltipDefaults
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', watchTheme);
    } else {
        watchTheme();
    }
})(window);
