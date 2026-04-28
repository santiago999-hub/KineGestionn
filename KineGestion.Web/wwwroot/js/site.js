document.addEventListener("DOMContentLoaded", function () {
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
