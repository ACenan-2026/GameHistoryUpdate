using System.Collections.Generic;
using GameHistory.Models;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.OverlayTests
{
    // The positive rendering paths of SpinOverlay/BuildTile: the overlay markup, once-placement cell selection, the
    // recorded-outcome gate choosing the paid vs unpaid style, the global gating flag, and amount formatting. The
    // fallback-to-plain paths are covered by SpinOverlayFallbackTests. Uses internal types via InternalsVisibleTo.
    [TestClass]
    public class SpinOverlayBehaviourTests
    {
        private const string Url = "~/Content/Images/Game/Desktop/B10.png";

        private static SlotSymbolReelViewModel Reel(params string[] floors)
        {
            var reel = new SlotSymbolReelViewModel("r") { Floors = new List<SlotSymbolViewModel>() };
            foreach (var f in floors) reel.Floors.Add(new SlotSymbolViewModel { SymbolName = f });
            return reel;
        }

        private static SlotSymbolTableViewModel Grid(params SlotSymbolReelViewModel[] reels) =>
            new SlotSymbolTableViewModel { Reels = new List<SlotSymbolReelViewModel>(reels) };

        private static MultiplierParams Paid(int? multiplier, RenderStyle paidStyle = null, RenderStyle unpaidStyle = null) =>
            new MultiplierParams(multiplier, paid: true,
                strategy: new StrategySpec("TotalBet", new Dictionary<string, string>()),
                groupName: "g", placement: MultiplierOverlayPlacement.All,
                paidStyle: paidStyle, unpaidStyle: unpaidStyle);

        private static MultiplierParams Once(int? multiplier, string group) =>
            new MultiplierParams(multiplier, paid: true,
                strategy: new StrategySpec("TotalBet", new Dictionary<string, string>()),
                groupName: group, placement: MultiplierOverlayPlacement.OnceOnLastOccurrence);

        private static SpinOverlay Overlay(
            MultiplierSymbolMapping mapping, IReadOnlyDictionary<string, decimal> computed,
            SlotSymbolTableViewModel spin, bool gate = false, List<decimal> recorded = null)
        {
            var ctx = new MultiplierOverlayContext { Mapping = mapping, Computed = computed, GateOnRecordedWin = gate };
            var reader = new FakeRoundReader { OneSpinScatterWins = recorded };
            return new SpinOverlay(ctx, spin, "details", reader);
        }

        // ----- overlay markup ----------------------------------------------------------------------

        [TestMethod]
        public void BuildTile_wraps_the_image_and_overlays_the_amount()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid(10));
            var computed = new Dictionary<string, decimal> { { "B10", 250m } };
            var overlay = Overlay(mapping, computed, Grid(Reel("B10")));

            var html = overlay.BuildTile(Url, "B10", 0, 0);

            StringAssert.Contains(html, "<img src=\"" + Url + "\"");   // the original artwork is kept
            StringAssert.Contains(html, "position:absolute");           // the overlay span
            StringAssert.Contains(html, ">250<");                       // the amount text
        }

        [DataTestMethod]
        [DataRow(200, "200")]      // whole number, no decimal point
        [DataRow(25, "25")]
        public void BuildTile_formats_whole_amounts_without_a_decimal_point(int amount, string expected)
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid(1));
            var computed = new Dictionary<string, decimal> { { "B10", amount } };
            var overlay = Overlay(mapping, computed, Grid(Reel("B10")));

            var html = overlay.BuildTile(Url, "B10", 0, 0);

            StringAssert.Contains(html, ">" + expected + "<");
        }

        [TestMethod]
        public void BuildTile_keeps_a_fractional_amount_and_trims_trailing_zeros()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid(1));
            // 5.50 must render as "5.5" (0.## trims the trailing zero), 12.00 as "12".
            var overlay1 = Overlay(mapping, new Dictionary<string, decimal> { { "B10", 5.50m } }, Grid(Reel("B10")));
            var overlay2 = Overlay(mapping, new Dictionary<string, decimal> { { "B10", 12.00m } }, Grid(Reel("B10")));

            StringAssert.Contains(overlay1.BuildTile(Url, "B10", 0, 0), ">5.5<");
            StringAssert.Contains(overlay2.BuildTile(Url, "B10", 0, 0), ">12<");
        }

        // ----- paid vs unpaid style (driven by the recorded-outcome gate) --------------------------

        [TestMethod]
        public void A_confirmed_paying_occurrence_uses_the_paid_style()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid(1,
                paidStyle: new RenderStyle("#FF0000", null, null, null, null),
                unpaidStyle: new RenderStyle("#00FF00", null, null, null, null)));
            var computed = new Dictionary<string, decimal> { { "B10", 250m } };
            // The spin recorded a located-scatter win equal to the computed amount -> this occurrence paid.
            var overlay = Overlay(mapping, computed, Grid(Reel("B10")), recorded: new List<decimal> { 250m });

            var html = overlay.BuildTile(Url, "B10", 0, 0);

            StringAssert.Contains(html, "color:#FF0000");
        }

        [TestMethod]
        public void A_non_paying_occurrence_uses_the_unpaid_style_when_gating_is_off()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid(1,
                paidStyle: new RenderStyle("#FF0000", null, null, null, null),
                unpaidStyle: new RenderStyle("#00FF00", null, null, null, null)));
            var computed = new Dictionary<string, decimal> { { "B10", 250m } };
            // No recorded win this spin -> not confirmed paid. Gating off, so it still renders, in the unpaid style.
            var overlay = Overlay(mapping, computed, Grid(Reel("B10")), recorded: new List<decimal>());

            var html = overlay.BuildTile(Url, "B10", 0, 0);

            StringAssert.Contains(html, "color:#00FF00");
        }

        [TestMethod]
        public void A_null_recorded_outcome_fails_open_to_the_paid_style()
        {
            // A spin with no reels means the gate cannot be built (null) -> fail open: treat as paid.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid(1,
                paidStyle: new RenderStyle("#FF0000", null, null, null, null),
                unpaidStyle: new RenderStyle("#00FF00", null, null, null, null)));
            var computed = new Dictionary<string, decimal> { { "B10", 250m } };
            var spinWithNoReels = new SlotSymbolTableViewModel { Reels = null };
            var overlay = Overlay(mapping, computed, spinWithNoReels, recorded: new List<decimal>());

            var html = overlay.BuildTile(Url, "B10", 0, 0);

            StringAssert.Contains(html, "color:#FF0000");   // paid look, not dimmed
        }

        // ----- gating on ---------------------------------------------------------------------------

        [TestMethod]
        public void With_gating_on_a_confirmed_payer_is_overlaid()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid(1));
            var computed = new Dictionary<string, decimal> { { "B10", 250m } };
            var overlay = Overlay(mapping, computed, Grid(Reel("B10")), gate: true, recorded: new List<decimal> { 250m });

            var html = overlay.BuildTile(Url, "B10", 0, 0);

            Assert.AreNotEqual(SpinOverlay.PlainTile(Url), html);
            StringAssert.Contains(html, ">250<");
        }

        // ----- once placement ----------------------------------------------------------------------

        [TestMethod]
        public void Once_placement_overlays_only_the_last_in_group_cell()
        {
            // Two Wh tiles on one reel (floor0, floor1). The overlay is drawn once, on the LAST in render order
            // (floor1); the earlier tile renders plain.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("Wh", Once(1, group: "wheel"));
            var computed = new Dictionary<string, decimal> { { "Wh", 300m } };
            var overlay = Overlay(mapping, computed, Grid(Reel("Wh", "Wh")), recorded: new List<decimal> { 300m });

            var lastCell = overlay.BuildTile(Url, "Wh", 0, 1);
            var firstCell = overlay.BuildTile(Url, "Wh", 0, 0);

            StringAssert.Contains(lastCell, ">300<");
            Assert.AreEqual(SpinOverlay.PlainTile(Url), firstCell);
        }

        [TestMethod]
        public void Once_placement_dedupes_across_differently_coded_group_members()
        {
            // Wh / Wh2 belong to the same group; only the last member in render order carries the overlay.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("Wh", Once(1, group: "wheel"));
            mapping.Insert("Wh2", Once(1, group: "wheel"));
            var computed = new Dictionary<string, decimal> { { "Wh", 300m }, { "Wh2", 300m } };
            var overlay = Overlay(mapping, computed, Grid(Reel("Wh"), Reel("Wh2")), recorded: new List<decimal> { 300m });

            // reel0 = Wh (earlier), reel1 = Wh2 (last) -> only Wh2 is overlaid.
            Assert.AreEqual(SpinOverlay.PlainTile(Url), overlay.BuildTile(Url, "Wh", 0, 0));
            StringAssert.Contains(overlay.BuildTile(Url, "Wh2", 1, 0), ">300<");
        }

        // ----- claim modes (count-scatter wins recorded with NumSymbols) ---------------------------

        private const string PaidColour = "color:#FF0000";
        private const string UnpaidColour = "color:#00FF00";

        private static MultiplierParams Claiming(MultiplierClaims claims, string group = "coins",
            MultiplierOverlayPlacement placement = MultiplierOverlayPlacement.All) =>
            new MultiplierParams(1, paid: true,
                strategy: new StrategySpec("TotalBet", new Dictionary<string, string>()),
                groupName: group, placement: placement, claims: claims,
                paidStyle: new RenderStyle("#FF0000", null, null, null, null),
                unpaidStyle: new RenderStyle("#00FF00", null, null, null, null));

        private static SpinOverlay OverlayWithEntries(
            MultiplierSymbolMapping mapping, IReadOnlyDictionary<string, decimal> computed,
            SlotSymbolTableViewModel spin, bool gate, params RecordedScatterWin[] recorded)
        {
            var ctx = new MultiplierOverlayContext { Mapping = mapping, Computed = computed, GateOnRecordedWin = gate };
            var reader = new FakeRoundReader { OneSpinScatterWinEntries = new List<RecordedScatterWin>(recorded) };
            return new SpinOverlay(ctx, spin, "details", reader);
        }

        // Cyber Cash, GameId 538: reel0 = Wd,R5,Wd  reel1 = R4,P1,P1  reels 2-4 = P1 x3  -> 11 P1 + R4 + R5 + 2 Wd.
        private static SlotSymbolTableViewModel CyberCashGrid() =>
            Grid(Reel("Wd", "R5", "Wd"), Reel("R4", "P1", "P1"), Reel("P1", "P1", "P1"),
                 Reel("P1", "P1", "P1"), Reel("P1", "P1", "P1"));

        private static SpinOverlay CyberCashOverlay(MultiplierClaims claims, bool gate = false)
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("P1", Claiming(claims));
            mapping.Insert("R4", Claiming(claims));
            mapping.Insert("R5", Claiming(claims));
            var computed = new Dictionary<string, decimal> { { "P1", 75m }, { "R4", 0.3m }, { "R5", 0.15m } };
            return OverlayWithEntries(mapping, computed, CyberCashGrid(), gate, new RecordedScatterWin(975m, 13));
        }

        [TestMethod]
        public void Shared_claims_mark_every_tile_of_a_count_win_as_paid()
        {
            var overlay = CyberCashOverlay(MultiplierClaims.Shared);

            // First P1 in render order (reel1 floor1) and the last (reel4 floor2) both take the paid style.
            StringAssert.Contains(overlay.BuildTile(Url, "P1", 1, 1), PaidColour);
            StringAssert.Contains(overlay.BuildTile(Url, "P1", 4, 2), PaidColour);
            StringAssert.Contains(overlay.BuildTile(Url, "P1", 4, 2), ">75<");
        }

        [TestMethod]
        public void Shared_claims_leave_symbols_below_the_trigger_unpaid()
        {
            // A single R4 / R5 did not reach 5 of a kind: no share of their value exists, so they stay unpaid.
            var overlay = CyberCashOverlay(MultiplierClaims.Shared);

            StringAssert.Contains(overlay.BuildTile(Url, "R4", 1, 0), UnpaidColour);
            StringAssert.Contains(overlay.BuildTile(Url, "R5", 0, 1), UnpaidColour);
            StringAssert.Contains(overlay.BuildTile(Url, "R4", 1, 0), ">0.3<");
        }

        [TestMethod]
        public void Whole_claims_leave_the_tiles_of_a_count_win_unpaid()
        {
            // The pre-claim behaviour: 75 != 975, so no P1 is confirmed paid.
            var overlay = CyberCashOverlay(MultiplierClaims.Whole);

            StringAssert.Contains(overlay.BuildTile(Url, "P1", 1, 1), UnpaidColour);
            StringAssert.Contains(overlay.BuildTile(Url, "P1", 4, 2), UnpaidColour);
        }

        [TestMethod]
        public void With_gating_on_shared_payers_render_and_below_trigger_symbols_are_suppressed()
        {
            var overlay = CyberCashOverlay(MultiplierClaims.Shared, gate: true);

            StringAssert.Contains(overlay.BuildTile(Url, "P1", 2, 0), ">75<");
            Assert.AreEqual(SpinOverlay.PlainTile(Url), overlay.BuildTile(Url, "R4", 1, 0));
            Assert.AreEqual(SpinOverlay.PlainTile(Url), overlay.BuildTile(Url, "R5", 0, 1));
        }

        [TestMethod]
        public void Unconfigured_wilds_render_plain_even_when_they_joined_the_count_win()
        {
            // The two Wd tiles account for the 2 leftover shares, but Wd is not configured, so it is never overlaid.
            var overlay = CyberCashOverlay(MultiplierClaims.Shared);

            Assert.AreEqual(SpinOverlay.PlainTile(Url), overlay.BuildTile(Url, "Wd", 0, 0));
        }

        [TestMethod]
        public void Shared_claims_mark_the_last_tiles_unpaid_once_the_shares_run_out()
        {
            // 3 P1 tiles but only 2 shares recorded: the first two in render order are paid, the third is not.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("P1", Claiming(MultiplierClaims.Shared));
            var computed = new Dictionary<string, decimal> { { "P1", 75m } };
            var overlay = OverlayWithEntries(mapping, computed, Grid(Reel("P1", "P1", "P1")), gate: false,
                new RecordedScatterWin(150m, 2));

            StringAssert.Contains(overlay.BuildTile(Url, "P1", 0, 0), PaidColour);
            StringAssert.Contains(overlay.BuildTile(Url, "P1", 0, 1), PaidColour);
            StringAssert.Contains(overlay.BuildTile(Url, "P1", 0, 2), UnpaidColour);
        }

        [TestMethod]
        public void A_computed_once_placement_whole_claim_is_unpaid_for_an_entry_counted_over_several_symbols()
        {
            // Whole claims require NumSymbols == 1; only TotalScatterWin is exempt. A wheel with a COMPUTED strategy
            // against a 3-symbol record is not confirmed, so its result tile takes the unpaid style.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("Wh", Claiming(MultiplierClaims.Whole, group: "wheel",
                placement: MultiplierOverlayPlacement.OnceOnLastOccurrence));
            var computed = new Dictionary<string, decimal> { { "Wh", 300m } };
            var overlay = OverlayWithEntries(mapping, computed, Grid(Reel("Wh"), Reel("Wh"), Reel("Wh")), gate: false,
                new RecordedScatterWin(300m, 3));

            StringAssert.Contains(overlay.BuildTile(Url, "Wh", 2, 0), UnpaidColour);
            Assert.AreEqual(SpinOverlay.PlainTile(Url), overlay.BuildTile(Url, "Wh", 0, 0));
        }

        // ----- TotalScatterWin: exempt from amount matching -----------------------------------------

        private static MultiplierParams WheelTotalScatterWin() =>
            new MultiplierParams(null, paid: true,
                strategy: new StrategySpec("TotalScatterWin", new Dictionary<string, string>()),
                groupName: "wheel", placement: MultiplierOverlayPlacement.OnceOnLastOccurrence,
                paidStyle: new RenderStyle("#FF0000", null, null, null, null),
                unpaidStyle: new RenderStyle("#00FF00", null, null, null, null));

        [TestMethod]
        public void A_TotalScatterWin_wheel_is_paid_on_a_spin_that_recorded_its_win()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("Wh", WheelTotalScatterWin());
            var computed = new Dictionary<string, decimal> { { "Wh", 300m } };
            var overlay = OverlayWithEntries(mapping, computed, Grid(Reel("Wh"), Reel("Wh"), Reel("Wh")), gate: false,
                new RecordedScatterWin(300m, 3));

            StringAssert.Contains(overlay.BuildTile(Url, "Wh", 2, 0), PaidColour);   // result tile
            StringAssert.Contains(overlay.BuildTile(Url, "Wh", 2, 0), ">300<");
        }

        [TestMethod]
        public void A_TotalScatterWin_wheel_is_unpaid_on_a_spin_that_recorded_nothing()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("Wh", WheelTotalScatterWin());
            var computed = new Dictionary<string, decimal> { { "Wh", 300m } };
            var overlay = OverlayWithEntries(mapping, computed, Grid(Reel("Wh"), Reel("Wh"), Reel("Wh")), gate: false);

            StringAssert.Contains(overlay.BuildTile(Url, "Wh", 2, 0), UnpaidColour);
        }
    }
}
