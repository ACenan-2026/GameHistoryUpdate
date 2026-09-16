# Multiplier Overlay — End-to-End / Controller Test Plan

Scope note. The unit tests in this project cover the `MultiplierRecompute` and `SlotRoundReader`
directories (computation, config parsing, validation, per-tile rendering) in isolation. They deliberately
do **not** exercise the `HomeController` details action or the rendered page, because that path is
web-hosted (needs `Server.MapPath`, `Url.Content`, `ConfigurationManager` from the app's `Web.config`, a
real pulled round and the game's `<Game>_reels.xml` on disk). The scenarios below close that gap: they are
manual / browser-driven checks against a running deployment, verifying that what the unit tests prove in the
small holds true once the controller wires it into the tile loop.

Integration point under test: `HomeController` resolves the round, calls
`MultiplierOverlayRenderer.TryCreate(slotRoundReader, Server.MapPath)`, then for each spin calls
`overlay?.BeginSpin(...)` and per tile `spinOverlay.BuildTile(symbolUrl, symbolName, reelIdx, floorIdx)`
(falling back to `SpinOverlay.PlainTile` when the feature is off). A null renderer must reproduce the
historical plain-symbol output exactly.

## Environment / fixtures

- A deployed GameHistory app (IIS or IIS Express) with access to a `GameConfig` root.
- `Web.config` appSettings:
  - `MultiplierRecompute.Enabled`
  - `MultiplierRecompute.GameConfigRoot` (absolute, or `~/...`; falls back to `..\GameConfig` beside the app root)
  - `MultiplierRecompute.GateOverlayOnRecordedWin`
- At least one known game round per scenario, reachable through the Game History details view, whose
  recorded outcome is known (so the expected overlay amounts can be asserted by eye).
- A `<Game>_reels.xml` per game exercising the relevant strategy / placement / render styles.

Suggested rounds to stage:
- **R1 — TotalBet game**: a base×value multiplier symbol (e.g. `B10`, value 10) present on the grid, with a
  known total bet, and a recorded located-scatter win that matches `bet × value`.
- **R2 — Wheel/jackpot game (TotalScatterWin, once placement)**: three `Wh` trigger symbols in one group,
  one recorded located-scatter win.
- **R3 — Mixed paid + unpaid**: a paid group (`B`) and a statically-unpaid group (`TB`) that share an
  equal value, with distinct paid/unpaid render styles.
- **R4 — No multiplier config**: a game with no `<Game>_reels.xml` (or an empty `multiplierGroups`).

## Scenarios

### E2E-1 — Feature disabled reproduces the historical page
Set `MultiplierRecompute.Enabled = false`. Open R1's details view.
Expect: every outcome tile is the bare `<img src="…">` (no overlay `<span>`), byte-for-byte the pre-feature
output. `TryCreate` returns null; the loop uses `SpinOverlay.PlainTile`.

### E2E-2 — Enabled, valid config overlays the finalised amount
Set `Enabled = true`, point `GameConfigRoot` at the staged config, open R1.
Expect: the multiplier tile shows the computed amount (`bet × value`) centred on the artwork; all other
tiles unchanged. Confirms `TryCreate` → `BeginSpin` → `BuildTile` and the `Server.MapPath`/`Url.Content`
wiring.

### E2E-3 — Config-root resolution modes
Run E2E-2 three ways: (a) absolute `GameConfigRoot`; (b) app-relative `~/GameConfig`; (c) `GameConfigRoot`
unset, relying on the `..\GameConfig` sibling fallback. All three must overlay identically. Confirms
`ResolveGameConfigRoot` against the real `Server.MapPath`.

### E2E-4 — Missing / empty config falls back to plain
Open R4 with the feature enabled.
Expect: plain tiles, no overlay, no error on the page, a DEBUG log line noting no config / no multiplier
symbols. Confirms the non-fatal early-outs in `PrepareContext`.

### E2E-5 — Paid vs unpaid render style (gating off)
Set `GateOverlayOnRecordedWin = false`, open R3.
Expect: the paid symbol that the recorded outcome confirms renders in the **paid** style; the unpaid (`TB`)
symbol — and any paid symbol the outcome did not confirm — renders the amount in the **unpaid** style. Both
are visible; they differ only in the configured style delta (e.g. colour).

### E2E-6 — Recorded-win gating on suppresses non-payers
Set `GateOverlayOnRecordedWin = true`, open R3.
Expect: only occurrences confirmed by the recorded located-scatter win show an overlay (paid style); every
non-payer (including all `TB`) renders as the plain image. Confirms the gate's suppression path in
`BuildMultiplierTile`.

### E2E-7 — "Once" placement draws a single overlay
Open R2 (three `Wh` in one `overlay="once"` group).
Expect: exactly one `Wh` tile — the last in render order (right-most reel, then lowest floor) — carries the
amount; the other `Wh` tiles render plain. The single amount equals the recorded located-scatter win
(TotalScatterWin), not `value ×` anything. Confirms `ResolveOnceOverlayCells` + once-group gate dedup end
to end.

### E2E-8 — Amount formatting
Across R1/R2, verify the on-tile text matches `FormatOverlayAmount`: no currency symbol, invariant decimal
point, trailing zeros trimmed (`200`, `25`, `5.5`, `12.5`). Check a decimal-valued win specifically.

### E2E-9 — Non-fatal degradation
Force a bad config (malformed XML, or a `reels.xml` the process cannot read) for an enabled game and open
its details view.
Expect: the page still renders with plain tiles (no yellow-screen), and an ERROR is logged from
`PrepareContext`'s catch. The multiplier feature must never take down the history page.

### E2E-10 — Validation is log-only
With the feature enabled on a round where a recorded located-scatter win has no matching computed value
(e.g. a deliberately wrong `value` in config), open the details view.
Expect: the displayed page is unchanged by validation (log-only), and a WARN is logged naming the game,
spin and unexplained amount; a computed value with no recorded match logs at DEBUG. Confirms the Phase 1
validator runs in the request path without altering output.

### E2E-11 — Grid/detail alignment
On a round where the grid spin count and detail spin count differ, confirm overlays still line up with the
correct tiles for the overlapping spins and the page does not error (the loop and the validator both clamp
to the shared count).

## Pass criteria
- Feature-off output is identical to the pre-feature page (E2E-1).
- Every overlay amount matches the value proven by the corresponding unit test for that strategy.
- No scenario produces a server error or a broken/empty tile; every failure mode degrades to the plain
  image.
- Log levels are as specified (DEBUG for expected no-ops, WARN for unexplained recorded wins, ERROR only
  for a genuine failure).
