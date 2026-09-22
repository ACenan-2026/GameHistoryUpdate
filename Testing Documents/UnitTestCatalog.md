# GameHistory.Tests — Unit Test Catalog

A catalog of the unit tests in the `GameHistory.Tests` project, grouped by the production code they exercise.
Each entry names the class and method under test and the purpose of every test. Framework: MSTest
(`[TestClass]` / `[TestMethod]` / `[DataTestMethod]`).

Everything here targets the `MultiplierRecompute` and `SlotRoundReader` code. A shared hand-written fake,
`FakeRoundReader` (an `ISlotRoundReader` stand-in with settable backing data), supplies round data to the
tests without a mocking library. Controller/frontend behaviour is covered separately by the manual
end-to-end plan (see the E2E test plan doc), not by these unit tests.

---

## SlotRoundReader — recorded scatter-win parsing

**Under test:** `SlotRoundReader.GetOneSpinScatterWins`, `GetScatterWins`, `GetScatterWinsTotal` (and, through
them, the private `GetScatterWonAmount` / `ParseAllScatterWins` and `IsScatterWinCategory`). These parse the
fragile `<br/>`-delimited "Details" string into per-spin located-scatter amounts.

### `SlotRoundReaderScatterTests`

`GetOneSpinScatterWins` (single-spin Details parse):
- **OneSpin_reads_a_single_scatter_win** — one `Basic_Scatter` entry yields its one amount.
- **OneSpin_reads_multiple_entries_split_on_br_in_order** — multiple entries split on `<br/>` are returned in
  order.
- **OneSpin_ignores_payline_entries_and_keeps_only_scatter_wins** — `Payline` entries are excluded; only
  scatter-kind wins are kept.
- **OneSpin_drops_zero_amount_scatter_markers** — a scatter entry with `WinAmount: 0` (a non-paying marker)
  is dropped.
- **OneSpin_parses_decimal_amounts** — decimal amounts (e.g. `12.50`) parse without loss.
- **OneSpin_is_case_insensitive_for_keys_and_the_scatter_type** — keys (`type`/`winamount`) and the
  `basic_scatter` type match case-insensitively.
- **OneSpin_ignores_unrelated_fields_within_an_entry** — extra fields inside an entry (e.g. `Position`) are
  ignored.
- **OneSpin_ignores_non_win_entries** — non-win markup (e.g. `GambleOutcome=3`) contributes nothing.
- **OneSpin_returns_empty_for_null_or_empty_details** — null or empty Details returns an empty list.
- **OneSpin_tolerates_a_trailing_br_delimiter** — a trailing `<br/>` does not create a phantom entry.

`GetScatterWins` (per-spin grouping over the round):
- **GetScatterWins_groups_wins_per_spin_with_an_empty_list_for_a_scatterless_spin** — one inner list per
  spin, empty for a spin that recorded no scatter.
- **GetScatterWins_returns_an_empty_list_when_there_is_no_round_data** — a null round yields an empty result.
- **GetScatterWins_returns_independent_copies_that_callers_cannot_use_to_corrupt_the_cache** — each call
  returns fresh copies, so a caller mutating the lists cannot corrupt the parsed-once cache.

`GetScatterWinsTotal` (round-wide aggregate):
- **GetScatterWinsTotal_sums_every_scatter_win_across_the_round** — sums all scatter wins, ignoring paylines.
- **GetScatterWinsTotal_is_zero_when_there_is_no_round_data** — null round totals zero.
- **GetScatterWinsTotal_sums_decimal_amounts_without_precision_loss** — decimal precision is preserved in the
  sum.
- **GetScatterWinsTotal_excludes_zero_amount_scatter_markers** — zero-amount markers do not count.
- **GetScatterWinsTotal_is_zero_when_the_round_has_spins_but_no_scatter_wins** — a round of payline-only spins
  totals zero (distinct from the null-round case).
- **GetScatterWinsTotal_counts_every_scatter_within_a_single_spin** — multiple scatters in one spin are all
  counted.
- **GetScatterWinsTotal_counts_every_scatter_within_a_single_spin_extra_br** — the same, with a trailing
  delimiter.
- **GetScatterWinsTotal_equals_the_sum_of_the_per_spin_scatter_wins** — the total agrees with the per-spin
  breakdown it aggregates.

---

## SlotRoundReader — model accessors

