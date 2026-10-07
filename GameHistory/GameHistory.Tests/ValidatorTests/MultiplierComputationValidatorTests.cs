using System.Collections.Generic;
using System.Linq;
using GameHistory.Models;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.ValidatorTests
{
    // Phase 1 validator behaviour. It is log-only in production, but ValidateRound also returns a result object
    // describing the discrepancies it found, which is what these tests assert on (per the agreed depth: behaviour +
    // result object, no log capture). The reconcile is a per-spin multiset match by amount.
    [TestClass]
    public class MultiplierComputationValidatorTests
    {
        // ----- builders ----------------------------------------------------------------------------

        private static SlotSymbolReelViewModel Reel(params string[] floors)
        {
            var reel = new SlotSymbolReelViewModel("r") { Floors = new List<SlotSymbolViewModel>() };
            foreach (var f in floors) reel.Floors.Add(new SlotSymbolViewModel { SymbolName = f });
            return reel;
        }

        private static SlotSymbolTableViewModel Grid(params SlotSymbolReelViewModel[] reels) =>
            new SlotSymbolTableViewModel { Reels = new List<SlotSymbolReelViewModel>(reels) };

        private static MultiplierParams Paid(
            MultiplierOverlayPlacement placement = MultiplierOverlayPlacement.All, string group = "g") =>
            new MultiplierParams(1, paid: true, strategy: new StrategySpec("TotalBet", new Dictionary<string, string>()),
                groupName: group, placement: placement);

        private static MultiplierParams Unpaid(string group = "tb") =>
            new MultiplierParams(1, paid: false, strategy: new StrategySpec("TotalBet", new Dictionary<string, string>()),
                groupName: group);

        // Reader that returns one grid spin, one detail entry (to satisfy the count) and one per-spin recorded pool.
        private static FakeRoundReader ReaderFor(SlotSymbolTableViewModel spin, List<decimal> recorded)
        {
            return new FakeRoundReader
            {
                SlotModel = new GameHistoryGameInfoSlotModel { GameName = "G", Symbols = new[] { spin } },
                SlotDetails = new List<GameHistorySlotPositionDetailModel> { new GameHistorySlotPositionDetailModel() },
                ScatterWins = new List<List<decimal>> { recorded }
            };
        }

        // Same, but with explicit recorded entries so a test can set NumSymbols (count-scatter wins).
        private static FakeRoundReader ReaderFor(SlotSymbolTableViewModel spin, params RecordedScatterWin[] recorded)
        {
            return new FakeRoundReader
            {
                SlotModel = new GameHistoryGameInfoSlotModel { GameName = "G", Symbols = new[] { spin } },
                SlotDetails = new List<GameHistorySlotPositionDetailModel> { new GameHistorySlotPositionDetailModel() },
                ScatterWinEntries = new List<List<RecordedScatterWin>> { recorded.ToList() }
            };
        }

        private static MultiplierParams WithClaims(MultiplierClaims claims, string group = "coins",
            MultiplierOverlayPlacement placement = MultiplierOverlayPlacement.All) =>
            new MultiplierParams(1, paid: true, strategy: new StrategySpec("TotalBet", new Dictionary<string, string>()),
                groupName: group, placement: placement, claims: claims);

        // The Cyber Cash round from GameId 538 (bet 7.50, line bet 0.15), laid out reel by reel: 11 x P1 (75 each),
        // one R4 (0.3), one R5 (0.15) and two substituting wilds. The engine recorded one count win: (975, 13).
        private static SlotSymbolTableViewModel CyberCashGrid() =>
            Grid(Reel("Wd", "R5", "Wd"), Reel("R4", "P1", "P1"), Reel("P1", "P1", "P1"),
                 Reel("P1", "P1", "P1"), Reel("P1", "P1", "P1"));

        private static MultiplierSymbolMapping CyberCashMapping(MultiplierClaims claims)
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("P1", WithClaims(claims));
            mapping.Insert("R4", WithClaims(claims));
            mapping.Insert("R5", WithClaims(claims));
            return mapping;
        }

        private static readonly Dictionary<string, decimal> CyberCashComputed =
            new Dictionary<string, decimal> { { "P1", 75m }, { "R4", 0.3m }, { "R5", 0.15m } };

        private static MultiplierValidationResult Validate(
            FakeRoundReader reader, MultiplierSymbolMapping mapping, IReadOnlyDictionary<string, decimal> computed) =>
            new MultiplierComputationValidator().ValidateRound(reader, mapping, computed);

        // ----- reconcile ---------------------------------------------------------------------------

        [TestMethod]
        public void A_computed_value_matching_a_recorded_win_produces_no_discrepancy()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid());
            var reader = ReaderFor(Grid(Reel("B10")), new List<decimal> { 200m });
            var computed = new Dictionary<string, decimal> { { "B10", 200m } };

            var result = Validate(reader, mapping, computed);

            Assert.IsFalse(result.HasDiscrepancies);
        }

        [TestMethod]
        public void A_recorded_win_with_no_matching_computed_value_is_flagged_as_unexplained()
        {
            // The grid symbol is unmapped, so nothing computes; the recorded 500 is a real payout config can't explain.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid());
            var reader = ReaderFor(Grid(Reel("X")), new List<decimal> { 500m });
            var computed = new Dictionary<string, decimal> { { "B10", 200m } };

            var result = Validate(reader, mapping, computed);

            Assert.AreEqual(1, result.UnexplainedRecorded.Count);
            Assert.AreEqual(500m, result.UnexplainedRecorded[0].Amount);
            Assert.IsNull(result.UnexplainedRecorded[0].Symbol);   // recorded side carries no symbol
            Assert.AreEqual(0, result.UnmatchedComputed.Count);
        }

        [TestMethod]
        public void A_computed_value_with_no_matching_recorded_win_is_flagged_as_unmatched()
        {
            // B10 is on the grid with a computed amount, but the spin recorded no scatter win (did not trigger).
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid());
            var reader = ReaderFor(Grid(Reel("B10")), new List<decimal>());
            var computed = new Dictionary<string, decimal> { { "B10", 200m } };

            var result = Validate(reader, mapping, computed);

            Assert.AreEqual(1, result.UnmatchedComputed.Count);
            Assert.AreEqual("B10", result.UnmatchedComputed[0].Symbol);
            Assert.AreEqual(200m, result.UnmatchedComputed[0].Amount);
            Assert.AreEqual(0, result.UnexplainedRecorded.Count);
        }

        [TestMethod]
        public void Statically_unpaid_symbols_are_not_reconciled()
        {
            // A TB (unpaid) tile shares a paid tile's amount but must never claim a recorded win; with only the TB on
            // the grid, the recorded win stays unexplained rather than being matched to the TB.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("TB", Unpaid());
            var reader = ReaderFor(Grid(Reel("TB")), new List<decimal> { 200m });
            var computed = new Dictionary<string, decimal> { { "TB", 200m } };

            var result = Validate(reader, mapping, computed);

            Assert.AreEqual(1, result.UnexplainedRecorded.Count);
            Assert.AreEqual(0, result.UnmatchedComputed.Count);
        }

        [TestMethod]
        public void Once_placement_group_counts_a_single_computed_entry_per_spin()
        {
            // Two in-group tiles share ONE recorded win. The once-dedup means only one computed entry is reconciled,
            // so the single recorded 300 matches it and nothing is left unmatched. (An "all" placement here would
            // count two 300s and leave one unmatched — see the contrast test below.)
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("Wh", Paid(MultiplierOverlayPlacement.OnceOnLastOccurrence, group: "wheel"));
            var reader = ReaderFor(Grid(Reel("Wh", "Wh")), new List<decimal> { 300m });
            var computed = new Dictionary<string, decimal> { { "Wh", 300m } };

            var result = Validate(reader, mapping, computed);

            Assert.IsFalse(result.HasDiscrepancies);
        }

        [TestMethod]
        public void All_placement_group_counts_every_occurrence()
        {
            // Contrast with the once case: two "all" tiles both compute 300 but only one 300 was recorded, so one
            // computed value is left unmatched.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid());   // placement All
            var reader = ReaderFor(Grid(Reel("B10", "B10")), new List<decimal> { 300m });
            var computed = new Dictionary<string, decimal> { { "B10", 300m } };

            var result = Validate(reader, mapping, computed);

            Assert.AreEqual(1, result.UnmatchedComputed.Count);
        }

        // ----- claim modes (count-scatter wins recorded with NumSymbols) ---------------------------

        [TestMethod]
        public void Shared_claims_reconcile_a_count_win_across_its_symbols()
        {
            // Every P1 takes one 75 share of (975, 13); the entry is touched, so nothing is unexplained. The single
            // R4 / R5 never reached the 5-symbol trigger, so they are the only unmatched computed values.
            var reader = ReaderFor(CyberCashGrid(), new RecordedScatterWin(975m, 13));

            var result = Validate(reader, CyberCashMapping(MultiplierClaims.Shared), CyberCashComputed);

            Assert.AreEqual(0, result.UnexplainedRecorded.Count);
            CollectionAssert.AreEquivalent(new[] { "R4", "R5" }, result.UnmatchedComputed.Select(d => d.Symbol).ToList());
        }

        [TestMethod]
        public void Whole_claims_cannot_reconcile_a_count_win()
        {
            // The historical rule: no tile computes 975, so all 13 tiles are unmatched and 975 is unexplained.
            var reader = ReaderFor(CyberCashGrid(), new RecordedScatterWin(975m, 13));

            var result = Validate(reader, CyberCashMapping(MultiplierClaims.Whole), CyberCashComputed);

            Assert.AreEqual(1, result.UnexplainedRecorded.Count);
            Assert.AreEqual(975m, result.UnexplainedRecorded[0].Amount);
            Assert.AreEqual(13, result.UnmatchedComputed.Count);
        }

        [TestMethod]
        public void Shared_claims_leave_extra_tiles_unmatched_once_the_shares_run_out()
        {
            // 14 P1 tiles but only 13 shares recorded: exactly one tile is left unmatched.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("P1", WithClaims(MultiplierClaims.Shared));
            var grid = Grid(Reel("P1", "P1", "P1"), Reel("P1", "P1", "P1"), Reel("P1", "P1", "P1"),
                            Reel("P1", "P1", "P1"), Reel("P1", "P1"));
            var reader = ReaderFor(grid, new RecordedScatterWin(975m, 13));

            var result = Validate(reader, mapping, new Dictionary<string, decimal> { { "P1", 75m } });

            Assert.AreEqual(1, result.UnmatchedComputed.Count);
            Assert.AreEqual(0, result.UnexplainedRecorded.Count);
        }

        [TestMethod]
        public void Whole_and_shared_groups_reconcile_side_by_side_in_one_spin()
        {
            // A located B (whole, 20) and a count win of P1 (shared) recorded in the same spin.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B20", WithClaims(MultiplierClaims.Whole, group: "located"));
            mapping.Insert("P1", WithClaims(MultiplierClaims.Shared));
            var grid = Grid(Reel("B20", "P1"), Reel("P1", "P1"), Reel("P1", "P1"));
            var reader = ReaderFor(grid, new RecordedScatterWin(20m), new RecordedScatterWin(375m, 5));
            var computed = new Dictionary<string, decimal> { { "B20", 20m }, { "P1", 75m } };

            var result = Validate(reader, mapping, computed);

            Assert.IsFalse(result.HasDiscrepancies);
        }

        [TestMethod]
        public void A_computed_once_placement_whole_claim_cannot_take_an_entry_counted_over_several_symbols()
        {
            // Whole claims require NumSymbols == 1; only TotalScatterWin is exempt. A wheel configured with a COMPUTED
            // strategy (here TotalBet) against a 3-symbol record is therefore unmatched, and the win unexplained —
            // see A_TotalScatterWin_group_owns_the_spins_scatter_win_and_leaves_nothing_unexplained for the supported way.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("Wh", WithClaims(MultiplierClaims.Whole, group: "wheel",
                placement: MultiplierOverlayPlacement.OnceOnLastOccurrence));
            var reader = ReaderFor(Grid(Reel("Wh"), Reel("Wh"), Reel("Wh")), new RecordedScatterWin(300m, 3));

            var result = Validate(reader, mapping, new Dictionary<string, decimal> { { "Wh", 300m } });

            Assert.AreEqual(1, result.UnmatchedComputed.Count);
            Assert.AreEqual(1, result.UnexplainedRecorded.Count);
        }

        [TestMethod]
        public void The_spin_key_comes_from_the_user_position_dict_when_present()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid());
            var reader = ReaderFor(Grid(Reel("B10")), new List<decimal>());
            reader.UserPositionDict = new List<SlotUserPositionKeyValuePair>
            {
                new SlotUserPositionKeyValuePair { Key = "Spin-A" }
            };
            var computed = new Dictionary<string, decimal> { { "B10", 200m } };

            var result = Validate(reader, mapping, computed);

            Assert.AreEqual(1, result.UnmatchedComputed.Count);
            Assert.AreEqual("Spin-A", result.UnmatchedComputed[0].SpinKey);
        }

        // ----- count mismatch + guards -------------------------------------------------------------

        [TestMethod]
        public void Mismatched_grid_and_detail_counts_validate_only_the_overlapping_spins()
        {
            // Two grid spins but one detail entry: only the first spin is validated (min of the two counts).
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid());
            var reader = new FakeRoundReader
            {
                SlotModel = new GameHistoryGameInfoSlotModel
                {
                    GameName = "G",
                    Symbols = new[] { Grid(Reel("B10")), Grid(Reel("B10")) }   // 2 spins
                },
                SlotDetails = new List<GameHistorySlotPositionDetailModel>
                {
                    new GameHistorySlotPositionDetailModel()                    // only 1 detail entry
                },
                ScatterWins = new List<List<decimal>> { new List<decimal>() }
            };
            var computed = new Dictionary<string, decimal> { { "B10", 200m } };

            var result = Validate(reader, mapping, computed);

            // Only the first spin's B10 is reconciled -> exactly one unmatched computed (not two), and no throw.
            Assert.AreEqual(1, result.UnmatchedComputed.Count);
        }

        [TestMethod]
        public void Returns_an_empty_result_when_there_are_no_grid_spins()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid());
            var reader = new FakeRoundReader
            {
                SlotModel = new GameHistoryGameInfoSlotModel { GameName = "G", Symbols = null },
                SlotDetails = new List<GameHistorySlotPositionDetailModel> { new GameHistorySlotPositionDetailModel() }
            };
            var computed = new Dictionary<string, decimal> { { "B10", 200m } };

            var result = Validate(reader, mapping, computed);

            Assert.IsFalse(result.HasDiscrepancies);
        }

        [TestMethod]
        public void Returns_an_empty_result_when_the_computed_map_is_null()
        {
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("B10", Paid());
            var reader = ReaderFor(Grid(Reel("B10")), new List<decimal> { 200m });

            var result = new MultiplierComputationValidator().ValidateRound(reader, mapping, null);

            Assert.IsFalse(result.HasDiscrepancies);
        }

        [TestMethod]
        public void Discrepancy_flag_is_true_when_either_side_is_non_empty()
        {
            // Sanity on the aggregate flag used by callers.
            var withRecorded = new MultiplierValidationResult();
            withRecorded.UnexplainedRecorded.Add(new MultiplierDiscrepancy("s", null, 5m));
            Assert.IsTrue(withRecorded.HasDiscrepancies);

            var withComputed = new MultiplierValidationResult();
            withComputed.UnmatchedComputed.Add(new MultiplierDiscrepancy("s", "B10", 5m));
            Assert.IsTrue(withComputed.HasDiscrepancies);

            Assert.IsFalse(new MultiplierValidationResult().HasDiscrepancies);
        }

        // ----- TotalScatterWin: exempt from amount matching -----------------------------------------

        private static MultiplierParams WheelTotalScatterWin() =>
            new MultiplierParams(null, paid: true,
                strategy: new StrategySpec("TotalScatterWin", new Dictionary<string, string>()),
                groupName: "wheel", placement: MultiplierOverlayPlacement.OnceOnLastOccurrence);

        [TestMethod]
        public void A_TotalScatterWin_group_owns_the_spins_scatter_win_and_leaves_nothing_unexplained()
        {
            // Three Wh trigger tiles, one wheel win recorded over 3 symbols; the group consumes it, no discrepancy.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("Wh", WheelTotalScatterWin());
            var reader = ReaderFor(Grid(Reel("Wh"), Reel("Wh"), Reel("Wh")), new RecordedScatterWin(300m, 3));

            var result = Validate(reader, mapping, new Dictionary<string, decimal> { { "Wh", 300m } });

            Assert.IsFalse(result.HasDiscrepancies);
        }

        [TestMethod]
        public void A_TotalScatterWin_group_is_unmatched_on_a_spin_that_recorded_nothing()
        {
            // The round total is computed once, so the Wh tile is present on a scatter-less spin; it must not pass.
            var mapping = new MultiplierSymbolMapping();
            mapping.Insert("Wh", WheelTotalScatterWin());
            var reader = ReaderFor(Grid(Reel("Wh"), Reel("Wh"), Reel("Wh")));

            var result = Validate(reader, mapping, new Dictionary<string, decimal> { { "Wh", 300m } });

            Assert.AreEqual(1, result.UnmatchedComputed.Count);
            Assert.AreEqual(0, result.UnexplainedRecorded.Count);
        }
    }
}
