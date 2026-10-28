
document.addEventListener("DOMContentLoaded", () => {
    const input = document.querySelector("#posterFile");
    const dropZone = document.querySelector("#posterDropZone");
    const preview = document.querySelector("#posterPreview");
    const info = document.querySelector("#posterInfo");
    const error = document.querySelector("#posterError");
    const removeButton = document.querySelector("#removePoster");

    if (!input || !dropZone || !preview || !info || !error || !removeButton) {
        return;
    }

    const maxSize = 5 * 1024 * 1024;
    const allowedTypes = ["image/jpeg", "image/png", "image/webp"];
    let objectUrl = null;

    function clearPreview() {
        if (objectUrl) {
            URL.revokeObjectURL(objectUrl);
            objectUrl = null;
        }

        preview.removeAttribute("src");
        preview.hidden = true;
        info.textContent = "";
        error.textContent = "";
        removeButton.hidden = true;
        input.value = "";
    }

    function showFile(file) {
        clearPreview();

        if (!file) return;

        const extension = file.name.split(".").pop()?.toLowerCase();

        if (!allowedTypes.includes(file.type) ||
            !["jpg", "jpeg", "png", "webp"].includes(extension)) {
            error.textContent = "Only JPG, PNG and WebP images are allowed.";
            return;
        }

        if (file.size > maxSize) {
            error.textContent = "Maximum file size is 5 MB.";
            return;
        }

        const transfer = new DataTransfer();
        transfer.items.add(file);
        input.files = transfer.files;

        objectUrl = URL.createObjectURL(file);
        preview.src = objectUrl;

        preview.onload = () => {
            if (!objectUrl) return;

            const sizeMB = (file.size / (1024 * 1024)).toFixed(2);

            info.textContent =
                `${file.name} | ${sizeMB} MB | ` +
                `${preview.naturalWidth} × ${preview.naturalHeight}px`;

            preview.hidden = false;
            removeButton.hidden = false;
        };

        preview.onerror = () => {
            clearPreview();
            error.textContent = "Cannot read this image.";
        };
    }

    input.addEventListener("change", () => {
        showFile(input.files[0]);
    });

    dropZone.addEventListener("dragover", (event) => {
        event.preventDefault();
        dropZone.classList.add("border-primary");
    });

    dropZone.addEventListener("dragleave", () => {
        dropZone.classList.remove("border-primary");
    });

    dropZone.addEventListener("drop", (event) => {
        event.preventDefault();
        dropZone.classList.remove("border-primary");
        showFile(event.dataTransfer.files[0]);
    });

    removeButton.addEventListener("click", clearPreview);

    window.addEventListener("pagehide", () => {
        if (objectUrl) URL.revokeObjectURL(objectUrl);
    });
});