**Under test:** `SlotRoundReader.GetTotalBet`, `GetSlotModel`, `GetGameName`, `GetUserPositionDict`,
`GetSlotDetails` — the null-safe accessors over the pulled round model.

### `SlotRoundReaderGettersTests`

`GetTotalBet`:
- **GetTotalBet_parses_the_slot_models_bet** — parses the slot model's `Bet` string into a decimal.
- **GetTotalBet_is_culture_invariant** — a dot is the decimal point regardless of locale.
- **GetTotalBet_is_null_when_the_bet_is_unparseable** — a non-numeric bet returns null.
- **GetTotalBet_is_null_when_there_is_no_slot_model** — a missing slot model returns null.
- **GetTotalBet_is_null_for_a_null_round** — a null round returns null.

`GetSlotModel` / `GetGameName`:
- **GetSlotModel_returns_the_slot_model** — returns the round's slot model instance.
- **GetSlotModel_is_null_for_a_null_round** — null round returns null.
- **GetGameName_returns_the_slot_models_game_name** — returns the slot model's game name.
- **GetGameName_is_null_when_there_is_no_slot_model** — missing slot model returns null.

`GetUserPositionDict` / `GetSlotDetails`:
- **GetUserPositionDict_returns_the_position_dict** — returns the per-spin position dictionary.
- **GetUserPositionDict_is_null_for_a_null_round** — null round returns null.
- **GetSlotDetails_returns_the_detail_entries** — returns the per-spin detail entries.
- **GetSlotDetails_is_null_for_a_null_round** — null round returns null.

---

## SpinGrid — grid traversal

**Under test:** `SpinGrid.Occurrences` — walks a spin's grid in render order and yields each configured
multiplier symbol that has a computed amount, with its coordinates.

### `SpinGridTests`
- **Yields_configured_symbols_with_coordinates_and_amount** — a mapped symbol with a computed amount is
  yielded with the correct reel/floor and amount.
- **Skips_symbols_that_are_unmapped_or_have_no_computed_amount** — a mapped symbol with no computed amount is
  not yielded.
- **Advances_the_reel_index_even_past_a_null_reel** — the reel index advances for a null reel, so yielded
  coordinates stay aligned with the tile render loop.

---

## Strategies — base-value computation

**Under test:** the `IMultiplierBaseStrategy.GetWonAmount` implementations and the
`MultiplierBaseStrategyResolver.Resolve` factory.

### `TotalBetStrategyTests` — `TotalBetStrategy.GetWonAmount`
- **Multiplies_total_bet_by_the_symbol_value** — computes `total_bet × value`.
- **Returns_null_when_the_value_was_missing** — a null multiplier value yields no amount (renders plain, not
  the old sentinel).
- **Returns_null_when_the_total_bet_is_unknown** — an unknown total bet yields no amount.

### `LineBetWithStaticMultStrategyTests` — `LineBetWithStaticMultStrategy.GetWonAmount`
- **Rounds_the_line_bet_base_to_two_places** — the line-bet base is rounded to two decimals.
- **Multiplies_before_dividing_then_rounds** — multiplies before dividing so an unreduced ratio stays exact,
  then rounds.
- **Applies_the_symbol_value_after_rounding_the_base** — the symbol value is applied after the base is
  rounded.
- **Returns_null_when_the_value_was_missing** — a null value yields no amount.
- **Returns_null_when_the_total_bet_is_unknown** — an unknown total bet yields no amount.

### `TotalScatterWinStrategyTests` — `TotalScatterWinStrategy.GetWonAmount`
- **Returns_the_recorded_scatter_win_total_when_positive** — returns the recorded located-scatter total.
- **Returns_null_when_no_scatter_win_was_recorded** — a zero total yields null (tile renders plain).
- **Passes_the_recorded_total_through_unchanged_ignoring_the_symbol_value** — the symbol value is
  documentation-only; the total is not scaled.
- **Works_even_when_the_symbol_value_is_missing** — a null value is harmless because value is ignored.
- **Preserves_decimal_amounts** — decimal amounts pass through unchanged.

### `MultiplierBaseStrategyResolverTests` — `MultiplierBaseStrategyResolver.Resolve`
- **Unknown_strategy_type_resolves_to_null** — an unknown type resolves to null (never throws).
- **Null_strategy_type_resolves_to_null** — a null type resolves to null.
- **TotalBet_resolves_to_a_strategy** — `"TotalBet"` resolves to a strategy.
- **LineBetWithStaticMult_with_a_zero_multiplier_resolves_to_null** — a zero `staticBetMultiplier`
  resolves to null.

