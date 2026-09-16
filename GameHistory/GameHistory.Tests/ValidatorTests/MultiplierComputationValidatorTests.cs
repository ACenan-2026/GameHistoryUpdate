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
    }
}
