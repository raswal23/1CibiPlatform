window.downloadFile = (fileName, contentType, bytes) => {
    const blob = new Blob([bytes], { type: contentType });

    const url = URL.createObjectURL(blob);

    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    a.click();

    URL.revokeObjectURL(url);
};