### `MultiplierBaseStrategyResolverExtraTests` — `MultiplierBaseStrategyResolver.Resolve` (remaining branches)
- **LineBetWithStaticMult_with_valid_constants_resolves_to_a_strategy** — valid `numLines` /
  `staticBetMultiplier` resolve to a strategy.
- **LineBetWithStaticMult_missing_numLines_resolves_to_null** — a missing `numLines` resolves to null.
- **TotalScatterWin_resolves_to_the_shared_strategy_instance** — `"TotalScatterWin"` resolves to the shared
  `TotalScatterWinStrategy.Instance`.
- **TotalBet_resolves_to_the_shared_strategy_instance** — `"TotalBet"` resolves to the shared
  `TotalBetStrategy.Instance`.
- **LineBetWithStaticMult_with_only_numLines_resolves_to_null** — a missing `staticBetMultiplier` resolves to null.
- **LineBetWithStaticMult_with_a_non_integer_constant_resolves_to_null** — a non-integer `numLines` resolves to null.

---

## WonAmountsComputer — per-symbol amount map

**Under test:** `WonAmountsComputer.ComputeWonAmounts` — maps each configured symbol to its finalised amount,
omitting any that cannot be determined.

### `WonAmountsComputerFallbackTests`
- **Returns_empty_when_the_round_has_no_slot_model** — no slot model yields an empty map.
- **Returns_empty_when_the_mapping_is_null** — a null mapping yields an empty map.
- **Omits_a_symbol_whose_strategy_is_unknown_but_keeps_the_good_ones** — an unknown-strategy symbol is
  dropped while valid symbols are kept.
- **Omits_a_symbol_whose_amount_cannot_be_determined** — a symbol whose strategy returns no amount (e.g.
  unknown total bet) is dropped.

---

## MultiplierConfigParser — XML config parsing

**Under test:** `MultiplierConfigParser.GetMultiplierParams` (and its constructor), which parses the game's
`<Game>_reels.xml` into a `MultiplierSymbolMapping` of `MultiplierParams`.

### `MultiplierConfigParserFallbackTests` (degradation / bad input)
- **No_GameHistoryConfig_element_yields_an_empty_mapping** — a config with no `GameHistoryConfig` yields an
  empty mapping.
- **Empty_multiplierGroups_yields_an_empty_mapping** — an empty `multiplierGroups` yields an empty mapping.
- **A_group_missing_its_strategy_still_parses_but_the_symbol_resolves_no_strategy** — a strategy-less group
  still parses; the symbol carries a null strategy type.
- **A_symbol_missing_its_value_parses_with_a_null_multiplier** — a value-less symbol parses with a null
  multiplier.
- **A_symbol_missing_its_name_is_skipped** — a name-less symbol is skipped; siblings still parse.
- **Malformed_xml_throws_from_the_constructor** — genuinely malformed XML throws from the constructor (the
  renderer wraps and degrades to plain).

### `MultiplierConfigParserTests` (positive parse of group/symbol attributes)

Overlay placement (`ParsePlacement` via `GetMultiplierParams`):
- **Overlay_absent_defaults_to_all** — an absent `overlay` attribute defaults to `All`.
- **Overlay_all_parses_to_all** — `overlay="all"` parses to `All`.
- **Overlay_once_synonyms_parse_to_once_on_last_occurrence** — `once` / `onceLast` /
  `onceOnLastOccurrence` (case-insensitive) all parse to `OnceOnLastOccurrence` (data-driven).
- **Overlay_unrecognised_value_degrades_to_all** — an unrecognised `overlay` degrades to `All`.

Group `paid` (`ParseGroupPaid`):
- **Group_paid_true_makes_its_symbols_paid** — `paid="true"` marks symbols paid.
- **Group_paid_false_makes_its_symbols_unpaid** — `paid="false"` marks symbols unpaid.
- **Group_with_no_paid_attribute_defaults_to_unpaid** — an absent `paid` defaults to unpaid (fail-safe).
- **Group_with_an_unparseable_paid_attribute_defaults_to_unpaid** — an unparseable `paid` defaults to unpaid.

