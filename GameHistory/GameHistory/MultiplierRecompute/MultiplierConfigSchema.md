# Multiplier Overlay — Config Schema (`<Game>_reels.xml`)

This document describes the XML that styles the amount overlay in Game History. The amounts themselves are
not computed here: the RGS owns the maths and location data, and the frontend currently draws a placeholder
value (`1.23`) on every tile. The config only decides how that number looks, per symbol.

---

## Where the file lives and how it is loaded

Per game, the config is `<GameName>_reels.xml`, resolved at runtime as:

```
<MultiplierRecompute.GameConfigRoot>\<GameName>\<GameName>_reels.xml
```

`GameConfigRoot` is a Web.config appSetting (typically `C:\inetpub\wwwroot\GameConfig`). If it is blank, the
app looks for `GameConfig` beside the app root (e.g. `...\wwwroot\GameConfig` for `...\wwwroot\GameHistory`).

The config is optional. With the feature enabled, a missing, empty or malformed config does **not** turn the
overlay off: every tile is still overlaid, in the code default style.

> **Deploy note.** The running app reads the *deployed* copy under `GameConfigRoot`, and executes the
> *deployed* `GameHistory.dll`. After changing this schema (or the parser), redeploy **both** the config and a
> freshly built DLL and recycle the app pool.

---

## Document skeleton

```xml
<AgtReelConfig>
  <agtReels>
    <!-- Reel layout (unrelated to the overlay). -->
  </agtReels>

  <GameHistoryConfig gameName="ExampleGame">
    <multiplierGroups>

      <group name="...">
        <renderStyle .../>                 <!-- optional: base / paid look -->
        <renderStyle state="unpaid" .../>  <!-- optional: delta for a zero amount -->
        <symbol name="..."/>
        <!-- ...more symbols... -->
      </group>

      <!-- ...more groups... -->

    </multiplierGroups>
  </GameHistoryConfig>
</AgtReelConfig>
```

Only the `GameHistoryConfig` subtree is read.

> **Legacy attributes.** Configs written for the old frontend recompute feature also carry `postWinDivider`,
> `strategy`, `paid`, `overlay`, `claim`, strategy attributes (`numLines`, `staticBetMultiplier`, ...) and
> per-symbol `value`. These are now ignored and can be removed at leisure; they do not cause warnings.

---

## Element reference

### `<GameHistoryConfig gameName="…">`

| Attribute  | Required | Meaning |
|------------|----------|---------|
| `gameName` | yes      | Should match the game's runtime name; for traceability only. |

### `<multiplierGroups>`

Container for one or more `<group>` elements. No attributes.

### `<group>`

A set of symbols that share a render style.

| Attribute | Required | Default | Meaning |
|-----------|----------|---------|---------|
| `name`    | recommended | `(unnamed)` | Group label, used in log messages. |

### `<symbol>`

| Attribute | Required | Meaning |
|-----------|----------|---------|
| `name`    | yes | The symbol code exactly as it appears on the history grid (e.g. `B10`, `TB10`, `Wh3`). |

Duplicate `name` within the config is first-wins (later definitions ignored, with a WARN).

---

## Render styling (`<renderStyle>`)

Optional. Up to two per group: a **base** (no `state`, or `state="paid"`) and an **unpaid delta**
(`state="unpaid"`).

### Attributes

| Attribute | Values | Notes |
|-----------|--------|-------|
| `color`   | a colour name (`red`, `yellow`, …) or `#RGB` / `#RRGGBB` hex | Text fill. Names are resolved via `ColorTranslator.FromHtml`; functional forms like `rgb()`/`hsl()` are not accepted. |
| `outline` | a colour name or `#RGB` / `#RRGGBB` hex, or `none` | Text outline (a 4-direction shadow plus a soft glow). `none` drops the outline. |
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

- **Code default** (used for any field never specified, and for every symbol not in the config): white
  `#FFFFFF`, `Arial, Helvetica, sans-serif`, `18` px, `bold`, outline `#000000`.
- The **base** block overrides the default for the fields it sets.
- The **unpaid** block is a *delta*: it only needs the fields that differ from the paid look; everything else
  inherits from the base.

Consequences:

- Omit **both** blocks → the group's symbols render in the code default look.
- Omit **only** the unpaid block → zero and non-zero amounts look the same.

### Which style a tile gets

| Symbol | Amount | Style |
|--------|--------|-------|
| In the config | non-zero | the group's paid style |
| In the config | zero | the group's unpaid style |
| Not in the config (or no config) | any | code default |

> **Interim rule.** "Zero means unpaid" is a stand-in while the amount is a placeholder. Once the RGS supplies
> real values, whether a symbol paid should come from the RGS as well, since a non-paying symbol can still
> carry a non-zero value.

---

## Interacting Web.config settings

| appSetting | Values | Effect |
|------------|--------|--------|
| `MultiplierRecompute.Enabled` | `true`/`false` | Master switch. `false` renders plain tiles everywhere; `true` overlays every tile. |
| `MultiplierRecompute.GameConfigRoot` | path | Root folder holding `<Game>\<Game>_reels.xml`. Absolute, app-relative (`~/...`), or blank for the sibling fallback. |

---

## Example

```xml
<GameHistoryConfig gameName="LaughingDragon">
  <multiplierGroups>
    <group name="paying">
      <renderStyle color="#FFD700" outline="#000000" />
      <renderStyle state="unpaid" color="#808080" />
      <symbol name="B01" />
      <symbol name="B10" />
    </group>
    <group name="never-pay">
      <renderStyle color="#808080" />
      <symbol name="TB01" />
    </group>
  </multiplierGroups>
</GameHistoryConfig>
```

---

## Validation & warnings (what the parser logs)

- `<symbol>` with no `name` → skipped + WARN.
- Duplicate symbol `name` → first kept, later ignored + WARN.
- `renderStyle` with an invalid attribute value → that field dropped (inherits) + WARN.
- `renderStyle` with an unrecognised `state` → ignored + WARN.
- More than one paid (or unpaid) `renderStyle` in a group → first kept + WARN.
- Malformed XML → logged as an ERROR by the renderer; the round is overlaid in the default style.
