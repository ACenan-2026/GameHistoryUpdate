using System.Collections.Generic;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.StrategiesTests
{
    [TestClass]
    public class LineBetWithStaticMultStrategyTests
    {
        private static MultiplierParams Params(int? multiplier) =>
            new MultiplierParams(multiplier, paid: true, strategy: new StrategySpec("LineBetWithStaticMult", new Dictionary<string, string>()));

        [TestMethod]
        public void Rounds_the_line_bet_base_to_two_places()
        {
            // round(1.00 * 1 / 3, 2) = 0.33, then × value(1)
            var reader = new FakeRoundReader { TotalBet = 1.00m };
            var strategy = new LineBetWithStaticMultStrategy(numLines: 1, staticBetMultiplier: 3);

            Assert.AreEqual(0.33m, strategy.GetWonAmount(reader, Params(1)).Value);
        }

        [TestMethod]
        public void Multiplies_before_dividing_then_rounds()
        {
            // round(10 * 2 / 3, 2) = round(6.6666.., 2) = 6.67
            var reader = new FakeRoundReader { TotalBet = 10m };
            var strategy = new LineBetWithStaticMultStrategy(numLines: 2, staticBetMultiplier: 3);

            Assert.AreEqual(6.67m, strategy.GetWonAmount(reader, Params(1)).Value);
        }

        [TestMethod]
        public void Applies_the_symbol_value_after_rounding_the_base()
        {
            // round(1.00 * 1 / 3, 2) = 0.33, then × value(10) = 3.30
            var reader = new FakeRoundReader { TotalBet = 1.00m };
            var strategy = new LineBetWithStaticMultStrategy(numLines: 1, staticBetMultiplier: 3);

            Assert.AreEqual(3.30m, strategy.GetWonAmount(reader, Params(10)).Value);
        }

        [TestMethod]
        public void Returns_null_when_the_value_was_missing()
        {
            var reader = new FakeRoundReader { TotalBet = 1.00m };
            var strategy = new LineBetWithStaticMultStrategy(numLines: 1, staticBetMultiplier: 3);

            Assert.IsNull(strategy.GetWonAmount(reader, Params(null)));
        }

        [TestMethod]
        public void Returns_null_when_the_total_bet_is_unknown()
        {
            var reader = new FakeRoundReader { TotalBet = null };
            var strategy = new LineBetWithStaticMultStrategy(numLines: 1, staticBetMultiplier: 3);

            Assert.IsNull(strategy.GetWonAmount(reader, Params(1)));
        }
    }
}
