# GameHistory.Tests — Unit Test Catalog

A catalog of the unit tests in the `GameHistory.Tests` project, grouped by the production code they exercise.
Each entry names the class and method under test and the purpose of every test. Framework: MSTest
(`[TestClass]` / `[TestMethod]` / `[DataTestMethod]`).

Everything here targets the `MultiplierRecompute` and `SlotRoundReader` code. The RGS owns the multiplier maths,
so the frontend only draws a caller-supplied amount; these tests cover how that overlay is drawn and styled, the
config it reads styles from, and the round reader. A shared hand-written fake, `FakeRoundReader`, supplies round
data without a mocking library. Controller/frontend behaviour is covered separately by the manual end-to-end
plan (see the E2E test plan doc).

---

## SlotRoundReader — model accessors

**Under test:** `SlotRoundReader.GetSlotModel`, `GetGameName`, `GetSlotDetails`: the null-safe accessors over
the pulled round model.

### `SlotRoundReaderGettersTests`

`GetSlotModel` / `GetGameName`:
- **GetSlotModel_returns_the_slot_model** — returns the round's slot model instance.
- **GetSlotModel_is_null_for_a_null_round** — null round returns null.
- **GetGameName_returns_the_slot_models_game_name** — returns the slot model's game name.
- **GetGameName_is_null_when_there_is_no_slot_model** — missing slot model returns null.

`GetSlotDetails`:
- **GetSlotDetails_returns_the_detail_entries** — returns the per-spin detail entries.
- **GetSlotDetails_is_null_for_a_null_round** — null round returns null.

---

## MultiplierConfigParser — XML config parsing

**Under test:** `MultiplierConfigParser.GetMultiplierParams` (and its constructor), which parses the game's
`<Game>_reels.xml` into a `MultiplierSymbolMapping` of per-symbol render styles (`MultiplierParams`).

### `MultiplierConfigParserFallbackTests` (degradation / bad input)
- **No_GameHistoryConfig_element_yields_an_empty_mapping** — a config with no `GameHistoryConfig` yields an
  empty mapping.
- **Empty_multiplierGroups_yields_an_empty_mapping** — an empty `multiplierGroups` yields an empty mapping.
- **A_symbol_missing_its_name_is_skipped** — a name-less symbol is skipped; siblings still parse.
- **Malformed_xml_throws_from_the_constructor** — genuinely malformed XML throws from the constructor (the
  renderer catches it and uses the default style).

### `MultiplierConfigParserTests` (positive parse)

Group render styles:
- **Paid_and_unpaid_render_styles_are_parsed_onto_the_params** — `state="paid"` / `state="unpaid"` blocks land
  on `PaidStyle` / `UnpaidStyle`.
- **Unpaid_style_inherits_from_the_paid_style_when_not_given** — with only a paid block, the unpaid look falls
  back to it.
- **A_stateless_render_style_is_treated_as_the_paid_style** — a `renderStyle` with no `state` is the paid look.
- **Duplicate_paid_render_style_keeps_the_first** — a second paid block is ignored.
- **Unknown_render_style_state_is_ignored_and_the_look_stays_default** — an unrecognised `state` contributes
  nothing.
- **A_group_with_no_render_style_uses_the_default_look** — no blocks resolves to `RenderStyle.Default`.

Symbol-level rules:
- **Duplicate_symbol_keeps_the_first_definition** — a symbol defined in two groups keeps the first group's style.
- **An_unnamed_group_still_parses_its_symbols** — a group with no `name` still contributes its symbols.
- **Every_symbol_in_a_group_shares_the_group_style** — all symbols in a group take its style; other groups are
  unaffected.
- **Legacy_maths_attributes_are_tolerated_and_ignored** — old recompute attributes (`strategy`, `paid`,
  `overlay`, `claim`, `value`, `postWinDivider`) still parse; only the style is taken.
- **MultiplierParams_with_no_styles_resolves_both_to_the_default_look** — the params constructor resolves
  missing styles to the default.

---

## MultiplierOverlayRenderer — tile rendering

**Under test:** `MultiplierOverlayRenderer.BuildTile` and `PlainTile`. The amount is supplied by the caller, so
these pin down how a tile is drawn: overlay vs plain, number formatting, and style selection.

### `BuildTileTests`

Plain tile:
- **PlainTile_is_the_bare_original_image** — the plain markup is exactly `<img src="..." >`.
- **RenderOverlay_false_returns_the_plain_tile_even_for_a_configured_symbol** — `renderOverlay: false` always
  gives the plain tile.

Overlay markup and formatting:
- **Overlay_keeps_the_original_artwork_and_draws_the_amount_on_top** — the image is kept and the amount sits in
  an absolutely positioned span.
- **Amount_is_formatted_with_trailing_zeros_trimmed** — invariant culture, at most two decimals, trailing zeros
  trimmed (data-driven).
- **A_null_amount_is_drawn_as_zero** — a null amount renders `0`.

Every symbol is overlaid:
- **Every_symbol_is_overlaid_with_a_config** — configured, unconfigured, blank and null symbol names all get the
  overlay, and a null name does not throw (data-driven).
- **Every_symbol_is_overlaid_without_a_config** — with no config, every symbol is overlaid in the default style
  (data-driven).

