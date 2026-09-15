# Multiplier Overlay — Config Schema (`<Game>_reels.xml`)

This document describes the XML that drives the multiplier-amount overlay in Game History: how a game
declares its multiplier symbols, how their amounts are computed, and how each tile is styled (including the
paid vs. unpaid distinction).

For the *why* behind the recorded-outcome reconciliation and its limits, see `MultiplierValidationNotes.md`.
This file is the *what* — the schema reference for config authors.

---

## Where the file lives and how it is loaded

Per game, the config is `<GameName>_reels.xml`, resolved at runtime as:

```
<MultiplierRecompute.GameConfigRoot>\<GameName>\<GameName>_reels.xml
```

`GameConfigRoot` is a Web.config appSetting (typically `C:\inetpub\wwwroot\GameConfig`). `<GameName>` is the
value the reader returns for the round, and must match the `gameName` attribute inside the file.

> **Deploy note.** The running app reads the *deployed* copy under `GameConfigRoot`, and executes the
> *deployed* `GameHistory.dll`. After changing this schema (or the parser), redeploy **both** the config and a
> freshly built DLL and recycle the app pool — a stale binary reading a new-schema config (or vice versa) is a
> common source of "it looks right but behaves wrong" bugs.

---

## Document skeleton

```xml
<AgtReelConfig>
  <agtReels>
    <!-- Reel layout (unrelated to the multiplier overlay). -->
  </agtReels>

  <GameHistoryConfig gameName="ExampleGame">
    <multiplierGroups>

      <group name="..." strategy="..." paid="true|false" overlay="all|onceLast" [strategy attributes]>
        <renderStyle .../>                 <!-- optional: base / paid look -->
        <renderStyle state="unpaid" .../>  <!-- optional: delta for non-payers -->
        <symbol name="..." value="..."/>
        <!-- ...more symbols... -->
      </group>

      <!-- ...more groups... -->

    </multiplierGroups>
  </GameHistoryConfig>
</AgtReelConfig>
```

Only the `GameHistoryConfig` subtree is read by the multiplier feature. If it is absent or has no multiplier
symbols, the game renders plain symbols (feature no-op).

---

## Element reference

### `<GameHistoryConfig gameName="…">`

| Attribute  | Required | Meaning |
|------------|----------|---------|
| `gameName` | yes      | Must match the game's runtime name; used only for logging/traceability. |

### `<multiplierGroups>`

Container for one or more `<group>` elements. No attributes.

### `<group>`

A group is a set of symbols that share a strategy, a placement, a **paid status**, and a render style.