Group render styles (`ParseGroupStyles` + `MultiplierParams` style resolution):
- **Paid_and_unpaid_render_styles_are_parsed_onto_the_params** — `state="paid"` and `state="unpaid"`
  render styles land on `PaidStyle` / `UnpaidStyle`.
- **Unpaid_style_inherits_from_the_paid_style_when_not_given** — with no unpaid block, the unpaid look falls
  back to the paid style.
- **A_stateless_render_style_is_treated_as_the_paid_style** — a `renderStyle` with no `state` is treated as
  the paid style.
- **Duplicate_paid_render_style_keeps_the_first** — a second paid `renderStyle` is ignored (first wins).
- **Unknown_render_style_state_is_ignored_and_the_look_stays_default** — an unknown `state` is ignored and the
  style resolves to the default look.
- **A_group_with_no_render_style_uses_the_default_look** — no `renderStyle` yields the historical default
  colour and size.

Symbol-level rules:
- **Duplicate_symbol_keeps_the_first_definition** — a duplicate symbol name keeps the first definition.
- **A_per_symbol_paid_attribute_is_ignored_in_favour_of_the_group** — a per-symbol `paid` is ignored; the
  group's `paid` wins.
- **An_unnamed_group_still_parses_its_symbols_under_a_placeholder_group_name** — a name-less group parses
  under the `(unnamed)` placeholder.
- **Multiple_groups_all_contribute_their_symbols_with_group_context** — multiple groups each contribute their
  symbols with the right group name and paid flag.
- **Strategy_type_and_attributes_are_captured_on_each_symbol** — the group's strategy type and attributes are
  captured on each symbol's `StrategySpec`.

---

## MultiplierComputationValidator — Phase 1 log-only validation

**Under test:** `MultiplierComputationValidator.ValidateRound` — cross-checks computed multiplier amounts
against recorded located-scatter wins per spin (a multiset match by amount) and returns a
`MultiplierValidationResult`. Log-only in production; these tests assert on the returned result.

### `MultiplierComputationValidatorTests`
- **A_computed_value_matching_a_recorded_win_produces_no_discrepancy** — a computed value matching a recorded
  win leaves no discrepancy.
- **A_recorded_win_with_no_matching_computed_value_is_flagged_as_unexplained** — an unmatched recorded win is
  reported in `UnexplainedRecorded` (with no symbol).
- **A_computed_value_with_no_matching_recorded_win_is_flagged_as_unmatched** — an unmatched computed value is
  reported in `UnmatchedComputed` (with its symbol).
- **Statically_unpaid_symbols_are_not_reconciled** — an unpaid (`TB`) symbol cannot claim a recorded win, so
  the win stays unexplained.
- **Once_placement_group_counts_a_single_computed_entry_per_spin** — a `once`-placement group's several tiles
  count as a single computed entry (dedup), matching the one shared recorded win.
- **All_placement_group_counts_every_occurrence** — an `all`-placement group counts each occurrence, so a
  surplus computed value is left unmatched (contrast with the once case).
- **The_spin_key_comes_from_the_user_position_dict_when_present** — the discrepancy's spin key comes from the
  user-position dictionary when available.
- **Mismatched_grid_and_detail_counts_validate_only_the_overlapping_spins** — differing grid/detail counts
  validate only the overlapping spins (clamped to the minimum) without error.
- **Returns_an_empty_result_when_there_are_no_grid_spins** — a null grid yields an empty result.
- **Returns_an_empty_result_when_the_computed_map_is_null** — a null computed map yields an empty result.
- **Discrepancy_flag_is_true_when_either_side_is_non_empty** — `MultiplierValidationResult.HasDiscrepancies`
  is true when either discrepancy list is non-empty, false when both are empty.

---

## SpinOverlay — per-tile rendering

**Under test:** `SpinOverlay.BuildTile` / `PlainTile` and the spin-scoped setup done in the constructor and
`MultiplierOverlayRenderer.BeginSpin` (via the internal `BuildMultiplierTile`, `ResolveOnceOverlayCells`,
`ResolveRecordedOverlayGate` and `FormatOverlayAmount`). Uses internal types via `InternalsVisibleTo`.

