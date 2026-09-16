using System.Collections.Generic;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.StrategiesTests
{
    // Fills the remaining resolver branches not covered by MultiplierBaseStrategyResolverTests: the valid
    // LineBetFromStaticMultiplier and TotalScatterWin paths, and the partial/invalid LineBet attribute cases.
    [TestClass]
    public class MultiplierBaseStrategyResolverExtraTests
    {
        [TestMethod]
        public void LineBetFromStaticMultiplier_with_valid_constants_resolves_to_a_strategy()
        {
            var attrs = new Dictionary<string, string> { { "numLines", "20" }, { "staticBetMultiplier", "40" } };

            Assert.IsNotNull(MultiplierBaseStrategyResolver.Resolve("LineBetFromStaticMultiplier", attrs));
        }

        [TestMethod]
        public void LineBetFromStaticMultiplier_missing_numLines_resolves_to_null()
        {
            var attrs = new Dictionary<string, string> { { "staticBetMultiplier", "40" } };

            Assert.IsNull(MultiplierBaseStrategyResolver.Resolve("LineBetFromStaticMultiplier", attrs));
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
        public void LineBetTotal_with_only_the_numerator_resolves_to_null()
        {
            var attrs = new Dictionary<string, string> { { "ratioNumerator", "1" } };   // denominator missing

            Assert.IsNull(MultiplierBaseStrategyResolver.Resolve("LineBetTotal", attrs));
        }

        [TestMethod]
        public void LineBetTotal_with_a_non_integer_ratio_resolves_to_null()
        {
            var attrs = new Dictionary<string, string> { { "ratioNumerator", "half" }, { "ratioDenominator", "3" } };

            Assert.IsNull(MultiplierBaseStrategyResolver.Resolve("LineBetTotal", attrs));
        }
    }
}
