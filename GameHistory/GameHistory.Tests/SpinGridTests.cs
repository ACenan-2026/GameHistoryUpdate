using System.Collections.Generic;
using System.Linq;
using GameHistory.Models;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests
{
    [TestClass]
    public class SpinGridTests
    {
        private static MultiplierParams Params(int? multiplier) =>
            new MultiplierParams(multiplier, paid: true, strategy: new StrategySpec("TotalBet", new Dictionary<string, string>()));

        private static SlotSymbolReelViewModel Reel(params string[] floors)
        {
            var reel = new SlotSymbolReelViewModel("r") { Floors = new List<SlotSymbolViewModel>() };
            foreach (var f in floors)
            {
                reel.Floors.Add(new SlotSymbolViewModel { SymbolName = f });
            }
            return reel;
        }

        private static SlotSymbolTableViewModel Grid(params SlotSymbolReelViewModel[] reels) =>
            new SlotSymbolTableViewModel { Reels = reels.ToList() };

        [TestMethod]
        public void Yields_configured_symbols_with_coordinates_and_amount()
        {
            var spin = Grid(Reel("B10", "X"));   // reel 0: floor0 = B10, floor1 = X
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Params(10));
            var computed = new Dictionary<string, decimal> { { "B10", 250m } };

            var occ = SpinGrid.Occurrences(spin, mapping, computed).Single();

            Assert.AreEqual("B10", occ.Symbol);
            Assert.AreEqual(0, occ.Reel);
            Assert.AreEqual(0, occ.Floor);
            Assert.AreEqual(250m, occ.Amount);
        }

        [TestMethod]
        public void Skips_symbols_that_are_unmapped_or_have_no_computed_amount()
        {
            var spin = Grid(Reel("B10", "X"));
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Params(10));       // B10 is mapped...
            var computed = new Dictionary<string, decimal>();  // ...but nothing was computed this round

            Assert.IsFalse(SpinGrid.Occurrences(spin, mapping, computed).Any());
        }

        [TestMethod]
        public void Advances_the_reel_index_even_past_a_null_reel()
        {
            // reel 0 is absent (null); the configured symbol sits on reel 1. The yielded coordinate must be reel 1,
            // matching the tile render loop which increments its reel index for every reel.
            var spin = new SlotSymbolTableViewModel
            {
                Reels = new List<SlotSymbolReelViewModel> { null, Reel("B10") }
            };
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Params(10));
            var computed = new Dictionary<string, decimal> { { "B10", 5m } };

            var occ = SpinGrid.Occurrences(spin, mapping, computed).Single();

            Assert.AreEqual(1, occ.Reel);
        }
    }
}
