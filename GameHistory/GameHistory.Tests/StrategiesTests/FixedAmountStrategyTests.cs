using System.Collections.Generic;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.StrategiesTests
{
    [TestClass]
    public class FixedAmountStrategyTests
    {
        private static MultiplierParams Params(int? multiplier) =>
            new MultiplierParams(multiplier, paid: true, strategy: new StrategySpec("FixedAmount", new Dictionary<string, string>()));

        [TestMethod]
        public void Returns_the_configured_amount_as_is()
        {
            // A fixed jackpot: the money figure comes entirely from config, not the round.
            var reader = new FakeRoundReader { TotalBet = 2.50m };

            decimal? amount = FixedAmountStrategy.Instance.GetWonAmount(reader, Params(40));

            Assert.AreEqual(40m, amount.Value);
        }

        [TestMethod]
        public void Ignores_the_total_bet()
        {
            // Unlike TotalBet/LineBet, the bet does not scale a fixed prize — the value is the win.
            var withBet = new FakeRoundReader { TotalBet = 5.00m };
            var noBet = new FakeRoundReader { TotalBet = null };

            Assert.AreEqual(200m, FixedAmountStrategy.Instance.GetWonAmount(withBet, Params(200)).Value);
            Assert.AreEqual(200m, FixedAmountStrategy.Instance.GetWonAmount(noBet, Params(200)).Value);
        }

        [TestMethod]
        public void Returns_null_when_no_amount_is_configured()
        {
            // No 'value' -> nothing to render, so the tile renders plain rather than a zero.
            var reader = new FakeRoundReader { TotalBet = 2.50m };

            Assert.IsNull(FixedAmountStrategy.Instance.GetWonAmount(reader, Params(null)));
        }

        [TestMethod]
        public void Resolver_maps_the_FixedAmount_type_to_the_strategy()
        {
            var strategy = MultiplierBaseStrategyResolver.Resolve("FixedAmount", new Dictionary<string, string>());

            Assert.IsInstanceOfType(strategy, typeof(FixedAmountStrategy));
        }
    }
}
