const pads = new WeakMap();

export function setup(canvas, componentReference, image, disabled) {
    if (!canvas) {
        return;
    }

    destroy(canvas);

    const context = canvas.getContext("2d");
    const state = {
        componentReference,
        context,
        disabled,
        drawing: false,
        resizeObserver: null,
        handlers: {}
    };

    let firstResize = true;
    const resize = () => {
        const currentImage = !firstResize && canvas.width > 0 && canvas.height > 0
            ? canvas.toDataURL("image/png")
            : image;
		firstResize = false;
        const bounds = canvas.getBoundingClientRect();
        const scale = window.devicePixelRatio || 1;

        canvas.width = Math.max(1, Math.floor(bounds.width * scale));
        canvas.height = Math.max(1, Math.floor(bounds.height * scale));
        context.setTransform(scale, 0, 0, scale, 0, 0);
        context.lineWidth = 2;
        context.lineCap = "round";
        context.lineJoin = "round";
        context.strokeStyle = "#102247";

        if (currentImage) {
            drawImage(canvas, context, currentImage, scale);
        }
    };

    const point = event => {
        const bounds = canvas.getBoundingClientRect();
        return { x: event.clientX - bounds.left, y: event.clientY - bounds.top };
    };

    state.handlers.pointerdown = event => {
        if (state.disabled) return;
        state.drawing = true;
        canvas.setPointerCapture(event.pointerId);
        const position = point(event);
        context.beginPath();
        context.moveTo(position.x, position.y);
        event.preventDefault();
    };

    state.handlers.pointermove = event => {
        if (!state.drawing || state.disabled) return;
        const position = point(event);
        context.lineTo(position.x, position.y);
        context.stroke();
        event.preventDefault();
    };

    state.handlers.pointerup = event => {
        if (!state.drawing) return;
        state.drawing = false;
        if (canvas.hasPointerCapture(event.pointerId)) {
            canvas.releasePointerCapture(event.pointerId);
        }
        componentReference.invokeMethodAsync(
            "SignatureChangedAsync",
            canvas.toDataURL("image/png"));
        event.preventDefault();
    };

    canvas.addEventListener("pointerdown", state.handlers.pointerdown);
    canvas.addEventListener("pointermove", state.handlers.pointermove);
    canvas.addEventListener("pointerup", state.handlers.pointerup);
    canvas.addEventListener("pointercancel", state.handlers.pointerup);
    canvas.style.touchAction = "none";

    state.resizeObserver = new ResizeObserver(resize);
    state.resizeObserver.observe(canvas);
    pads.set(canvas, state);
    resize();
}

export function setDisabled(canvas, disabled) {
    const state = canvas ? pads.get(canvas) : null;
    if (state) {
        state.disabled = disabled;
    }
}

export function clear(canvas) {
    const state = canvas ? pads.get(canvas) : null;
    if (!state) return;
    state.context.clearRect(0, 0, canvas.width, canvas.height);
}

export function setImage(canvas, source) {
    const state = canvas ? pads.get(canvas) : null;
    if (!state || !source) return;
    const scale = window.devicePixelRatio || 1;
    state.context.clearRect(0, 0, canvas.width, canvas.height);
    drawImage(canvas, state.context, source, scale);
}

export function destroy(canvas) {
    const state = canvas ? pads.get(canvas) : null;
    if (!state) return;

    canvas.removeEventListener("pointerdown", state.handlers.pointerdown);
    canvas.removeEventListener("pointermove", state.handlers.pointermove);
    canvas.removeEventListener("pointerup", state.handlers.pointerup);
    canvas.removeEventListener("pointercancel", state.handlers.pointerup);
    state.resizeObserver?.disconnect();
    pads.delete(canvas);
}

function drawImage(canvas, context, source, scale) {
    const image = new Image();
    image.onload = () => {
        context.drawImage(
            image,
            0,
            0,
            canvas.width / scale,
            canvas.height / scale);
    };
    image.src = source;
}
