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

		icon.className = theme === "hospital" ? "bi bi-hospital" : "bi bi-circle-half";
	}

	function applyTheme(theme) {
		if (!body) {
			return;
		}

		var selected = theme === "hospital" ? "hospital" : "professional";
		body.classList.remove("kg-theme-professional", "kg-theme-hospital");
		body.classList.add(selected === "hospital" ? "kg-theme-hospital" : "kg-theme-professional");
		paintThemeIcon(selected);
	}

	try {
		var savedTheme = localStorage.getItem("kg-theme") || "professional";
		applyTheme(savedTheme);

		if (themeToggle) {
			themeToggle.addEventListener("click", function () {
				var isHospital = body.classList.contains("kg-theme-hospital");
				var nextTheme = isHospital ? "professional" : "hospital";
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
});
