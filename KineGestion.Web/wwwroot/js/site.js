document.addEventListener("DOMContentLoaded", function () {
	var body = document.getElementById("kgBody") || document.body;
	var themeToggle = document.getElementById("themeToggle") || document.getElementById("themeToggleLogin");

	function paintThemeIcon(theme) {
		if (!themeToggle) {
			return;
		}

		var icon = themeToggle.querySelector("i");
		if (!icon) {
			return;
		}

		icon.className = theme === "dark" ? "bi bi-sun-fill" : "bi bi-moon-fill";
	}

	function applyTheme(theme) {
		if (!body) {
			return;
		}

		var selected = theme === "dark" ? "dark" : "professional";
		body.classList.remove("kg-theme-professional", "kg-theme-dark");
		body.classList.add(selected === "dark" ? "kg-theme-dark" : "kg-theme-professional");
		paintThemeIcon(selected);
	}

	try {
		var savedTheme = localStorage.getItem("kg-theme") || "professional";
		applyTheme(savedTheme);

		if (themeToggle) {
			themeToggle.addEventListener("click", function () {
				var isDark = body.classList.contains("kg-theme-dark");
				var nextTheme = isDark ? "professional" : "dark";
				localStorage.setItem("kg-theme", nextTheme);
				applyTheme(nextTheme);
			});
		}
	} catch (e) {
		applyTheme("professional");
	}

	var forms = document.querySelectorAll("form[data-loading-submit='true']");

	forms.forEach(function (form) {
		form.addEventListener("submit", function () {
			var submitButton = form.querySelector("button[type='submit']");
			if (!submitButton || submitButton.disabled) {
				return;
			}

			submitButton.disabled = true;
			submitButton.classList.add("kg-btn-loading");

			var originalText = submitButton.innerHTML;
			submitButton.dataset.originalText = originalText;

			var loadingText = submitButton.dataset.loadingText || "Procesando...";
			submitButton.innerHTML = "<span class='kg-btn-loading-spinner'></span> " + loadingText;
		});
	});

	var prefersReducedMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

	function initCounters() {
		var values = document.querySelectorAll(".kg-stat-value, .kg-stat-hero-value");
		if (!values.length || prefersReducedMotion) {
			return;
		}

		function parseTarget(text) {
			var raw = (text || "").trim();
			var suffix = "";
			if (raw.slice(-1) === "%") {
				suffix = "%";
				raw = raw.slice(0, -1).trim();
			}

			if (/^\d+$/.test(raw)) {
				return { target: parseInt(raw, 10), decimals: 0, separator: "", suffix: suffix };
			}

			var decimalMatch = raw.match(/^(\d+)([.,])(\d+)$/);
			if (decimalMatch) {
				return {
					target: parseFloat(decimalMatch[1] + "." + decimalMatch[3]),
					decimals: decimalMatch[3].length,
					separator: decimalMatch[2],
					suffix: suffix
				};
			}

			return null;
		}

		function formatValue(value, decimals, separator, suffix) {
			var text = value.toFixed(decimals);
			if (separator) {
				text = text.replace(".", separator);
			}
			return text + suffix;
		}

		values.forEach(function (el) {
			var info = parseTarget(el.textContent);
			if (!info || info.target <= 0) {
				return;
			}

			var duration = 900;
			var startedAt = null;

			function step(timestamp) {
				if (startedAt === null) {
					startedAt = timestamp;
				}
				var progress = Math.min((timestamp - startedAt) / duration, 1);
				var eased = 1 - Math.pow(1 - progress, 3);
				el.textContent = formatValue(info.target * eased, info.decimals, info.separator, info.suffix);
				if (progress < 1) {
					requestAnimationFrame(step);
				} else {
					el.textContent = formatValue(info.target, info.decimals, info.separator, info.suffix);
				}
			}

			requestAnimationFrame(step);
		});
	}

	function initToasts() {
		var stack = document.querySelector(".kg-toast-stack");
		if (!stack) {
			return;
		}

		stack.querySelectorAll(".kg-toast").forEach(function (toast) {
			var close = toast.querySelector(".kg-toast-close");

			function dismiss() {
				if (toast.classList.contains("kg-toast-hide")) {
					return;
				}
				toast.classList.add("kg-toast-hide");
				setTimeout(function () {
					if (toast.parentNode) {
						toast.parentNode.removeChild(toast);
					}
				}, 340);
			}

			if (close) {
				close.addEventListener("click", dismiss);
			}

			setTimeout(dismiss, 5400);
		});
	}

	function initNavGroups() {
		var nav = document.querySelector(".kg-sidebar-nav");
		if (!nav) {
			return;
		}

		var labels = Array.prototype.slice.call(nav.querySelectorAll(".kg-nav-section-label"));

		labels.forEach(function (label) {
			var group = document.createElement("div");
			group.className = "kg-nav-group";

			var node = label.nextSibling;
			while (node) {
				if (node.nodeType === 1 && node.classList && node.classList.contains("kg-nav-section-label")) {
					break;
				}
				var next = node.nextSibling;
				group.appendChild(node);
				node = next;
			}

			label.parentNode.insertBefore(group, label.nextSibling);

			var groupKey = "kg-nav-group:" + label.textContent.trim();
			var collapsed = false;
			try {
				collapsed = localStorage.getItem(groupKey) === "collapsed";
			} catch (e) {
				collapsed = false;
			}

			var chevron = document.createElement("i");
			chevron.className = "bi bi-chevron-down kg-nav-chevron";
			label.appendChild(chevron);

			label.classList.add("kg-nav-collapsible");
			label.setAttribute("role", "button");
			label.setAttribute("tabindex", "0");

			function apply(state) {
				group.classList.toggle("collapsed", state);
				label.classList.toggle("kg-collapsed", state);
				label.setAttribute("aria-expanded", state ? "false" : "true");
			}

			apply(collapsed);

			function toggle() {
				var next = !group.classList.contains("collapsed");
				apply(next);
				try {
					localStorage.setItem(groupKey, next ? "collapsed" : "open");
				} catch (e) {
					void e;
				}
			}

			label.addEventListener("click", toggle);
			label.addEventListener("keydown", function (event) {
				if (event.key === "Enter" || event.key === " ") {
					event.preventDefault();
					toggle();
				}
			});
		});
	}

	function initPageTransitions() {
		if (prefersReducedMotion) {
			return;
		}

		document.addEventListener("click", function (event) {
			if (event.defaultPrevented || event.button !== 0) {
				return;
			}
			if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
				return;
			}

			var link = event.target.closest ? event.target.closest("a") : null;
			if (!link) {
				return;
			}
			if (link.target && link.target !== "_self") {
				return;
			}
			if (link.hasAttribute("download") || link.getAttribute("data-bs-toggle")) {
				return;
			}

			var href = link.getAttribute("href");
			if (!href || href.charAt(0) === "#" || href.indexOf("javascript:") === 0) {
				return;
			}

			var url;
			try {
				url = new URL(link.href, window.location.href);
			} catch (e) {
				return;
			}
			if (url.origin !== window.location.origin) {
				return;
			}

			event.preventDefault();
			document.body.classList.add("kg-page-leaving");
			setTimeout(function () {
				window.location.href = link.href;
			}, 200);
		});

		window.addEventListener("pageshow", function () {
			document.body.classList.remove("kg-page-leaving");
		});
	}

	initCounters();
	initToasts();
	initNavGroups();
	initPageTransitions();
});