### `SpinOverlayFallbackTests` (fall back to the plain image)
- **PlainTile_is_the_bare_original_image** — `PlainTile` is the bare `<img>` markup.
- **BuildTile_falls_back_to_the_original_image_for_an_unmapped_symbol** — an unmapped symbol renders plain.
- **BuildTile_falls_back_to_the_original_image_when_no_amount_was_computed** — a mapped symbol with no
  computed amount renders plain.
- **BuildTile_falls_back_to_the_original_image_for_an_empty_symbol_name** — an empty symbol name renders
  plain.
- **BuildTile_suppresses_to_the_original_image_when_gating_on_and_no_recorded_win** — with gating on and no
  recorded win, the tile is suppressed to the plain image.
- **BuildTile_overlays_the_amount_in_the_normal_case** — with a computed amount and gating off, the tile is
  not plain and shows the amount.

### `SpinOverlayBehaviourTests` (positive rendering paths)

Overlay markup / formatting (`BuildTile` + `FormatOverlayAmount`):
- **BuildTile_wraps_the_image_and_overlays_the_amount** — the tile keeps the original image and adds an
  absolutely-positioned overlay span with the amount.
- **BuildTile_formats_whole_amounts_without_a_decimal_point** — whole amounts render without a decimal point
  (data-driven).
- **BuildTile_keeps_a_fractional_amount_and_trims_trailing_zeros** — fractional amounts keep needed digits and
  trim trailing zeros (`5.50`→`5.5`, `12.00`→`12`).

Paid vs unpaid style (driven by the recorded-outcome gate):
- **A_confirmed_paying_occurrence_uses_the_paid_style** — an occurrence confirmed by a matching recorded win
  uses the paid style.
- **A_non_paying_occurrence_uses_the_unpaid_style_when_gating_is_off** — an unconfirmed occurrence still
  renders (gating off), in the unpaid style.
- **A_null_recorded_outcome_fails_open_to_the_paid_style** — when the recorded outcome can't be read, the tile
  fails open to the paid style rather than dimming.

Gating on:
- **With_gating_on_a_confirmed_payer_is_overlaid** — with gating on, a confirmed payer is overlaid with its
  amount.

Once placement (`ResolveOnceOverlayCells`):
- **Once_placement_overlays_only_the_last_in_group_cell** — a `once` group overlays only the last in-group
  cell in render order; earlier tiles render plain.
- **Once_placement_dedupes_across_differently_coded_group_members** — the `once` dedup spans differently-coded
  members of the same group (e.g. `Wh` / `Wh2`).

---

## MultiplierOverlayRenderer — feature entry point

**Under test:** `MultiplierOverlayRenderer.TryCreate` and, through it, `PrepareContext` and
`ResolveGameConfigRoot` — the config-gated, filesystem-backed entry point. These tests drive the real
`ConfigurationManager` appSettings (from the isolated test `App.config`, overridden per test) and real temp
`<Game>_reels.xml` files.

### `MultiplierOverlayRendererPrepareContextTests`
- **TryCreate_returns_null_when_the_feature_is_disabled** — with `MultiplierRecompute.Enabled` off, returns
  null (render plain).
- **TryCreate_returns_null_when_the_game_name_is_empty** — an empty game name returns null.
- **TryCreate_returns_null_when_the_config_file_is_missing** — a missing `<Game>_reels.xml` returns null.
- **TryCreate_returns_null_when_the_config_has_no_multiplier_symbols** — a config with no multiplier symbols
  returns null.
- **TryCreate_with_a_valid_absolute_root_returns_a_renderer_that_overlays_the_amount** — a valid absolute
  `GameConfigRoot` returns a renderer whose tile overlays the computed amount end-to-end.
- **TryCreate_resolves_an_app_relative_config_root_via_mapPath** — an app-relative `~/...` root is resolved
  through the injected `mapPath` delegate.
- **TryCreate_falls_back_to_GameConfig_beside_the_app_root_when_no_root_is_configured** — with no configured
  root, it falls back to `GameConfig` as a sibling of the app root.

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

## Test support

- **`FakeRoundReader`** — a hand-written `ISlotRoundReader` fake (not a test). Fields default to benign values
  so tests set only what they need; the collection accessors hand out fresh copies so consumers that mutate
  the lists cannot corrupt the fake's configured data.
- **Test `App.config`** — supplies the `MultiplierRecompute.*` appSettings baseline for the PrepareContext
  tests. It is compiled to `GameHistory.Tests.dll.config` and loaded only into the test process, entirely
  separate from the web app's `Web.config`.
