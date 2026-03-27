window.navigationLock = {
    enable: function (message) {
        window.onbeforeunload = function () {
            return message;
        };
    },
    disable: function () {
        window.onbeforeunload = null;
    }
};