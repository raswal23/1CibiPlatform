window.atsAssistantScrollToBottom = (element) => {
    if (!element) {
        return;
    }

    element.scrollTop = element.scrollHeight;
};
