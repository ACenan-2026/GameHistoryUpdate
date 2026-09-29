using System;
using System.Collections.Generic;
using System.IO;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.StrategiesTests
{
    // Coverage for the game-level POST_WIN_DIVIDER (<GameHistoryConfig postWinDivider="N">).
    // The engine divides every win by N, so strategies that COMPUTE an amount divide by it (last, rounded once to
    // cents); strategies that READ a recorded win (TotalScatterWin) or carry a money figure (FixedAmount) do not.
    [TestClass]
    public class PostWinDividerTests
    {
        private static MultiplierParams Params(string strategy, int? multiplier, int postWinDivider) =>
            new MultiplierParams(multiplier, paid: true,
                strategy: new StrategySpec(strategy, new Dictionary<string, string>()),
                postWinDivider: postWinDivider);

        // ----- TotalBet (the ways-game strategy) ----------------------------------------------------

        [TestMethod]
        public void TotalBet_divides_by_the_divider_RocketFrenzy_style()
        {
            // RocketFrenzy: N = 80, B012 weight 12 → 12/80 = 0.15 × total bet. Bet $1.60 → $0.24.
            var reader = new FakeRoundReader { TotalBet = 1.60m };

            Assert.AreEqual(0.24m, TotalBetStrategy.Instance.GetWonAmount(reader, Params("TotalBet", 12, 80)).Value);
        }

        [TestMethod]
        public void TotalBet_divides_by_the_divider_TreasureSpirits_style()
        {
            // TreasureSpiritsDragon: N = 60, B01 weight 60 → 1 × total bet. Same result as the hand-authored value="1".
            var reader = new FakeRoundReader { TotalBet = 3.00m };

            Assert.AreEqual(3.00m, TotalBetStrategy.Instance.GetWonAmount(reader, Params("TotalBet", 60, 60)).Value);
        }

        [TestMethod]
        public void TotalBet_rounds_the_final_amount_to_cents()
        {
            // 1.00 × 1 / 3 = 0.333… → 0.33 (a single rounding, after the division).
            var reader = new FakeRoundReader { TotalBet = 1.00m };

            Assert.AreEqual(0.33m, TotalBetStrategy.Instance.GetWonAmount(reader, Params("TotalBet", 1, 3)).Value);
        }

        [TestMethod]
        public void TotalBet_is_unchanged_with_the_default_divider()
        {
            var reader = new FakeRoundReader { TotalBet = 2.50m };

            Assert.AreEqual(25.00m, TotalBetStrategy.Instance.GetWonAmount(reader, Params("TotalBet", 10, 1)).Value);
        }

        [TestMethod]
        public void TotalBet_still_returns_null_for_a_missing_value_or_bet()
        {
            Assert.IsNull(TotalBetStrategy.Instance.GetWonAmount(new FakeRoundReader { TotalBet = 1.60m }, Params("TotalBet", null, 80)));
            Assert.IsNull(TotalBetStrategy.Instance.GetWonAmount(new FakeRoundReader { TotalBet = null }, Params("TotalBet", 12, 80)));
        }

        // ----- line-bet strategies -------------------------------------------------------------------

        [TestMethod]
        public void LineBetStaticMultNoLines_is_unchanged_with_the_default_divider()
        {
            // (3.00 / 30) × 200 = 20.00 — identical to the pre-divider behaviour.
            var reader = new FakeRoundReader { TotalBet = 3.00m };
            var strategy = new LineBetStaticMultNoLinesStrategy(30m);

            Assert.AreEqual(20.00m, strategy.GetWonAmount(reader, Params("LineBetStaticMultNoLines", 200, 1)).Value);
        }

        [TestMethod]
        public void LineBetStaticMultNoLines_divides_by_the_divider()
        {
            // (3.00 / 30) × 200 / 4 = 5.00
            var reader = new FakeRoundReader { TotalBet = 3.00m };
            var strategy = new LineBetStaticMultNoLinesStrategy(30m);

            Assert.AreEqual(5.00m, strategy.GetWonAmount(reader, Params("LineBetStaticMultNoLines", 200, 4)).Value);
        }

        [TestMethod]
        public void LineBetWithStaticMult_divides_by_the_divider()
        {
            // (3.00 × 20 / 30) × 10 / 4 = 5.00
            var reader = new FakeRoundReader { TotalBet = 3.00m };
            var strategy = new LineBetWithStaticMultStrategy(20m, 30m);

            Assert.AreEqual(5.00m, strategy.GetWonAmount(reader, Params("LineBetWithStaticMult", 10, 4)).Value);
        }

        // ----- strategies that must IGNORE the divider -----------------------------------------------

        [TestMethod]
        public void TotalScatterWin_ignores_the_divider()
        {
            // The recorded win was actually paid, so it is already post-divider; dividing again would be wrong.
            var reader = new FakeRoundReader { ScatterWinsTotal = 12.00m };

            Assert.AreEqual(12.00m, TotalScatterWinStrategy.Instance.GetWonAmount(reader, Params("TotalScatterWin", 1, 80)).Value);
        }

        [TestMethod]
        public void FixedAmount_ignores_the_divider()
        {
            // The value is a paytable money figure, not a divider-scaled credit weight.
            var reader = new FakeRoundReader { TotalBet = 1.60m };

            Assert.AreEqual(40m, FixedAmountStrategy.Instance.GetWonAmount(reader, Params("FixedAmount", 40, 80)).Value);
        }

        // ----- MultiplierParams guard ----------------------------------------------------------------

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(-5)]
        public void MultiplierParams_clamps_a_non_positive_divider_to_one(int divider)
        {
            Assert.AreEqual(1, Params("TotalBet", 1, divider).PostWinDivider);
        }

        [TestMethod]
        public void MultiplierParams_defaults_the_divider_to_one()
        {
            var p = new MultiplierParams(1, paid: true, strategy: new StrategySpec("TotalBet", new Dictionary<string, string>()));

            Assert.AreEqual(1, p.PostWinDivider);
        }
    }

    // Parser side: reading postWinDivider off <GameHistoryConfig> and stamping it onto every symbol.
    [TestClass]
    public class PostWinDividerParserTests
    {
        private readonly List<string> _tempFiles = new List<string>();

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var f in _tempFiles)
            {
                try { File.Delete(f); } catch { /* best effort */ }
            }
        }

        private MultiplierSymbolMapping Parse(string gameHistoryConfigAttributes)
        {
            var path = Path.Combine(Path.GetTempPath(), "cfg_" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path,
                "<AgtReelConfig><GameHistoryConfig gameName=\"G\"" + gameHistoryConfigAttributes + "><multiplierGroups>" +
                "<group name=\"g1\" strategy=\"TotalBet\" paid=\"true\"><symbol name=\"B012\" value=\"12\" /></group>" +
                "<group name=\"g2\" strategy=\"TotalScatterWin\" paid=\"true\"><symbol name=\"Wh\" /></group>" +
                "</multiplierGroups></GameHistoryConfig></AgtReelConfig>");
            _tempFiles.Add(path);
            return new MultiplierConfigParser(path).GetMultiplierParams();
        }

        [TestMethod]
        public void Absent_divider_defaults_to_one()
        {
            var mapping = Parse("");

            Assert.IsTrue(mapping.TryGet("B012", out var p));
            Assert.AreEqual(1, p.PostWinDivider);
        }

        [TestMethod]
        public void Valid_divider_is_applied_to_every_group()
        {
            var mapping = Parse(" postWinDivider=\"80\"");

            Assert.IsTrue(mapping.TryGet("B012", out var b));
            Assert.IsTrue(mapping.TryGet("Wh", out var wh));
            Assert.AreEqual(80, b.PostWinDivider);
            Assert.AreEqual(80, wh.PostWinDivider); // carried, but TotalScatterWin ignores it
        }

        [TestMethod]
        public void Divider_tolerates_surrounding_whitespace()
        {
            var mapping = Parse(" postWinDivider=\" 60 \"");

            Assert.IsTrue(mapping.TryGet("B012", out var p));
            Assert.AreEqual(60, p.PostWinDivider);
        }

        [DataTestMethod]
        [DataRow("0")]      // would divide by zero
        [DataRow("-5")]
        [DataRow("abc")]
        [DataRow("1.5")]    // must be an integer
        [DataRow("")]
        public void Invalid_divider_defaults_to_one(string raw)
        {
            var mapping = Parse(" postWinDivider=\"" + raw + "\"");

            Assert.IsTrue(mapping.TryGet("B012", out var p));
            Assert.AreEqual(1, p.PostWinDivider);
        }

        [TestMethod]
        public void End_to_end_ways_game_amount()
        {
            // RocketFrenzy-style config: raw weight + game divider → (1.60 × 12) / 80 = 0.24.
            var mapping = Parse(" postWinDivider=\"80\"");
            Assert.IsTrue(mapping.TryGet("B012", out var p));

            var amount = TotalBetStrategy.Instance.GetWonAmount(new FakeRoundReader { TotalBet = 1.60m }, p);

            Assert.AreEqual(0.24m, amount.Value);
        }
    }
}