Style selection:
- **A_configured_symbol_with_a_non_zero_amount_uses_its_paid_style**
- **A_configured_symbol_with_a_zero_amount_uses_its_unpaid_style**
- **An_unconfigured_symbol_uses_the_default_style**

---

## MultiplierOverlayRenderer — feature entry point

**Under test:** `MultiplierOverlayRenderer.TryCreate` (and its private `LoadMapping` / `ResolveGameConfigRoot`),
driven through real `ConfigurationManager` appSettings and temp config files. Whether the config was used is
observed through the overlay colour.

### `MultiplierOverlayRendererTryCreateTests`

The Enabled switch:
- **TryCreate_returns_null_when_the_feature_is_disabled** — `Enabled=false` returns null (plain tiles).
- **TryCreate_returns_null_when_the_feature_is_disabled_even_with_a_valid_config** — the switch wins over a
  present config.

A valid config is used:
- **TryCreate_with_a_valid_absolute_root_uses_the_configured_style**
- **TryCreate_resolves_an_app_relative_config_root_via_mapPath** — a `~/...` root goes through `mapPath`.
- **TryCreate_falls_back_to_GameConfig_beside_the_app_root_when_no_root_is_configured**

No usable config: still overlays, in the default style:
- **TryCreate_overlays_with_the_default_style_when_the_game_name_is_empty**
- **TryCreate_overlays_with_the_default_style_when_the_config_file_is_missing**
- **TryCreate_overlays_with_the_default_style_when_the_config_has_no_multiplier_symbols**
- **TryCreate_overlays_with_the_default_style_when_the_config_is_malformed** — the parse error is caught.

---

## RenderStyle — overlay text style

**Under test:** `RenderStyle.Default`, `OverrideOnto`, `AppendCss`, and `Parse` (with its private field
validators for colour, font, size, weight and outline).

### `RenderStyleTests` (defaults, merge, emit, colour parse)
- **Default_reproduces_the_historical_look** — `Default` carries the historical colour, size, weight and
  outline.
- **OverrideOnto_takes_this_styles_set_fields_and_inherits_the_null_ones** — `OverrideOnto` overrides with a
  delta's set fields and inherits its null fields.
- **AppendCss_emits_complete_typographic_declarations** — `AppendCss` emits font, size, weight, colour and the
  outline text-shadow.
- **AppendCss_drops_the_outline_when_it_is_none** — an outline of `none` drops the text-shadow.
- **Parse_valid_color_when_hex_color_used** — a hex colour parses through.
- **Parse_valid_color_when_plaintext_color_used** — a named colour (e.g. `blue`) parses through.
- **Parse_null_color_when_plaintext_color_is_invalid** — an invalid name drops to null (inherit).
- **Parse_null_color_when_hex_color_is_invalid** — an invalid hex drops to null (inherit).

### `RenderStyleValidationTests` (remaining validator/emit/merge branches)

`Parse` field validators:
- **Parse_accepts_a_plain_font_family_list** — a comma-separated font family list is accepted.
- **Parse_rejects_a_font_with_style_breaking_characters** — a font containing style-breaking characters
  (e.g. `;`) drops to null.
- **Parse_accepts_a_size_within_range** — a size within 1–200 is accepted.
- **Parse_rejects_an_out_of_range_or_non_integer_size** — `0`, `201`, `12px`, `abc` all drop to null
  (data-driven).
- **Parse_accepts_the_size_boundaries** — the boundary sizes `1` and `200` are accepted (data-driven).
- **Parse_accepts_valid_weights_case_insensitively** — `normal` / `bold` (any case) are accepted and
  normalised (data-driven).
- **Parse_rejects_an_unknown_weight** — an unknown weight drops to null.
- **Parse_accepts_none_as_the_outline** — an outline of `none` is accepted verbatim.
- **Parse_accepts_a_hex_outline_colour** — a hex outline colour is accepted.
- **Parse_rejects_an_invalid_outline** — an invalid outline drops to null.
- **Parse_accepts_a_three_digit_hex_colour** — a 3-digit `#RGB` colour is accepted.

`AppendCss` (emit):
- **AppendCss_emits_a_text_shadow_for_a_hex_outline** — a hex outline emits a text-shadow with that colour.
- **AppendCss_emits_the_normal_weight_when_set** — a `normal` weight is emitted as `font-weight:normal`.
- **AppendCss_fills_null_fields_from_the_default** — an all-null style still emits a complete declaration,
  filled from `Default`.

`Parse` / `OverrideOnto` edge cases:
- **Parse_of_a_null_element_yields_an_all_null_style** — parsing a null element yields an all-null (fully
  inheriting) style.
- **OverrideOnto_a_null_base_returns_this_instance** — overriding onto a null base returns the instance
  unchanged.

---

---

## Test support

- **`FakeRoundReader`** — a hand-written `ISlotRoundReader` fake (not a test). Fields default to benign values
  so tests set only what they need.
- **Test `App.config`** — supplies the `MultiplierRecompute.*` appSettings baseline for the TryCreate tests. It
  is compiled to `GameHistory.Tests.dll.config` and loaded only into the test process, entirely separate from
  the web app's `Web.config`.
