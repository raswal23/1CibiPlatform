// Reports which section of a long document is currently in view, so a contents rail can
// highlight it. Used by the public API documentation page.
window.scrollSpy = {
    _observer: null,
    _visible: new Set(),
    _dotNetRef: null,

    // Observes every element carrying one of `ids` and calls back with the id of the
    // topmost visible one whenever that changes.
    start: function (ids, dotNetRef, callbackName) {
        this.stop();

        this._dotNetRef = dotNetRef;
        this._visible = new Set();

        const targets = ids
            .map(id => document.getElementById(id))
            .filter(element => element !== null);

        if (targets.length === 0) {
            return;
        }

        let current = null;

        const report = () => {
            // Document order, not intersection order: with several sections on screen
            // the topmost is the one the reader is actually looking at.
            const topmost = targets.find(element => this._visible.has(element.id));
            const next = topmost ? topmost.id : null;

            if (next !== null && next !== current) {
                current = next;
                dotNetRef.invokeMethodAsync(callbackName, next);
            }
        };

        this._observer = new IntersectionObserver(
            entries => {
                for (const entry of entries) {
                    if (entry.isIntersecting) {
                        this._visible.add(entry.target.id);
                    } else {
                        this._visible.delete(entry.target.id);
                    }
                }

                report();
            },
            {
                // A band across the upper part of the viewport: a section counts as
                // "current" once its heading reaches the top third, and stops counting
                // once it has scrolled well past.
                rootMargin: '-10% 0px -70% 0px',
                threshold: 0
            });

        for (const target of targets) {
            this._observer.observe(target);
        }
    },

    stop: function () {
        if (this._observer !== null) {
            this._observer.disconnect();
            this._observer = null;
        }

        this._visible = new Set();
        this._dotNetRef = null;
    }
};
