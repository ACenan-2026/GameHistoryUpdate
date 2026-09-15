using System.Collections.Generic;
using System.Linq;
using GameHistory.Models;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.SlotRoundReaderTests
{
    [TestClass]
    public class SlotRoundReaderScatterTests
    {
        // ----- helpers -------------------------------------------------------------------------------

        // The Details parse (GetOneSpinScatterWins) reads only its string argument, so a reader over a null
        // round is a valid host for it — no model graph needed.
        private static SlotRoundReader NullReader() => new SlotRoundReader(null);

        // Builds the minimal DTO graph GetScatterWins()/GetScatterWinsTotal() walk: one detail entry per spin,
        // each carrying its raw Details string. These are plain DTOs with public setters — no mocking required.
        private static SlotRoundReader ReaderWithSpins(params string[] spinDetails)
        {
            var details = spinDetails
                .Select(d => new GameHistorySlotPositionDetailModel { Details = d })
                .ToList();

            var gameInfo = new GameHistoryGameInfoModel
            {
                UserPositions = new GameHistoryUserPositionsModel
                {
                    SlotUsersPositionsAndDetails = new GameHistorySlotUserPositionsAndDetailsModel
                    {
                        SlotDetails = new GameHistorySlotResultDetailModel { SlotDetails = details }
                    }
                }
            };
            return new SlotRoundReader(gameInfo);
        }

        // ----- GetOneSpinScatterWins: the <br/>-delimited Details parse -------------------------------

        [TestMethod]
        public void OneSpin_reads_a_single_scatter_win()
        {
            var wins = NullReader().GetOneSpinScatterWins("Type: Basic_Scatter, WinAmount: 200");

            CollectionAssert.AreEqual(new List<decimal> { 200m }, wins);
        }

        [TestMethod]
        public void OneSpin_reads_multiple_entries_split_on_br_in_order()
        {
            var wins = NullReader().GetOneSpinScatterWins(
                "Type: Basic_Scatter, WinAmount: 20<br/>Type: Basic_Scatter, WinAmount: 200");

            CollectionAssert.AreEqual(new List<decimal> { 20m, 200m }, wins);
        }

        [TestMethod]
        public void OneSpin_ignores_payline_entries_and_keeps_only_scatter_wins()
        {
            var wins = NullReader().GetOneSpinScatterWins(
                "Type: Basic_Scatter, WinAmount: 20<br/>Type: Payline, WinAmount: 5<br/>Type: Basic_Scatter, WinAmount: 200");

            CollectionAssert.AreEqual(new List<decimal> { 20m, 200m }, wins);
        }

        [TestMethod]
        public void OneSpin_drops_zero_amount_scatter_markers()
        {
            // A recorded located scatter that did not pay (WinAmount 0) is a non-paying marker, not a win.
            var wins = NullReader().GetOneSpinScatterWins(
                "Type: Basic_Scatter, WinAmount: 0<br/>Type: Basic_Scatter, WinAmount: 50");

            CollectionAssert.AreEqual(new List<decimal> { 50m }, wins);
        }

        [TestMethod]
        public void OneSpin_parses_decimal_amounts()
        {
            var wins = NullReader().GetOneSpinScatterWins("Type: Basic_Scatter, WinAmount: 12.50");

            CollectionAssert.AreEqual(new List<decimal> { 12.50m }, wins);
        }

        [TestMethod]
        public void OneSpin_is_case_insensitive_for_keys_and_the_scatter_type()
        {
            var wins = NullReader().GetOneSpinScatterWins("type: basic_scatter, winamount: 50");

            CollectionAssert.AreEqual(new List<decimal> { 50m }, wins);
        }

        [TestMethod]
        public void OneSpin_ignores_unrelated_fields_within_an_entry()
        {
            var wins = NullReader().GetOneSpinScatterWins("Type: Basic_Scatter, Position: N/A, WinAmount: 200");

            CollectionAssert.AreEqual(new List<decimal> { 200m }, wins);
        }

        [TestMethod]
        public void OneSpin_ignores_non_win_entries()
        {
            var wins = NullReader().GetOneSpinScatterWins("GambleOutcome=3<br/>SelectedGamble=2");

            Assert.AreEqual(0, wins.Count);
        }

        [TestMethod]
        public void OneSpin_returns_empty_for_null_or_empty_details()
        {
            Assert.AreEqual(0, NullReader().GetOneSpinScatterWins(null).Count);
            Assert.AreEqual(0, NullReader().GetOneSpinScatterWins("").Count);
        }

        [TestMethod]
        public void OneSpin_tolerates_a_trailing_br_delimiter()
        {
            var wins = NullReader().GetOneSpinScatterWins("Type: Basic_Scatter, WinAmount: 200<br/>");

            CollectionAssert.AreEqual(new List<decimal> { 200m }, wins);
        }

        // ----- GetScatterWins: per-spin grouping over the round --------------------------------------

        [TestMethod]
        public void GetScatterWins_groups_wins_per_spin_with_an_empty_list_for_a_scatterless_spin()
        {
            var reader = ReaderWithSpins(
                "Type: Basic_Scatter, WinAmount: 20",                                                   // spin 0
                "Type: Payline, WinAmount: 5",                                                          // spin 1: no scatter
                "Type: Basic_Scatter, WinAmount: 200<br/>Type: Basic_Scatter, WinAmount: 20");          // spin 2

            var result = reader.GetScatterWins();

            Assert.AreEqual(3, result.Count);
            CollectionAssert.AreEqual(new List<decimal> { 20m }, result[0]);
            Assert.AreEqual(0, result[1].Count);
            CollectionAssert.AreEqual(new List<decimal> { 200m, 20m }, result[2]);
        }

        [TestMethod]
        public void GetScatterWins_returns_an_empty_list_when_there_is_no_round_data()
        {
            var reader = new SlotRoundReader(null);

            Assert.AreEqual(0, reader.GetScatterWins().Count);
        }

        [TestMethod]
        public void GetScatterWins_returns_independent_copies_that_callers_cannot_use_to_corrupt_the_cache()
        {
            // The validator's reconcile mutates the lists it is handed (RemoveAt); each call must therefore hand
            // out a fresh copy so a later caller still sees the full, parsed-once data.
            var reader = ReaderWithSpins("Type: Basic_Scatter, WinAmount: 20");

            var first = reader.GetScatterWins();
            first[0].Add(999m);            // mutate the returned copy

            var second = reader.GetScatterWins();

            CollectionAssert.AreEqual(new List<decimal> { 20m }, second[0]);
        }

        // ----- GetScatterWinsTotal ------------------------------------------------------------------

        [TestMethod]
        public void GetScatterWinsTotal_sums_every_scatter_win_across_the_round()
        {
            var reader = ReaderWithSpins(
                "Type: Basic_Scatter, WinAmount: 20",
                "Type: Payline, WinAmount: 5",
                "Type: Basic_Scatter, WinAmount: 200<br/>Type: Basic_Scatter, WinAmount: 20");

            Assert.AreEqual(240m, reader.GetScatterWinsTotal());   // 20 + 200 + 20 (payline ignored)
        }

        [TestMethod]
        public void GetScatterWinsTotal_is_zero_when_there_is_no_round_data()
        {
            Assert.AreEqual(0m, new SlotRoundReader(null).GetScatterWinsTotal());
        }

        [TestMethod]
        public void GetScatterWinsTotal_sums_decimal_amounts_without_precision_loss()
        {
            var reader = ReaderWithSpins(
                "Type: Basic_Scatter, WinAmount: 12.50",
                "Type: Basic_Scatter, WinAmount: 7.25",
                "Type: Basic_Scatter, WinAmount: 200");

            Assert.AreEqual(219.75m, reader.GetScatterWinsTotal());
        }

        [TestMethod]
        public void GetScatterWinsTotal_excludes_zero_amount_scatter_markers()
        {
            var reader = ReaderWithSpins(
                "Type: Basic_Scatter, WinAmount: 0<br/>Type: Basic_Scatter, WinAmount: 50",
                "Type: Basic_Scatter, WinAmount: 0");

            Assert.AreEqual(50m, reader.GetScatterWinsTotal());
        }

        [TestMethod]
        public void GetScatterWinsTotal_is_zero_when_the_round_has_spins_but_no_scatter_wins()
        {
            // Distinct from the null-round case: here the round exists and has spins, but every recorded win is a
            // payline, so nothing counts toward the scatter total.
            var reader = ReaderWithSpins(
                "Type: Payline, WinAmount: 5",
                "Type: Payline, WinAmount: 10");

            Assert.AreEqual(0m, reader.GetScatterWinsTotal());
        }

        [TestMethod]
        public void GetScatterWinsTotal_counts_every_scatter_within_a_single_spin()
        {
            var reader = ReaderWithSpins(
                "Type: Basic_Scatter, WinAmount: 20<br/>Type: Basic_Scatter, WinAmount: 200<br/>Type: Basic_Scatter, WinAmount: 20");

            Assert.AreEqual(240m, reader.GetScatterWinsTotal());
        }

        [TestMethod]
        public void GetScatterWinsTotal_counts_every_scatter_within_a_single_spin_extra_br()
        {
            var reader = ReaderWithSpins(
                "Type: Basic_Scatter, WinAmount: 20<br/>Type: Basic_Scatter, WinAmount: 200<br/>Type: Basic_Scatter, WinAmount: 20<br/>");

            Assert.AreEqual(240m, reader.GetScatterWinsTotal());
        }

        [TestMethod]
        public void GetScatterWinsTotal_equals_the_sum_of_the_per_spin_scatter_wins()
        {
            // The total must agree with the per-spin breakdown it aggregates — both read the same parsed data.
            var reader = ReaderWithSpins(
                "Type: Basic_Scatter, WinAmount: 20",
                "Type: Payline, WinAmount: 5",
                "Type: Basic_Scatter, WinAmount: 200<br/>Type: Basic_Scatter, WinAmount: 12.50");

            decimal fromPerSpin = reader.GetScatterWins().SelectMany(spin => spin).Sum();

            Assert.AreEqual(fromPerSpin, reader.GetScatterWinsTotal());
        }
    }
}
