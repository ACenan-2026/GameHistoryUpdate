using System.Collections.Generic;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.StrategiesTests
{
    // Fills the remaining resolver branches not covered by MultiplierBaseStrategyResolverTests: the valid
    // LineBetWithStaticMult and TotalScatterWin paths, and the partial/invalid LineBetWithStaticMult attribute cases.
    [TestClass]
    public class MultiplierBaseStrategyResolverExtraTests
    {
        [TestMethod]
        public void LineBetWithStaticMult_with_valid_constants_resolves_to_a_strategy()
        {
            var attrs = new Dictionary<string, string> { { "numLines", "20" }, { "staticBetMultiplier", "40" } };

            Assert.IsNotNull(MultiplierBaseStrategyResolver.Resolve("LineBetWithStaticMult", attrs));
        }

        [TestMethod]
        public void LineBetWithStaticMult_missing_numLines_resolves_to_null()
        {
            var attrs = new Dictionary<string, string> { { "staticBetMultiplier", "40" } };

            Assert.IsNull(MultiplierBaseStrategyResolver.Resolve("LineBetWithStaticMult", attrs));
        }

        [TestMethod]
        public void TotalScatterWin_resolves_to_the_shared_strategy_instance()
        {
            var strategy = MultiplierBaseStrategyResolver.Resolve("TotalScatterWin", null);

            Assert.IsNotNull(strategy);
            Assert.AreSame(TotalScatterWinStrategy.Instance, strategy);
        }

        [TestMethod]
        public void TotalBet_resolves_to_the_shared_strategy_instance()
        {
            var strategy = MultiplierBaseStrategyResolver.Resolve("TotalBet", null);

            Assert.AreSame(TotalBetStrategy.Instance, strategy);
        }

        [TestMethod]
        public void LineBetWithStaticMult_with_only_numLines_resolves_to_null()
        {
            var attrs = new Dictionary<string, string> { { "numLines", "20" } };   // staticBetMultiplier missing

            Assert.IsNull(MultiplierBaseStrategyResolver.Resolve("LineBetWithStaticMult", attrs));
        }

        [TestMethod]
        public void LineBetWithStaticMult_with_a_non_integer_constant_resolves_to_null()
        {
            var attrs = new Dictionary<string, string> { { "numLines", "twenty" }, { "staticBetMultiplier", "40" } };

            Assert.IsNull(MultiplierBaseStrategyResolver.Resolve("LineBetWithStaticMult", attrs));
        }
    }
}
