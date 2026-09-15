using System.Collections.Generic;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.StrategiesTests
{
    [TestClass]
    public class TotalScatterWinStrategyTests
    {
        private static MultiplierParams Params(int? multiplier) =>
            new MultiplierParams(multiplier, paid: true, strategy: new StrategySpec("TotalScatterWin", new Dictionary<string, string>()));

        [TestMethod]
        public void Returns_the_recorded_scatter_win_total_when_positive()
        {
            var reader = new FakeRoundReader { ScatterWinsTotal = 200m };

            Assert.AreEqual(200m, TotalScatterWinStrategy.Instance.GetWonAmount(reader, Params(1)).Value);
        }

        [TestMethod]
        public void Returns_null_when_no_scatter_win_was_recorded()
        {
            // A spin can show the multiplier symbol without the wheel/jackpot feature paying; the total is then 0
            // and the tile must render plain rather than showing an unrelated amount.
            var reader = new FakeRoundReader { ScatterWinsTotal = 0m };

            Assert.IsNull(TotalScatterWinStrategy.Instance.GetWonAmount(reader, Params(1)));
        }

        [TestMethod]
        public void Passes_the_recorded_total_through_unchanged_ignoring_the_symbol_value()
        {
            // 'value' is documentation-only for TotalScatterWin: the amount is the recorded scatter total, never
            // total × value. A value of 5 must not scale 150 up to 750.
            var reader = new FakeRoundReader { ScatterWinsTotal = 150m };

            Assert.AreEqual(150m, TotalScatterWinStrategy.Instance.GetWonAmount(reader, Params(5)).Value);
        }

        [TestMethod]
        public void Works_even_when_the_symbol_value_is_missing()
        {
            // Because the strategy ignores 'value', a null Multiplier is harmless here (unlike the base×value
            // strategies, where a missing value yields null).
            var reader = new FakeRoundReader { ScatterWinsTotal = 80m };

            Assert.AreEqual(80m, TotalScatterWinStrategy.Instance.GetWonAmount(reader, Params(null)).Value);
        }

        [TestMethod]
        public void Preserves_decimal_amounts()
        {
            var reader = new FakeRoundReader { ScatterWinsTotal = 12.50m };

            Assert.AreEqual(12.50m, TotalScatterWinStrategy.Instance.GetWonAmount(reader, Params(1)).Value);
        }
    }
}
