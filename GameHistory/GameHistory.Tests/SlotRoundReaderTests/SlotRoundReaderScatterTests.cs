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

        private static List<decimal> Amounts(List<RecordedScatterWin> wins) => wins.Select(w => w.Amount).ToList();

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

            CollectionAssert.AreEqual(new List<decimal> { 200m }, Amounts(wins));
        }

        [TestMethod]
        public void OneSpin_reads_multiple_entries_split_on_br_in_order()
        {
            var wins = NullReader().GetOneSpinScatterWins(
                "Type: Basic_Scatter, WinAmount: 20<br/>Type: Basic_Scatter, WinAmount: 200");

            CollectionAssert.AreEqual(new List<decimal> { 20m, 200m }, Amounts(wins));
        }

        [TestMethod]
        public void OneSpin_ignores_payline_entries_and_keeps_only_scatter_wins()
        {
            var wins = NullReader().GetOneSpinScatterWins(
                "Type: Basic_Scatter, WinAmount: 20<br/>Type: Payline, WinAmount: 5<br/>Type: Basic_Scatter, WinAmount: 200");

            CollectionAssert.AreEqual(new List<decimal> { 20m, 200m }, Amounts(wins));
        }

        [TestMethod]
        public void OneSpin_drops_zero_amount_scatter_markers()
        {
            // A recorded located scatter that did not pay (WinAmount 0) is a non-paying marker, not a win.
            var wins = NullReader().GetOneSpinScatterWins(
                "Type: Basic_Scatter, WinAmount: 0<br/>Type: Basic_Scatter, WinAmount: 50");

            CollectionAssert.AreEqual(new List<decimal> { 50m }, Amounts(wins));
        }

        [TestMethod]
        public void OneSpin_parses_decimal_amounts()
        {
            var wins = NullReader().GetOneSpinScatterWins("Type: Basic_Scatter, WinAmount: 12.50");

            CollectionAssert.AreEqual(new List<decimal> { 12.50m }, Amounts(wins));
        }

        [TestMethod]
        public void OneSpin_is_case_insensitive_for_keys_and_the_scatter_type()
        {
            var wins = NullReader().GetOneSpinScatterWins("type: basic_scatter, winamount: 50");

            CollectionAssert.AreEqual(new List<decimal> { 50m }, Amounts(wins));
        }

        [TestMethod]
        public void OneSpin_ignores_unrelated_fields_within_an_entry()
        {
            var wins = NullReader().GetOneSpinScatterWins("Type: Basic_Scatter, Position: N/A, WinAmount: 200");

            CollectionAssert.AreEqual(new List<decimal> { 200m }, Amounts(wins));
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

            CollectionAssert.AreEqual(new List<decimal> { 200m }, Amounts(wins));
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
            CollectionAssert.AreEqual(new List<decimal> { 20m }, Amounts(result[0]));
            Assert.AreEqual(0, result[1].Count);
            CollectionAssert.AreEqual(new List<decimal> { 200m, 20m }, Amounts(result[2]));
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
            // The validator's reconcile consumes the entries it is handed (SharesLeft); each call must therefore hand
            // out a fresh deep copy so a later caller still sees the full, parsed-once data.
            var reader = ReaderWithSpins("Type: Basic_Scatter, WinAmount: 20");

            var first = reader.GetScatterWins();
            first[0].Add(new RecordedScatterWin(999m));   // mutate the returned list
            first[0][0].SharesLeft = 0;                   // ...and consume the returned entry

            var second = reader.GetScatterWins();

            CollectionAssert.AreEqual(new List<decimal> { 20m }, Amounts(second[0]));
            Assert.IsTrue(second[0][0].IsUntouched, "consuming a returned entry must not leak into the cache");
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

            decimal fromPerSpin = reader.GetScatterWins().SelectMany(spin => spin).Sum(w => w.Amount);

            Assert.AreEqual(fromPerSpin, reader.GetScatterWinsTotal());
        }

        // ----- NumSymbols: symbols counted toward each recorded win ---------------------------------

        [TestMethod]
        public void OneSpin_reads_NumSymbols_and_starts_every_entry_unclaimed()
        {
            var wins = NullReader().GetOneSpinScatterWins("Type: Basic_Scatter, WinAmount: 975, NumSymbols: 13");

            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(975m, wins[0].Amount);
            Assert.AreEqual(13, wins[0].NumSymbols);
            Assert.AreEqual(13, wins[0].SharesLeft);
            Assert.IsTrue(wins[0].IsUntouched);
        }

        [TestMethod]
        public void OneSpin_defaults_NumSymbols_to_1_when_absent()
        {
            var wins = NullReader().GetOneSpinScatterWins("Type: Basic_Scatter, WinAmount: 200");

            Assert.AreEqual(1, wins[0].NumSymbols);
            Assert.AreEqual(1, wins[0].SharesLeft);
        }

        [TestMethod]
        public void OneSpin_defaults_NumSymbols_to_1_when_unparseable_or_non_positive()
        {
            var wins = NullReader().GetOneSpinScatterWins(
                "Type: Basic_Scatter, WinAmount: 10, NumSymbols: abc<br/>" +
                "Type: Basic_Scatter, WinAmount: 20, NumSymbols: 0<br/>" +
                "Type: Basic_Scatter, WinAmount: 30, NumSymbols: -4<br/>" +
                "Type: Basic_Scatter, WinAmount: 40, NumSymbols: ");

            CollectionAssert.AreEqual(new List<int> { 1, 1, 1, 1 }, wins.Select(w => w.NumSymbols).ToList());
            CollectionAssert.AreEqual(new List<int> { 1, 1, 1, 1 }, wins.Select(w => w.SharesLeft).ToList());
        }

        [TestMethod]
        public void OneSpin_parses_a_real_CyberCash_count_scatter_Details_string()
        {
            // Verbatim from a Cyber Cash pull (GameId 538): a 13 x P1 count win (11 P1 + 2 substituting wilds) and
            // a non-paying Myst_Symbol marker whose WinAmount is empty. Only the paying entry is a win.
            var wins = NullReader().GetOneSpinScatterWins(
                "WinComboId: 32,Type: Basic_Scatter,WinAmount: 975,Multiplier: ,MultiplierWin: ,NumSymbols: 13,SymbolId: 6 ,ScatterType: NormalScatter<br/>" +
                "WinComboId: 23,Type: Basic_Scatter,WinAmount: ,Multiplier: ,MultiplierWin: ,NumSymbols: 1,SymbolId: 30 ,ScatterType: NormalScatter<br/>");

            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(975m, wins[0].Amount);
            Assert.AreEqual(13, wins[0].NumSymbols);
        }

        [TestMethod]
        public void OneSpin_still_drops_zero_amount_markers_that_carry_NumSymbols()
        {
            var wins = NullReader().GetOneSpinScatterWins(
                "Type: Basic_Scatter, WinAmount: 0, NumSymbols: 3<br/>Type: Basic_Scatter, WinAmount: 50, NumSymbols: 1");

            CollectionAssert.AreEqual(new List<decimal> { 50m }, Amounts(wins));
        }

        [TestMethod]
        public void GetScatterWinsTotal_sums_amounts_regardless_of_NumSymbols()
        {
            // TotalScatterWin reads the total; NumSymbols must not scale it (975 is the whole win, not per-symbol).
            var reader = ReaderWithSpins("Type: Basic_Scatter, WinAmount: 975, NumSymbols: 13");

            Assert.AreEqual(975m, reader.GetScatterWinsTotal());
        }

        // ----- RecordedScatterWinPool.TryClaimWhole: the whole-amount claim rule ---------------------

        private static List<RecordedScatterWin> Pool(params (decimal amount, int n)[] wins) =>
            wins.Select(w => new RecordedScatterWin(w.amount, w.n)).ToList();

        [TestMethod]
        public void TryClaimWhole_consumes_the_first_untouched_entry_with_an_equal_amount()
        {
            var pool = Pool((20m, 1), (200m, 1), (20m, 1));

            Assert.IsTrue(pool.TryClaimWhole(20m));

            Assert.AreEqual(0, pool[0].SharesLeft);     // first 20 consumed
            Assert.IsTrue(pool[2].IsUntouched);         // duplicate left for a later claim
            Assert.IsTrue(pool.TryClaimWhole(20m));
            Assert.IsFalse(pool.TryClaimWhole(20m));    // both 20s gone
        }

        [TestMethod]
        public void TryClaimWhole_returns_false_when_no_amount_matches()
        {
            var pool = Pool((200m, 1));

            Assert.IsFalse(pool.TryClaimWhole(20m));
            Assert.IsTrue(pool[0].IsUntouched);
        }

        [TestMethod]
        public void TryClaimWhole_never_takes_a_count_win_neither_a_share_nor_the_whole_amount()
        {
            // A whole claim never takes a per-symbol share (75 of (975, 13) is TryClaimShared's job), and since the
            // entry is counted over 13 symbols it cannot be taken whole either — even by a tile worth exactly 975.
            var pool = Pool((975m, 13));

            Assert.IsFalse(pool.TryClaimWhole(75m));
            Assert.IsFalse(pool.TryClaimWhole(975m));
            Assert.IsTrue(pool[0].IsUntouched);
        }

        [TestMethod]
        public void TryClaimWhole_keeps_a_count_win_for_its_shared_tiles()
        {
            // The collision the NumSymbols rule closes: two 50 shared tiles paid (100, 2); a 100 whole tile processed
            // first must not swallow it, so both shares remain for the 50s.
            var pool = Pool((100m, 2));

            Assert.IsFalse(pool.TryClaimWhole(100m));
            Assert.IsTrue(pool.TryClaimShared(50m));
            Assert.IsTrue(pool.TryClaimShared(50m));
        }

        [TestMethod]
        public void TryClaimWhole_takes_the_single_symbol_entry_when_both_kinds_share_an_amount()
        {
            // (100, 1) and (100, 2) in one spin: the whole tile takes the single-symbol entry, whatever the order.
            var pool = Pool((100m, 2), (100m, 1));

            Assert.IsTrue(pool.TryClaimWhole(100m));
            Assert.IsTrue(pool[0].IsUntouched);
            Assert.AreEqual(0, pool[1].SharesLeft);
        }

        [TestMethod]
        public void TryClaimWhole_is_false_for_a_null_pool()
        {
            List<RecordedScatterWin> pool = null;
            Assert.IsFalse(pool.TryClaimWhole(20m));
        }

        // ----- RecordedScatterWinPool.TryClaimShared: the per-symbol share claim rule ---------------

        [TestMethod]
        public void TryClaimShared_gives_out_exactly_NumSymbols_shares_then_runs_out()
        {
            // Cyber Cash: 13 x P1 recorded as one (975, 13) entry; each 75 tile takes one share.
            var pool = Pool((975m, 13));

            for (int i = 0; i < 13; i++)
            {
                Assert.IsTrue(pool.TryClaimShared(75m), "share " + (i + 1) + " should be claimable");
            }

            Assert.AreEqual(0, pool[0].SharesLeft);
            Assert.IsFalse(pool.TryClaimShared(75m));   // a 14th tile finds nothing left
        }

        [TestMethod]
        public void TryClaimShared_rejects_a_value_that_is_not_an_exact_share()
        {
            var pool = Pool((975m, 13));

            Assert.IsFalse(pool.TryClaimShared(76m));
            Assert.IsFalse(pool.TryClaimShared(975m));  // the whole amount is not a share when N > 1
            Assert.IsTrue(pool[0].IsUntouched);
        }

        [TestMethod]
        public void TryClaimShared_matches_decimal_shares_exactly()
        {
            // 13 x R5 at 0.15 = 1.95: compared by multiplication, so no rounding is involved.
            var pool = Pool((1.95m, 13));

            Assert.IsTrue(pool.TryClaimShared(0.15m));
            Assert.AreEqual(12, pool[0].SharesLeft);
        }

        [TestMethod]
        public void TryClaimShared_on_a_single_symbol_entry_takes_the_whole_amount()
        {
            // NumSymbols = 1: a share IS the whole amount, so the entry is consumed in one claim.
            var pool = Pool((20m, 1));

            Assert.IsTrue(pool.TryClaimShared(20m));
            Assert.AreEqual(0, pool[0].SharesLeft);
            Assert.IsFalse(pool.TryClaimShared(20m));
        }

        [TestMethod]
        public void TryClaimShared_routes_each_value_to_the_entry_it_divides()
        {
            // Two count wins in one spin: 5 x P2 (150, 5 -> 30 each) and 6 x R1 (18, 6 -> 3 each).
            var pool = Pool((150m, 5), (18m, 6));

            Assert.IsTrue(pool.TryClaimShared(3m));
            Assert.IsTrue(pool.TryClaimShared(30m));

            Assert.AreEqual(4, pool[0].SharesLeft);
            Assert.AreEqual(5, pool[1].SharesLeft);
        }

        [TestMethod]
        public void TryClaimShared_cannot_take_from_an_entry_already_claimed_whole()
        {
            var pool = Pool((20m, 1));
            Assert.IsTrue(pool.TryClaimWhole(20m));

            Assert.IsFalse(pool.TryClaimShared(20m));
        }

        [TestMethod]
        public void TryClaimWhole_cannot_take_an_entry_once_a_share_has_been_claimed()
        {
            // A partly-shared entry is no longer untouched, so a whole claim can't take it. Shown on a single-symbol
            // entry, where the NumSymbols rule alone would allow the whole claim: (20, 1) shared once is consumed.
            var pool = Pool((20m, 1));
            Assert.IsTrue(pool.TryClaimShared(20m));

            Assert.IsFalse(pool.TryClaimWhole(20m));
        }

        [TestMethod]
        public void TryClaimShared_is_false_for_a_null_pool()
        {
            List<RecordedScatterWin> pool = null;
            Assert.IsFalse(pool.TryClaimShared(20m));
        }

        // ----- RecordedScatterWinPool.TryClaim: routing by the group's claim mode ------------------

        private static MultiplierParams WithClaims(MultiplierClaims claims) =>
            new MultiplierParams(1, paid: true, strategy: new StrategySpec("TotalBet", new Dictionary<string, string>()),
                claims: claims);

        [TestMethod]
        public void TryClaim_with_shared_claims_a_per_symbol_share()
        {
            var pool = Pool((975m, 13));

            Assert.IsTrue(pool.TryClaim(WithClaims(MultiplierClaims.Shared), 75m));
            Assert.AreEqual(12, pool[0].SharesLeft);
        }

        [TestMethod]
        public void TryClaim_with_whole_does_not_claim_a_share()
        {
            var pool = Pool((975m, 13));

            Assert.IsFalse(pool.TryClaim(WithClaims(MultiplierClaims.Whole), 75m));
            Assert.IsTrue(pool[0].IsUntouched);
        }

        [TestMethod]
        public void TryClaim_with_whole_claims_the_full_amount_of_a_single_symbol_entry()
        {
            var pool = Pool((975m, 1));

            Assert.IsTrue(pool.TryClaim(WithClaims(MultiplierClaims.Whole), 975m));
            Assert.AreEqual(0, pool[0].SharesLeft);
        }

        [TestMethod]
        public void TryClaim_with_null_params_falls_back_to_a_whole_claim()
        {
            var pool = Pool((975m, 13), (975m, 1));

            Assert.IsFalse(pool.TryClaim(null, 75m));     // not a share claim...
            Assert.IsTrue(pool.TryClaim(null, 975m));     // ...a whole claim, which takes the single-symbol entry
            Assert.IsTrue(pool[0].IsUntouched);
        }

        // ----- TryClaim: the TotalScatterWin exemption --------------------------------------------

        private static MultiplierParams TotalScatterWin() =>
            new MultiplierParams(1, paid: true,
                strategy: new StrategySpec("TotalScatterWin", new Dictionary<string, string>()),
                placement: MultiplierOverlayPlacement.OnceOnLastOccurrence);

        [TestMethod]
        public void TryClaim_TotalScatterWin_consumes_every_entry_and_succeeds_without_amount_matching()
        {
            // A wheel record over 3 trigger symbols plus another entry: under the sole-group contract every scatter
            // win in the spin is TotalScatterWin's, whatever its amount or NumSymbols.
            var pool = Pool((300m, 3), (50m, 1));

            Assert.IsTrue(pool.TryClaim(TotalScatterWin(), 999m));   // amount is not compared
            Assert.IsTrue(pool.All(w => w.SharesLeft == 0));
        }

        [TestMethod]
        public void TryClaim_TotalScatterWin_fails_on_a_spin_that_recorded_nothing()
        {
            // Computed carries the round total, so the tile appears on every spin; an empty spin must stay unpaid.
            Assert.IsFalse(new List<RecordedScatterWin>().TryClaim(TotalScatterWin(), 300m));
        }

        [TestMethod]
        public void TryClaim_TotalScatterWin_fails_on_a_null_pool_without_throwing()
        {
            List<RecordedScatterWin> pool = null;
            Assert.IsFalse(pool.TryClaim(TotalScatterWin(), 300m));
        }

        [TestMethod]
        public void TryClaim_other_strategies_still_match_by_amount()
        {
            // Only the strategy name triggers the exemption: a TotalBet once-group still claims by amount.
            var onceTotalBet = new MultiplierParams(1, paid: true,
                strategy: new StrategySpec("TotalBet", new Dictionary<string, string>()),
                placement: MultiplierOverlayPlacement.OnceOnLastOccurrence);
            var pool = Pool((300m, 3), (50m, 1));

            Assert.IsFalse(pool.TryClaim(onceTotalBet, 999m));
            Assert.IsTrue(pool.All(w => w.IsUntouched));
        }

        [TestMethod]
        public void TryClaimWhole_rejects_an_entry_counted_over_several_symbols()
        {
            // Whole claims require NumSymbols == 1 (a WARN is logged on this rejection). Only TotalScatterWin, via
            // TryClaim, may take such an entry.
            var pool = Pool((300m, 3));

            Assert.IsFalse(pool.TryClaimWhole(300m));
            Assert.IsTrue(pool[0].IsUntouched);
            Assert.IsTrue(pool.TryClaim(TotalScatterWin(), 300m));
        }
    }
}
