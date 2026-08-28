# bMinesweeper

Minesweeper in Blazor WebAssembly, rendered to a 2D canvas. Scaffolded from bSolitaire —
the plumbing is done, the game is not.

## Run

```bash
dotnet run --project BMinesweeper
```

Then open the URL it prints (http://localhost:5080).

## Layout

| Path | Purpose |
| --- | --- |
| `BMinesweeper/Game/Minesweeper.cs` | The game. Plain C#, no Blazor or JS — write the rules here. |
| `BMinesweeper/Game/Cell.cs` | One square of the grid, and the game's overall state. |
| `BMinesweeper/Game/BoardLayout.cs` | Cell ↔ pixel, in one place, so the game and the drawer agree. |
| `BMinesweeper/Game/IGameDrawer.cs` | Rendering seam, so the game never touches a canvas. |
| `BMinesweeper/Game/CanvasDrawer.cs` | Draws the grid to a 2D canvas. |
| `BMinesweeper/Pages/Home.razor` | Host component: wires the frame loop and input to the game. |
| `BMinesweeper/wwwroot/js/game.js` | `requestAnimationFrame` loop + DPI-aware canvas sizing. |

## How a frame works

1. `game.js` calls `Home.OnFrame(timestamp)` once per animation frame.
2. `Home` calls `Minesweeper.Update(elapsed)`, then `IGameDrawer.Draw(game)` — but only
   when `NeedsRedraw` is set, because the board is static almost all the time.
3. The next frame is requested only after that round trip completes, so a slow frame
   delays the next one rather than queueing interop calls behind it.

Input flows the same direction: `Home` forwards clicks and key presses to
`Minesweeper.OnClick(x, y)` / `OnFlag(x, y)` / `OnKeyDown(code)`. Coordinates are CSS
pixels with the origin at the top-left of the board — the canvas is scaled for device
pixel ratio in JS, so the game never deals with DPI.

## What is left to write

The two `TODO`s in `Minesweeper.cs` are the whole game:

- `EnsureMines` — bury the mines on the first click, skipping the 3x3 around it, then fill
  in each cell's `Adjacent` count.
- `OnClick` — reveal a cell: flood-fill the run of zeroes, lose on a mine, win once every
  cell that is not a mine is open.

After that, the obvious next things: chording, a long press to flag on touch, difficulty
presets, and a status line for the clock and the mine counter.
