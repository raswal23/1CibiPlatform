/*
 * Chart -> PDF export for the ATS dashboard.
 *
 * html2canvas and jsPDF are ~560 KB combined, so they are NOT referenced from
 * index.html. They are injected on the first export instead, which keeps the
 * initial page payload unchanged for the majority of users who never export.
 *
 * Rasterising is deliberate: the four dashboard charts are a mix of hand-written
 * inline SVG and MudBlazor-rendered SVG, plus HTML overlays (the donut centre
 * KPI and the stat rails) that sit on top of the SVG. Only a screenshot of the
 * composed result reproduces what the user is actually looking at.
 */
window.atsChartExport = (() => {
    const SCRIPTS = [
        "js/generic/html2canvas.min.js",
        "js/generic/jspdf.umd.min.js"
    ];

    // A4 landscape in jsPDF "pt" units, and the margin used on all four sides.
    const PAGE_WIDTH = 841.89;
    const PAGE_HEIGHT = 595.28;
    const MARGIN = 36;

    let librariesPromise = null;

    // The publish pipeline renames JS assets to content-fingerprinted file names
    // and records the mapping in index.html's import map. Import maps only apply
    // to module imports, not injected <script src>, so resolve manually here.
    const resolveAssetSrc = src => {
        const importMap = document.querySelector('script[type="importmap"]');
        if (!importMap) {
            return src;
        }

        try {
            const imports = JSON.parse(importMap.textContent).imports || {};
            return imports[`./${src}`] || src;
        } catch {
            return src;
        }
    };

    const loadScript = src => new Promise((resolve, reject) => {
        const resolvedSrc = resolveAssetSrc(src);
        const existing = document.querySelector(`script[src="${resolvedSrc}"]`);
        if (existing) {
            resolve();
            return;
        }

        const script = document.createElement("script");
        script.src = resolvedSrc;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error(`Failed to load ${resolvedSrc}`));
        document.head.appendChild(script);
    });

    // Loaded once per session. The promise is cached so two quick clicks on
    // different cards do not inject the scripts twice.
    const ensureLibraries = () => {
        if (!librariesPromise) {
            librariesPromise = Promise.all(SCRIPTS.map(loadScript)).catch(error => {
                // Let a later attempt retry rather than caching the failure.
                librariesPromise = null;
                throw error;
            });
        }

        return librariesPromise;
    };

    const resolveJsPdf = () => {
        const namespace = window.jspdf || window.jsPDF;
        return namespace && namespace.jsPDF ? namespace.jsPDF : namespace;
    };

    /*
     * Runs against html2canvas's off-screen clone, before it is rasterised.
     *
     * Animations are neutralised because an element captured mid-animation can be
     * painted at its "from" keyframe. With the animation removed each element
     * falls back to its own base style, so nothing has to be forced visible.
     *
     * Nothing is done about SVG paint here on purpose. html2canvas serialises each
     * <svg> to a standalone data URI - a separate document that page stylesheets
     * do not reach - but it already copies the resolved computed style onto every
     * cloned node first, so CSS-painted SVG (the YTD grid and axis lines, the
     * transparent hover targets, MudBlazor's donut segments) survives the trip.
     * Verified by capturing with and without a manual inlining pass: identical,
     * pixel for pixel.
     */
    const prepareClone = clonedDocument => {
        const style = clonedDocument.createElement("style");
        style.textContent = `
            *, *::before, *::after {
                animation: none !important;
                transition: none !important;
            }
        `;
        clonedDocument.head.appendChild(style);
    };

    const buildPdf = (canvas, title, subtitle, footer) => {
        const JsPdf = resolveJsPdf();
        const pdf = new JsPdf({ orientation: "landscape", unit: "pt", format: "a4" });

        pdf.setFont("helvetica", "bold");
        pdf.setFontSize(16);
        pdf.setTextColor(11, 31, 58);
        pdf.text(title, MARGIN, MARGIN + 6);

        let headerBottom = MARGIN + 6;

        if (subtitle) {
            pdf.setFont("helvetica", "normal");
            pdf.setFontSize(10);
            pdf.setTextColor(107, 123, 146);
            pdf.text(subtitle, MARGIN, MARGIN + 24);
            headerBottom = MARGIN + 24;
        }

        pdf.setDrawColor(227, 233, 241);
        pdf.line(MARGIN, headerBottom + 12, PAGE_WIDTH - MARGIN, headerBottom + 12);

        // Reserve a footer band, then fit the capture into what is left. Scaling
        // is uniform and never enlarges, so a small chart is not blurred up.
        const footerHeight = footer ? 26 : 0;
        const availableWidth = PAGE_WIDTH - MARGIN * 2;
        const availableHeight = PAGE_HEIGHT - (headerBottom + 28) - MARGIN - footerHeight;
        const scale = Math.min(
            availableWidth / canvas.width,
            availableHeight / canvas.height,
            1);
        const imageWidth = canvas.width * scale;
        const imageHeight = canvas.height * scale;

        /*
         * PNG keeps chart text and hairlines crisp; JPEG artefacts around 10px
         * axis labels are very visible. But jsPDF embeds PNG uncompressed unless
         * told otherwise, which turned a 115 KB image into a 5.7 MB PDF. FAST
         * (Flate) brings that to ~64 KB and is lossless, so the fidelity argument
         * for PNG still holds.
         */
        pdf.addImage(
            canvas.toDataURL("image/png"),
            "PNG",
            MARGIN + (availableWidth - imageWidth) / 2,
            headerBottom + 28,
            imageWidth,
            imageHeight,
            undefined,
            "FAST");

        if (footer) {
            pdf.setFont("helvetica", "normal");
            pdf.setFontSize(8);
            pdf.setTextColor(107, 123, 146);
            pdf.text(footer, MARGIN, PAGE_HEIGHT - MARGIN);
        }

        return pdf;
    };

    /**
     * Captures the element carrying data-chart-export="<exportId>" and saves it
     * as a single-page A4 landscape PDF.
     *
     * Returns true on success. Returns false when the element is not in the DOM
     * (the caller turns that into a user-facing message); throws on a genuine
     * failure so the caller can distinguish the two.
     */
    const downloadChartAsPdf = async (exportId, fileName, title, subtitle, footer) => {
        const element = document.querySelector(`[data-chart-export="${exportId}"]`);
        if (!element) {
            return false;
        }

        await ensureLibraries();

        const canvas = await window.html2canvas(element, {
            // 2x keeps text legible in the PDF without an unreasonable payload.
            scale: 2,
            backgroundColor: "#ffffff",
            logging: false,
            useCORS: true,
            onclone: prepareClone
        });

        buildPdf(canvas, title, subtitle, footer).save(fileName);
        return true;
    };

    return { downloadChartAsPdf };
})();
