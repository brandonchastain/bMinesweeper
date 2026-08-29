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
    // Two ways to say it, because the platforms offer different vocabularies: a pattern
    // for the Vibration API, and a count of system ticks for the iOS fallback below,
    // which can only make one fixed tap and so has to spell the difference out in
    // repeats instead of in length.
    const HAPTICS = {
        Placed: { pattern: 35, ticks: 1 },
        Removed: { pattern: [12, 45, 12], ticks: 2 },
    };

    // Far enough apart to be felt as two taps rather than one smeared one.
    const TICK_GAP_MS = 90;

    // iOS has no Vibration API — WebKit has never shipped it, in Safari or in any other
    // iOS browser, since they are all WebKit underneath. What it does have, since 17.4,
    // is the switch checkbox, which fires the system's own haptic when it toggles. So on
    // a phone that cannot vibrate, a hidden switch is flipped instead.
    //
    // This is borrowed behaviour, not an API: it can go away in any iOS release, the
    // control has to actually be rendered for the haptic to fire (hence the styling
    // rather than display: none), and the feedback is the system's single fixed tap with
    // no say over length or strength. All of which is why the drawn flag, not this,
    // remains the feedback the game relies on.
    //
    // What is clicked is the label, not the checkbox inside it. WebKit plays the haptic
    // on the label's activation of the switch; a click dispatched straight at the input
    // toggles it silently. wwwroot/haptic-test.html is a standalone probe of this and the
    // other variants, for when a future iOS moves the goalposts again.
    let hapticSwitch = null;

    function makeHapticSwitch() {
        if (navigator.vibrate || !('switch' in document.createElement('input'))) {
            return null;
        }

        const label = document.createElement('label');
        label.className = 'haptic-switch';
        label.setAttribute('aria-hidden', 'true');

        const input = document.createElement('input');
        input.type = 'checkbox';
        input.setAttribute('switch', '');
        input.tabIndex = -1;

        label.appendChild(input);
        document.body.appendChild(label);

        return label;
    }

    function buzz(haptic) {
        if (navigator.vibrate) {
            navigator.vibrate(haptic.pattern);
            return;
        }

        if (!hapticSwitch) {
            return;
        }

        // Toggled, not set: which way the switch is left does not matter, only that it
        // moved. Nothing reads its value.
        //
        // The first tick is fired now rather than on a zero timer, so it still lands
        // inside the gesture that asked for it.
        hapticSwitch.click();

        for (let i = 1; i < haptic.ticks; i++) {
            setTimeout(() => hapticSwitch.click(), i * TICK_GAP_MS);
        }
    }

    function flag(point) {
        // Answered rather than anticipated: only the game knows whether a flag actually
        // moved, and a buzz for a press that landed on an open cell would be a lie.
        return dotNet.invokeMethodAsync('OnLongPress', point.x, point.y)
            .then((result) => {
                const haptic = HAPTICS[result];

                if (haptic !== undefined) {
                    buzz(haptic);
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
            hapticSwitch = makeHapticSwitch();

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
