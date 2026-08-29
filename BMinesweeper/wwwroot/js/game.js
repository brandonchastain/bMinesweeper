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


    // How long a press has to be held to count as a flag, and how far the finger may
    // stray first. The slop is generous because a finger held still on glass still moves
    // a few pixels, and stingy enough that a flick reads as a scroll attempt, not a flag.
    const LONG_PRESS_MS = 450;
    const MOVE_SLOP_PX = 12;

    let press = null;

    function boardPoint(event) {
        // The game reads coordinates as board pixels, and the canvas starts exactly where
        // the board element does, so the element's own rect is the origin.
        const rect = canvas.getBoundingClientRect();
        return { x: event.clientX - rect.left, y: event.clientY - rect.top };
    }

    function cancelPress() {
        if (press) {
            clearTimeout(press.timer);
            press = null;
        }
    }

    // The two things a press can do, told apart by feel: planting a flag is one firm
    // buzz, and pulling one back up is a lighter double tick, so a player who
    // mis-pressed a cell they had already flagged knows it without looking. A press that
    // changed nothing stays silent — a buzz that means 'nothing happened' is noise.
    //
    // Only where there is a vibrator to play it. iOS has no Vibration API — WebKit has
    // never shipped it, in Safari or in any other iOS browser, since they are all WebKit
    // underneath — and the switch-checkbox trick that stood in for one was closed off in
    // iOS 26.5, which stopped script from toggling a switch to borrow the system tick.
    // What survives there needs a finger on the control itself, before the touch begins,
    // which a flag decided mid-press cannot arrange. So on iPhone the drawn flag is the
    // feedback, which is what it always really was.
    const HAPTICS = {
        Placed: 35,
        Removed: [12, 45, 12],
    };

    function buzz(pattern) {
        if (navigator.vibrate) {
            navigator.vibrate(pattern);
        }
    }

    function flag(point) {
        // Answered rather than anticipated: only the game knows whether a flag actually
        // moved, and a buzz for a press that landed on an open cell would be a lie.
        return dotNet.invokeMethodAsync('OnLongPress', point.x, point.y)
            .then((result) => {
                const pattern = HAPTICS[result];

                if (pattern !== undefined) {
                    buzz(pattern);
                }
            });
    }

    function onPointerDown(event) {
        cancelPress();

        // Only the primary button starts a press. A right click is handled on the
        // contextmenu event instead, which is the one event every browser agrees to send
        // for it.
        if (event.button !== 0) {
            return;
        }

        const point = boardPoint(event);

        press = {
            id: event.pointerId,
            point,
            startX: event.clientX,
            startY: event.clientY,
            flagged: false,
            timer: setTimeout(() => {
                // Fires while the finger is still down: the flag appears under the finger
                // rather than when it lifts, which is what makes the press feel answered.
                if (press) {
                    press.flagged = true;
                    flag(press.point);
                }
            }, LONG_PRESS_MS),
        };
    }

    function onPointerMove(event) {
        if (!press || event.pointerId !== press.id) {
            return;
        }

        // Dragged too far to be a press on one cell. Reveal is dropped too — the player
        // moved off what they aimed at.
        if (Math.hypot(event.clientX - press.startX, event.clientY - press.startY) > MOVE_SLOP_PX) {
            cancelPress();
        }
    }

    function onPointerUp(event) {
        if (!press || event.pointerId !== press.id) {
            return;
        }

        const { flagged, point } = press;
        cancelPress();

        // A press that already flagged must not also reveal on release.
        if (!flagged) {
            dotNet.invokeMethodAsync('OnTap', point.x, point.y);
        }
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

            board.addEventListener('pointerdown', onPointerDown);
            board.addEventListener('pointermove', onPointerMove);
            board.addEventListener('pointerup', onPointerUp);
            board.addEventListener('pointercancel', cancelPress);
            board.addEventListener('pointerleave', cancelPress);

            // On a mouse this is the right click, and flagging from it is the whole
            // desktop story. On a phone it is Android's own long-press menu arriving on
            // top of the flag the timer already placed, so it is suppressed either way and
            // a press that already flagged does not flag twice.
            board.addEventListener('contextmenu', (e) => {
                e.preventDefault();

                if (!press?.flagged) {
                    flag(boardPoint(e));
                }

                cancelPress();
            });

            board.focus();
            requestAnimationFrame(frame);
        }
    };
})();
