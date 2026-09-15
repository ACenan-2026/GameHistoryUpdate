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
        public void LineBetTotal_without_ratio_attributes_resolves_to_null()
        {
            Assert.IsNull(MultiplierBaseStrategyResolver.Resolve("LineBetTotal", new Dictionary<string, string>()));
        }

        [TestMethod]
        public void LineBetTotal_with_zero_denominator_resolves_to_null()
        {
            var attrs = new Dictionary<string, string> { { "ratioNumerator", "1" }, { "ratioDenominator", "0" } };

            Assert.IsNull(MultiplierBaseStrategyResolver.Resolve("LineBetTotal", attrs));
        }

        [TestMethod]
        public void LineBetTotal_with_a_valid_ratio_resolves_to_a_strategy()
        {
            var attrs = new Dictionary<string, string> { { "ratioNumerator", "1" }, { "ratioDenominator", "3" } };

            Assert.IsNotNull(MultiplierBaseStrategyResolver.Resolve("LineBetTotal", attrs));
        }

        [TestMethod]
        public void LineBetFromStaticMultiplier_with_a_zero_multiplier_resolves_to_null()
        {
            var attrs = new Dictionary<string, string> { { "numLines", "20" }, { "staticBetMultiplier", "0" } };

            Assert.IsNull(MultiplierBaseStrategyResolver.Resolve("LineBetFromStaticMultiplier", attrs));
        }
    }
}
