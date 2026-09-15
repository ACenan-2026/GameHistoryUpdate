using System.Collections.Generic;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.StrategiesTests
{
    [TestClass]
    public class TotalBetStrategyTests
    {
        private static MultiplierParams Params(int? multiplier) =>
            new MultiplierParams(multiplier, paid: true, strategy: new StrategySpec("TotalBet", new Dictionary<string, string>()));

        [TestMethod]
        public void Multiplies_total_bet_by_the_symbol_value()
        {
            var reader = new FakeRoundReader { TotalBet = 2.50m };

            decimal? amount = TotalBetStrategy.Instance.GetWonAmount(reader, Params(10));

            Assert.AreEqual(25.00m, amount.Value);
        }

        [TestMethod]
        public void Returns_null_when_the_value_was_missing()
        {
            // The int? change: a missing/invalid config 'value' becomes null, so a base×value strategy yields no
            // amount (and the tile renders plain) instead of the old 1000000007 sentinel figure.
            var reader = new FakeRoundReader { TotalBet = 2.50m };

            Assert.IsNull(TotalBetStrategy.Instance.GetWonAmount(reader, Params(null)));
        }

        [TestMethod]
        public void Returns_null_when_the_total_bet_is_unknown()
        {
            var reader = new FakeRoundReader { TotalBet = null };

            Assert.IsNull(TotalBetStrategy.Instance.GetWonAmount(reader, Params(10)));
        }
    }
}
