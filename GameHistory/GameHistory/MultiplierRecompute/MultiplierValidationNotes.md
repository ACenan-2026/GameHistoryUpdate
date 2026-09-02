# Multiplier Recompute — Recorded-Outcome Validation Notes

_Design note, 2026-09-02. Scope: whether/how to traverse the Game History JSON to check computed multiplier values against what the spin actually recorded._

## Context

The multiplier-recompute feature displays a **finalised value** on each multiplier symbol, computed as `base × multiplier`, where `base` is a spin-level quantity supplied by a strategy (currently `TotalBet` → `base = total_bet`) and `multiplier` comes from per-game config.

That computed value is easy to attribute to a specific grid cell (we know each cell's symbol code, hence its multiplier), but it is **theoretical**: it is what the multiplier *should* produce, not what the recorded spin outcome says it *did* produce. The two can legitimately diverge — most obviously when a located scatter did not trigger and actually paid `0`.

The question this note answers: is there merit in traversing the JSON to recover the *recorded* outcome (e.g. whether a multiplier actually paid), and if so, how should we use it?

## The core tension: computed vs recorded

- **Computed (`base × value`)** — cleanly attributable to each grid cell, but theoretical; cannot know whether a specific occurrence triggered.
- **Recorded (`WonOutcome` / `Details`)** — the ground-truth amount the spin actually produced, but hard to attribute back to a specific grid cell.

Neither is complete on its own. The recommendation below uses the recorded data to *validate* the computed data rather than to replace it.

## What the history JSON can give us

For **located-scatter multipliers** (`B` / `TB`), the per-spin `Details` and `WonOutcome` fields record the real amounts. Example (RichCluckinEggs base game): the located scatters paid `20, 200, 20`, which is exactly the ground truth for `base × value` on `B01, B10, B01`. So traversal yields:

- The **actual amount** a multiplier win produced — including `0.00` for a located scatter that did not trigger, which the computed value cannot know.
- A **cross-check**: compute `base × value`, then confirm it appears among the recorded located-scatter amounts. Agreement is high confidence; divergence signals either bad config/base **or** an occurrence that did not pay.

## What the JSON cannot give us cleanly

- **Per-cell attribution is unreliable.** The `Symbols` field mislabels multipliers (every located scatter appears as `B01` even when it is `B10`), and located scatters carry no reel/row coordinates (`PositionOutcome` is `N/A`). So we learn "a located scatter paid 200," not "*this* cell paid 200." Matching by amount (computed `B10 → 200` ↔ recorded `200`) is possible but ambiguous when two symbols share a value.
- **The `Multiplier` / `MultiplierWin` fields are empty** for these games — the mechanic is recorded as a located-scatter `WinAmount`, not through those fields. Parsing must target `Type: LocatedScatter` + `WinAmount`, not a dedicated multiplier field.
- **Wild multipliers (`Wd`) get no help.** A wild's contribution is folded into the payline `WonOutcome` with no isolated amount, so traversal cannot extract "the wild's share" any better than computation can.
- **`Details` is a `<br/>`-delimited semi-structured string.** Parsing it is brittle and becomes a maintenance surface if the upstream format shifts.

## Recommendation — validation first, not replacement

Use traversal as a **validation / enrichment pass**, not as a replacement for the computed value — at least for the first strategy.

### Phase 1 (now): cross-check and log

Keep computing `base × value` for display (simple, cleanly attributable per cell). Add a pass that:

1. Extracts the recorded located-scatter amounts from each spin's `Details` / `WonOutcome`.
2. Cross-checks them against the computed values.
3. **Logs mismatches** (via the existing log4net logger).

This hardens the "computed can diverge from recorded" assumption: it converts a silent wrong number into a logged one, and immediately surfaces a bad config value or base — with **zero display risk**, because the display path is unchanged.

### Phase 2 (later): reconcile for display

If we later want the display to reflect reality — show the actual recorded amount, and visually distinguish non-paying multipliers (e.g. grey out a `TB` / didn't-trigger occurrence) — reconcile computed against recorded. This requires the attribution work above (amount-matching, or a `SymbolId → code` map) plus handling its ambiguity, so it is a larger step and should follow Phase 1.

## Framing

Traversing the history JSON is **downstream reconstruction of what the engine already computed and then partly discarded**. As a cross-check within the "stay in the Game History project" constraint it is genuinely useful and low-risk. As a source of per-cell ground truth it is fighting a lossy representation (unreliable `Symbols`, no coordinates, empty multiplier fields), which is why the durable answer still lives upstream — having the engine record the multiplier base and/or its resulting win at spin time.

The Phase 1 validation logs are themselves useful evidence: a steady stream of computed-vs-recorded divergences is a concrete argument for recording the multiplier outcome at the source.

## Assumptions this interacts with

The validation pass is the main mitigation for two assumptions in the compute path:

- **We display computed `base × value`, not the recorded per-occurrence outcome** — so occurrences that did not trigger still get a computed overlay.
- **Paid/unpaid is treated as a property of the symbol type (code), not the specific occurrence** — relies on the game encoding paid vs unpaid in distinct codes (`B` vs `TB`).

Validation does not fix these; it makes their violations visible.
