window.general = {
    attachNameFilter: function () {

        document.querySelectorAll(".name-filter input").forEach(input => {

            if (input.dataset.filterAttached)
                return;

            input.dataset.filterAttached = "true";

            input.addEventListener("keydown", function (e) {

                if (
                    e.ctrlKey ||
                    e.metaKey ||
                    e.altKey ||
                    [
                        "Backspace",
                        "Delete",
                        "Tab",
                        "Escape",
                        "Enter",
                        "ArrowLeft",
                        "ArrowRight",
                        "ArrowUp",
                        "ArrowDown",
                        "Home",
                        "End"
                    ].includes(e.key)
                ) {
                    return;
                }

                if (!/^[A-Za-zÑñ.\- ]$/.test(e.key)) {
                    e.preventDefault();
                }
            });
        });
    }
};