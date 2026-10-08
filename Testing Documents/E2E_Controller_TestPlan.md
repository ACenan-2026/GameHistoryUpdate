# Multiplier Overlay — End-to-End / Controller Test Plan

Scope note. The unit tests cover the `MultiplierRecompute` and `SlotRoundReader` directories (config parsing,
render styles, tile rendering, config loading) in isolation. They deliberately do **not** exercise the
`HomeController` details action or the rendered page, because that path is web-hosted (needs
`Server.MapPath`, `Url.Content`, `ConfigurationManager` from the app's `Web.config`, a real pulled round and the
game's `<Game>_reels.xml` on disk). The scenarios below are manual / browser-driven checks against a running
deployment.

The RGS owns the multiplier maths and location data. Until it supplies real values, the controller draws a
placeholder amount (`MULTIPLIER_DUMMY_VALUE`, `1.23`) on every tile.

Integration point under test: `HomeController` resolves the round, calls
`MultiplierOverlayRenderer.TryCreate(slotRoundReader, Server.MapPath)`, then per tile calls
`overlay.BuildTile(symbolUrl, overlayAmount: MULTIPLIER_DUMMY_VALUE, renderOverlay: true, symbolName: ...)`,
falling back to `MultiplierOverlayRenderer.PlainTile` when the renderer is null (feature off).

## Environment / fixtures

- A deployed GameHistory app (IIS or IIS Express) with access to a `GameConfig` root.
- `Web.config` appSettings:
  - `MultiplierRecompute.Enabled`
  - `MultiplierRecompute.GameConfigRoot` (absolute, or `~/...`; falls back to `..\GameConfig` beside the app root)

Suggested rounds to stage:
- **R1 — Styled game**: a game whose `<Game>_reels.xml` gives at least one group a distinctive `renderStyle`
  (e.g. `color="#FFD700"`), with one of its symbols on the grid.
- **R2 — No config**: a game with no `<Game>_reels.xml` (or an empty `multiplierGroups`).
- **R3 — Bad config**: a game whose `<Game>_reels.xml` is malformed XML.

## Scenarios

### E2E-1 — Feature disabled reproduces the historical page
Set `MultiplierRecompute.Enabled = false`. Open R1's details view.
Expect: every outcome tile is the bare `<img src="…">` (no overlay `<span>`), byte-for-byte the pre-feature
output.

### E2E-2 — Enabled: every tile shows the placeholder
Set `Enabled = true`, open R1.
Expect: **every** tile, including non-multiplier and blank/hidden symbols, shows `1.23` centred on the artwork.

### E2E-3 — Configured style is applied
On R1 (enabled), the configured symbols show `1.23` in their group's style (e.g. gold); every other symbol
uses the default look (white, bold, black outline).

### E2E-4 — Config-root resolution modes
Run E2E-3 three ways: (a) absolute `GameConfigRoot`; (b) app-relative `~/GameConfig`; (c) `GameConfigRoot`
blank, relying on the `..\GameConfig` sibling fallback. All three must show the configured style.

### E2E-5 — No config still overlays
Open R2 with the feature enabled.
Expect: every tile shows `1.23` in the default style, no error on the page, and a DEBUG log line noting no
config / no multiplier symbols.

### E2E-6 — Bad config is non-fatal
Open R3 with the feature enabled.
Expect: the page renders, every tile shows `1.23` in the default style, and an ERROR is logged from the
renderer's config load. The overlay must never take down the history page.

### E2E-7 — Grid/detail alignment
On a round where the grid spin count and detail spin count differ, confirm the page does not error and tiles
render for the overlapping spins.

## Pass criteria
- Feature-off output is identical to the pre-feature page (E2E-1).
- With the feature on, every tile carries the overlay, whatever the config state.
- No scenario produces a server error or a broken/empty tile.
- Log levels are as specified (DEBUG for expected no-ops, ERROR only for a genuine failure).
