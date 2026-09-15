using System.Collections.Generic;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.OverlayTests
{
    // When an amount cannot be determined, the symbol must simply be absent from the computed map (never throw),
    // which is what makes the render loop fall back to the plain image for that tile.
    [TestClass]
    public class WonAmountsComputerFallbackTests
    {
        private static MultiplierParams Params(int? multiplier, string strategyType) =>
            new MultiplierParams(multiplier, paid: true, strategy: new StrategySpec(strategyType, new Dictionary<string, string>()));

        [TestMethod]
        public void Returns_empty_when_the_round_has_no_slot_model()
        {
            var reader = new FakeRoundReader { SlotModel = null };
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Params(10, "TotalBet"));

            var result = new WonAmountsComputer().ComputeWonAmounts(reader, mapping);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void Returns_empty_when_the_mapping_is_null()
        {
            var reader = new FakeRoundReader { TotalBet = 2m };

            var result = new WonAmountsComputer().ComputeWonAmounts(reader, null);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void Omits_a_symbol_whose_strategy_is_unknown_but_keeps_the_good_ones()
        {
            var reader = new FakeRoundReader { TotalBet = 2m };   // SlotModel non-null by default
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("Good", Params(10, "TotalBet"));       // 2 * 10 = 20
            mapping.Insert("Bad", Params(10, "NotAStrategy"));    // unknown strategy -> no amount

            var result = new WonAmountsComputer().ComputeWonAmounts(reader, mapping);

            Assert.IsTrue(result.ContainsKey("Good"));
            Assert.AreEqual(20m, result["Good"]);
            Assert.IsFalse(result.ContainsKey("Bad"));
        }

        [TestMethod]
        public void Omits_a_symbol_whose_amount_cannot_be_determined()
        {
            // TotalBet strategy with an unknown total bet yields no amount, so the symbol is dropped.
            var reader = new FakeRoundReader { TotalBet = null };
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Params(10, "TotalBet"));

            var result = new WonAmountsComputer().ComputeWonAmounts(reader, mapping);

            Assert.AreEqual(0, result.Count);
        }
    }
}
