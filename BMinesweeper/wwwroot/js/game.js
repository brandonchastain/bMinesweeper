// Bridges the browser's animation frame loop and window resizes into Blazor.
// Game logic belongs in C# — keep this file boring.
window.bMinesweeper = (() => {
    let dotNet = null;
    let canvas = null;

    // What the canvas was last sized for, so repeated notifications about one change do
    // one piece of work.
    let lastWidth = 0;
    let lastHeight = 0;
    let lastDpr = 0;

    function resize() {
        // Back the canvas at device resolution but keep the drawing coordinate system in
        // CSS pixels, so the game never has to think about DPI.
        //
        // Measured off the board element rather than off the window: the board is inset by
        // the phone's safe areas in CSS, and on a mobile browser window.innerHeight
        // includes whatever the URL bar is currently covering.
        const dpr = window.devicePixelRatio || 1;
        const width = Math.max(1, canvas.parentElement.clientWidth);
        const height = Math.max(1, canvas.parentElement.clientHeight);

        if (width === lastWidth && height === lastHeight && dpr === lastDpr) {
            return;
        }

        lastWidth = width;
        lastHeight = height;
        lastDpr = dpr;

        canvas.width = Math.round(width * dpr);
        canvas.height = Math.round(height * dpr);
        canvas.style.width = width + 'px';
        canvas.style.height = height + 'px';
        canvas.getContext('2d').setTransform(dpr, 0, 0, dpr, 0, 0);

        dotNet.invokeMethodAsync('OnResize', width, height, dpr);
    }

    function frame(timestamp) {
        // Chained rather than fire-and-forget: a slow frame delays the next one instead of
        // queueing up interop calls behind it.
        dotNet.invokeMethodAsync('OnFrame', timestamp)
            .then(() => requestAnimationFrame(frame));
    }

    return {
        start: (dotNetRef) => {
            dotNet = dotNetRef;
            const board = document.getElementById('board');
            canvas = board.querySelector('canvas');

            resize();

            // The board's own size is what matters, and on a phone it changes for reasons
            // no window event reports: the URL bar sliding away, the keyboard coming up, a
            // rotation, a safe area changing shape. Watching the element covers all of
            // them; the window events below cost nothing and catch the rest.
            new ResizeObserver(resize).observe(board);
            window.addEventListener('resize', resize);
            window.addEventListener('orientationchange', resize);
            window.visualViewport?.addEventListener('resize', resize);

            board.focus();
            requestAnimationFrame(frame);
        }
    };
})();
