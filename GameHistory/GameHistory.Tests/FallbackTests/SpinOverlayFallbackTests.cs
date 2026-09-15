using System.Collections.Generic;
using GameHistory.Models;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.OverlayTests
{
    // The headline fallback: in every "bad case" the tile builder returns the plain original image
    // (SpinOverlay.PlainTile), never a broken or empty overlay. Uses internal types via InternalsVisibleTo.
    [TestClass]
    public class SpinOverlayFallbackTests
    {
        private const string Url = "~/Content/Images/Game/Desktop/B10.png";

        private static MultiplierParams Paid(int? multiplier) =>
            new MultiplierParams(multiplier, paid: true, strategy: new StrategySpec("TotalBet", new Dictionary<string, string>()));

        private static SlotSymbolReelViewModel Reel(params string[] floors)
        {
            var reel = new SlotSymbolReelViewModel("r") { Floors = new List<SlotSymbolViewModel>() };
            foreach (var f in floors) reel.Floors.Add(new SlotSymbolViewModel { SymbolName = f });
            return reel;
        }

        private static SlotSymbolTableViewModel Grid(params SlotSymbolReelViewModel[] reels) =>
            new SlotSymbolTableViewModel { Reels = new List<SlotSymbolReelViewModel>(reels) };

        private static SpinOverlay Overlay(
            MultiplierSymbolMapping mapping, IReadOnlyDictionary<string, decimal> computed,
            SlotSymbolTableViewModel spin, bool gate = false)
        {
            var ctx = new MultiplierOverlayContext { Mapping = mapping, Computed = computed, GateOnRecordedWin = gate };
            return new SpinOverlay(ctx, spin, "", new FakeRoundReader());
        }

        [TestMethod]
        public void PlainTile_is_the_bare_original_image()
        {
            Assert.AreEqual("<img src=\"" + Url + "\" >", SpinOverlay.PlainTile(Url));
        }

        [TestMethod]
        public void BuildTile_falls_back_to_the_original_image_for_an_unmapped_symbol()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid(10));
            var computed = new Dictionary<string, decimal> { { "B10", 250m } };
            var overlay = Overlay(mapping, computed, Grid(Reel("X")));

            Assert.AreEqual(SpinOverlay.PlainTile(Url), overlay.BuildTile(Url, "X", 0, 0));
        }

        [TestMethod]
        public void BuildTile_falls_back_to_the_original_image_when_no_amount_was_computed()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid(10));
            var computed = new Dictionary<string, decimal>();   // nothing computed this round
            var overlay = Overlay(mapping, computed, Grid(Reel("B10")));

            Assert.AreEqual(SpinOverlay.PlainTile(Url), overlay.BuildTile(Url, "B10", 0, 0));
        }

        [TestMethod]
        public void BuildTile_falls_back_to_the_original_image_for_an_empty_symbol_name()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid(10));
            var computed = new Dictionary<string, decimal> { { "B10", 250m } };
            var overlay = Overlay(mapping, computed, Grid(Reel("B10")));

            Assert.AreEqual(SpinOverlay.PlainTile(Url), overlay.BuildTile(Url, "", 0, 0));
        }

        [TestMethod]
        public void BuildTile_suppresses_to_the_original_image_when_gating_on_and_no_recorded_win()
        {
            // Gating ON + the recorded outcome (empty here) does not confirm the win => suppress to the plain image.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid(10));
            var computed = new Dictionary<string, decimal> { { "B10", 250m } };
            var overlay = Overlay(mapping, computed, Grid(Reel("B10")), gate: true);

            Assert.AreEqual(SpinOverlay.PlainTile(Url), overlay.BuildTile(Url, "B10", 0, 0));
        }

        [TestMethod]
        public void BuildTile_overlays_the_amount_in_the_normal_case()
        {
            // Contrast: with a real computed amount and gating off, the tile is NOT the plain image.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid(10));
            var computed = new Dictionary<string, decimal> { { "B10", 250m } };
            var overlay = Overlay(mapping, computed, Grid(Reel("B10")));

            var html = overlay.BuildTile(Url, "B10", 0, 0);

            Assert.AreNotEqual(SpinOverlay.PlainTile(Url), html);
            StringAssert.Contains(html, "250");
        }
    }
}
