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
    }
}
