using System.Collections.Generic;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.StrategiesTests
{
    // Bad/misconfigured strategy config must resolve to a null strategy (never throw), so the computer can skip
    // that symbol and the tile falls back to the plain image.
    [TestClass]
    public class MultiplierBaseStrategyResolverTests
    {
        [TestMethod]
        public void Unknown_strategy_type_resolves_to_null()
        {
            Assert.IsNull(MultiplierBaseStrategyResolver.Resolve("NotAStrategy", null));
        }

        [TestMethod]
        public void Null_strategy_type_resolves_to_null()
        {
            Assert.IsNull(MultiplierBaseStrategyResolver.Resolve(null, null));
        }

        [TestMethod]
        public void TotalBet_resolves_to_a_strategy()
        {
            Assert.IsNotNull(MultiplierBaseStrategyResolver.Resolve("TotalBet", null));
        }

        [TestMethod]
        public void LineBetWithStaticMult_with_a_zero_multiplier_resolves_to_null()
        {
            var attrs = new Dictionary<string, string> { { "numLines", "20" }, { "staticBetMultiplier", "0" } };

            Assert.IsNull(MultiplierBaseStrategyResolver.Resolve("LineBetWithStaticMult", attrs));
        }
    }
}