| Attribute  | Required | Default | Meaning |
|------------|----------|---------|---------|
| `name`     | recommended | `(unnamed)` | Group label. Used in logs and to dedupe "once" placement across differently-coded members. |
| `strategy` | yes (for output) | — | How each symbol's amount is computed. See [Strategies](#strategies). Missing → symbols resolve no amount (WARN). |
| `paid`     | yes | `false` (+ WARN) | **Group-level.** `true` = a paying class (e.g. `B`); `false` = a non-paying class (e.g. `TB`). A group is *wholly* paid or *wholly* unpaid — this is what forces paid and unpaid symbols into separate groups. Missing/invalid defaults to `false` and warns. |
| `overlay`  | no | `all` | Placement. See [Placement](#placement-overlay). |
| *strategy attrs* | depends | — | Extra attributes read by some strategies (e.g. `ratioNumerator`, `numLines`). See [Strategies](#strategies). |

> **`paid` is group-level, not per-symbol.** A `paid` attribute on a `<symbol>` is ignored (with a WARN). To
> have both paying and non-paying variants of the same denomination, put them in two groups (see the
> [paid/unpaid model](#paid-vs-unpaid-and-the-three-visual-buckets)).

### `<symbol>`

| Attribute | Required | Meaning |
|-----------|----------|---------|
| `name`    | yes | The symbol code exactly as it appears on the history grid (e.g. `B10`, `TB10`, `Wh3`). |
| `value`   | yes for base×value strategies | The multiplier value. Used by `TotalBet` / `LineBetTotal` / `LineBetFromStaticMultiplier` as the `× value` factor. **Documentation-only** for `TotalScatterWin`. A missing/invalid `value` yields the sentinel `1000000007` — if you see that amount, a `value` is missing. |
| `paid`    | — | **Deprecated.** Ignored with a WARN; set `paid` on the `<group>` instead. |

Duplicate `name` within the config is first-wins (later definitions ignored, with a WARN).

---

## Strategies

The `strategy` attribute selects how each symbol's overlay amount is computed. Unknown/misspelled strategy →
no amount (the tile renders plain), logged.

| `strategy` | Extra group attributes | Amount | Use when |
|------------|------------------------|--------|----------|
| `TotalBet` | none | `totalBet × value` | The located-scatter base is the whole total bet. |
| `LineBetTotal` | `ratioNumerator`, `ratioDenominator` (non-zero) | `round(totalBet × ratioNumerator / ratioDenominator, 2) × value` | The base is a fixed fraction of the total bet (line-bet total), expressed as a raw ratio. |
| `LineBetFromStaticMultiplier` | `numLines`, `staticBetMultiplier` (non-zero) | `round(totalBet × numLines / staticBetMultiplier, 2) × value` | Same as above, but expressed with the game's own constants. |
| `TotalScatterWin` | none | The recorded located-scatter win read straight from the round; `value` is not used. Returns nothing when no scatter win was recorded → the tile renders plain. | Wheel/jackpot features where the amount can't be reconstructed from config (base × value), but the round always records the resulting located-scatter win. |

Notes:

- `LineBetTotal` and `LineBetFromStaticMultiplier` share one implementation; they are just two ways to supply
  the same ratio. Do **not** use either for games where the line count or bet multiplier varies per spin.
- For base×value strategies, choosing the correct base matters: if the recorded located-scatter win is
  `totalBet × value`, use `TotalBet`; if it's a line-bet fraction, use one of the line-bet strategies. A wrong
  base means computed amounts never reconcile with the recorded wins (see the
  [paid-decision caveat](#how-paidunpaid-is-decided-at-render-time)).

---

## Placement (`overlay`)

Controls how many of a symbol's on-grid occurrences carry the overlay.

| `overlay` value | Behaviour |
|-----------------|-----------|
| `all` (default) | Overlay every in-scope occurrence. Correct when each occurrence is an independent win (e.g. located scatters). |
| `once` / `onceLast` / `onceOnLastOccurrence` | Overlay a single occurrence per spin — the **last** in-group tile in render order (right-most reel, then lowest floor). Use when several tiles share one win won only once (e.g. a three-symbol wheel trigger where only the result tile carries the multiplier). |

An unrecognised value falls back to `all` (with a WARN).

---

## Paid vs. unpaid, and the three visual buckets

"Did not pay" covers two different cases:

1. **Never pays** — a statically non-paying symbol (`paid="false"` group, e.g. the `TB` teaser codes).
2. **Didn't trigger this spin** — a paying-class symbol (`paid="true"` group) that appeared but did not meet
   the trigger, so it paid nothing on this particular spin.

Because paid status and render style are both **per group**, you get up to three distinct looks with no special
markup — just by putting the non-paying symbols in their own group:

| Bucket | Where | Style it takes |
|--------|-------|----------------|
| Paid this spin | `paid="true"` group, confirmed by the recorded outcome | that group's **base/paid** style |
| Didn't trigger this spin | `paid="true"` group, not confirmed | that group's **`state="unpaid"`** style |
| Never pays | `paid="false"` group | that group's style |

### Recommended pattern for a non-paying group

Give a `paid="false"` group a **single base `<renderStyle>`** (no `state="unpaid"`). Every symbol in it is
non-paying, so they all take that one look, and the group is immune to the fail-open edge (see
[render-time decision](#how-paidunpaid-is-decided-at-render-time)) because its paid and unpaid styles are
identical.

---

## Render styling (`<renderStyle>`)

Optional. Up to two per group: a **base** (no `state`, or `state="paid"`) and an **unpaid delta**
(`state="unpaid"`).

### Attributes

| Attribute | Values | Notes |
|-----------|--------|-------|
| `color`   | `#RGB` or `#RRGGBB` | Text fill. |
| `outline` | `#RGB` / `#RRGGBB`, or `none` | Text outline (a 4-direction shadow plus a soft glow). `none` drops the outline. |
| `size`    | integer `1`–`200` | Font size in px. |
| `weight`  | `normal` \| `bold` | Font weight. |
| `font`    | family list | e.g. `Arial, Helvetica, sans-serif`. Restricted to letters, digits, spaces, commas, hyphens and single quotes — **no raw CSS**. |
| `state`   | (absent) / `paid` / `unpaid` | Which look this block defines. |

An invalid value for any attribute is dropped (with a WARN) and that field inherits, rather than breaking the
overlay markup.

### Precedence (cascade)

```
code default  ←  base / state="paid"  ←  state="unpaid" delta
```

- **Code default** (used for any field never specified): white `#FFFFFF`, `Arial, Helvetica, sans-serif`,
  `18` px, `bold`, outline `#000000`. This is the historical look.
- The **base** block overrides the default for the fields it sets.
- The **unpaid** block is a *delta*: it only needs the fields that differ from the paid look; everything else
  inherits from the base.

Consequences:

- Omit **both** blocks → every tile renders the historical default look.
- Omit **only** the unpaid block → non-payers look identical to payers (no distinction drawn).

### How `paid`/`unpaid` is decided at render time

For each rendered tile the amount overlay takes the **paid** style if the occurrence paid this spin, otherwise
the **unpaid** style:

```
paidThisSpin = symbol is in a paid="true" group
               AND the recorded located-scatter outcome confirms this occurrence (its cell, or its group for
               a "once" placement)
```

- A `paid="false"` (never-pay) symbol is never confirmed → always the unpaid style.
- If the recorded outcome can't be read for a spin, the tile **fails open**: treated as paid (paid style,
  never suppressed) rather than dimmed or hidden. This is why a uniform single-style non-paying group is
  robust — paid and unpaid resolve to the same look, so fail-open is invisible.

> **Caveat (base×value strategies).** The paid-vs-didn't-trigger distinction relies on the *computed*
> `base × value` matching a *recorded* located-scatter win by amount. If the strategy/base is wrong, or the
> game's history grid doesn't surface the paying denomination, no occurrence is confirmed and every paying-class
> tile falls to the unpaid style. `TotalScatterWin` sidesteps this: a non-paying tile gets no amount at all and
> simply renders plain. See `MultiplierValidationNotes.md` for the full discussion; the Phase-1 validator logs
> exactly this reconciliation (`computed … has no matching recorded …` / `recorded … has no matching computed …`).

---

## Interacting Web.config settings

| appSetting | Values | Effect |
|------------|--------|--------|
| `MultiplierRecompute.Enabled` | `true`/`false` | Master switch for the whole feature. |
| `MultiplierRecompute.GateOverlayOnRecordedWin` | `false` (default) / `true` | **off:** every configured symbol is overlaid, payers and non-payers told apart by render style — this is where the unpaid/never-pay styles are seen. **on:** only occurrences the recorded outcome confirms paid are overlaid; every non-payer is suppressed to the plain symbol, so the unpaid style is never drawn. |
| `MultiplierRecompute.GameConfigRoot` | path | Root folder holding `<Game>\<Game>_reels.xml`. |

(The former `MultiplierRecompute.OverlayIncludesUnpaid` setting has been removed.)

---

## Worked examples

### 1. Located scatters, paid + never-pay split (`TotalBet`)

Paying `B` symbols white/bold; a `B` that didn't trigger shown dimmed grey; teaser `TB` symbols in a distinct
darker grey.

```xml
<group name="LocatedScatterPaid" strategy="TotalBet" paid="true">
  <renderStyle color="#FFFFFF" font="Arial, Helvetica, sans-serif" size="18" weight="bold" outline="#000000"/>
  <renderStyle state="unpaid" color="#9AA0A6" weight="normal"/>
  <symbol name="B01" value="1"/>
  <symbol name="B10" value="10"/>
  <!-- ... -->
</group>

<group name="LocatedScatterNeverPay" strategy="TotalBet" paid="false">
  <renderStyle color="#6B7075" font="Arial, Helvetica, sans-serif" size="18" weight="normal" outline="#000000"/>
  <symbol name="TB01" value="1"/>
  <symbol name="TB10" value="10"/>
  <!-- ... -->
</group>
```

### 2. Wheel result, single overlay per spin (`TotalScatterWin`, `overlay="onceLast"`)

All wheel codes are paying; only one tile per spin carries the amount (the result symbol). `value` is
documentation-only here.

```xml
<group name="WheelResult" strategy="TotalScatterWin" overlay="onceLast" paid="true">
  <symbol name="Wh"  value="1"/>
  <symbol name="Wh2" value="2"/>
  <symbol name="Wh3" value="3"/>
  <symbol name="Wh4" value="4"/>
  <symbol name="Wh5" value="5"/>
</group>
```

### 3. Line-bet base via game constants (`LineBetFromStaticMultiplier`)

```xml
<group name="LocatedScatter" strategy="LineBetFromStaticMultiplier"
       numLines="20" staticBetMultiplier="30" paid="true">
  <symbol name="B01" value="1"/>
  <symbol name="B25" value="25"/>
  <!-- ... -->
</group>
```

---

## Validation & warnings (what the parser logs)

- Group with no valid `strategy` → WARN; its symbols resolve no amount.
- Group with no valid `paid` → defaults to `false` (non-paying) + WARN.
- `<symbol>` with a `paid` attribute → ignored + WARN (`paid` is group-level now).
- `<symbol>` with no `name` → skipped + WARN.
- Duplicate symbol `name` → first kept, later ignored + WARN.
- `renderStyle` with an invalid attribute value → that field dropped (inherits) + WARN.
- `renderStyle` with an unrecognised `state` → ignored + WARN.
- Unrecognised `overlay` value → treated as `all` + WARN.

---

## Migration notes (from the per-symbol `paid` schema)

Earlier configs set `paid` on each `<symbol>`. The current schema sets it on the `<group>`:

1. Move `paid` up to every `<group>` (`paid="true"` or `paid="false"`) and remove it from the `<symbol>`s.
2. Split any group that mixed paying and non-paying symbols into two groups — a `paid="true"` group and a
   `paid="false"` group — each repeating the shared `strategy` (and its attributes).
3. Redeploy the config **and** a rebuilt `GameHistory.dll` together; a new-schema config read by an old binary
   makes every symbol resolve `paid=false` (and vice versa), which silently mis-styles everything.
