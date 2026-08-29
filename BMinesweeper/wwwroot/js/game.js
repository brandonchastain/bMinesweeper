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

            if (!press.flagged) {
                silenceHaptic();
            }

            press = null;
        }
    }

    // The two things a press can do, told apart by feel where the platform allows it:
    // planting a flag is one firm buzz, and pulling one back up is a lighter double tick,
    // so a player who mis-pressed a cell they had already flagged knows it without
    // looking. A press that changed nothing stays silent — a buzz that means 'nothing
    // happened' is noise.
    const HAPTICS = {
        Placed: 35,
        Removed: [12, 45, 12],
    };

    // iOS has no Vibration API — WebKit has never shipped it, in Safari or in any other
    // iOS browser, since they are all WebKit underneath. What it does have, since 17.4, is
    // the switch checkbox, which plays the system's own tick when a finger toggles it. So
    // on a phone that cannot vibrate, an invisible switch is laid over the board and the
    // press that flags a cell is also, unknowingly, a press on that switch.
    //
    // It has to be the finger: iOS 26.5 closed the last path that let script fire the tick
    // by toggling a switch itself, so the control must be under the touch before the touch
    // starts. Which is why this is an overlay and not a hidden element clicked from the
    // long-press timer — that shape worked until 26.5 and is silent now.
    //
    // Two consequences, both visible in the feel of the game. The tick lands when the
    // finger lifts rather than when the flag appears, because that is when a switch
    // commits its toggle. And it is one fixed tick, so placing and removing feel alike;
    // the double tick above survives only on the platforms with a real vibrator.
    let hapticSwitch = null;

    // Long enough to outlast the activation being swallowed, short enough that the next
    // press cannot beat it back.
    const RE_ARM_MS = 100;

    function makeHapticSwitch(board) {
        if (navigator.vibrate || !('switch' in document.createElement('input'))) {
            return null;
        }

        const input = document.createElement('input');
        input.type = 'checkbox';
        input.setAttribute('switch', '');
        input.className = 'haptic-switch';
        input.tabIndex = -1;
        input.setAttribute('aria-hidden', 'true');

        // Inside the board, so it covers exactly what a finger can press.
        board.appendChild(input);

        return input;
    }

    // The switch is live by default, because it has to be live before a finger lands to
    // tick at all. Silencing is therefore the active step: a press that turns out not to
    // have flagged anything disables the control before its lift can commit the toggle,
    // and re-arms once that lift is spent. Nothing reads the switch's value; only whether
    // it was allowed to move.
    function silenceHaptic() {
        if (!hapticSwitch || hapticSwitch.disabled) {
            return;
        }

        hapticSwitch.disabled = true;
        setTimeout(() => { hapticSwitch.disabled = false; }, RE_ARM_MS);
    }

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

                if (pattern === undefined) {
                    // Long-pressed a cell that was already open, or otherwise took no
                    // flag. The answer usually beats the finger off the glass, which is
                    // what lets the tick be called off in time.
                    silenceHaptic();
                    return;
                }

                buzz(pattern);
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

        // Before anything else: a release that is not the end of a flag must not be
        // allowed to toggle the switch under the board, and this is the last moment it can
        // be stopped.
        if (!flagged) {
            silenceHaptic();
        }

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
            hapticSwitch = makeHapticSwitch(board);

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
