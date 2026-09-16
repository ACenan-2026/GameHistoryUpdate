using System.Collections.Generic;
using GameHistory.Models;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.SlotRoundReaderTests
{
    // Covers the plain, null-safe accessors in SlotRoundReader.Getters.cs. These simply navigate the pulled round
    // model, so the tests build the minimal DTO graph each accessor walks and assert it is returned (or null when
    // a link in the chain is absent). The Details-string parsing lives in SlotRoundReaderScatterTests.
    [TestClass]
    public class SlotRoundReaderGettersTests
    {
        private static GameHistoryGameInfoModel RoundWith(
            GameHistoryGameInfoSlotModel slot = null,
            List<SlotUserPositionKeyValuePair> positions = null,
            List<GameHistorySlotPositionDetailModel> details = null)
        {
            return new GameHistoryGameInfoModel
            {
                GameHistoryGameInfoSlotModel = slot,
                UserPositions = new GameHistoryUserPositionsModel
                {
                    SlotUsersPositionsAndDetails = new GameHistorySlotUserPositionsAndDetailsModel
                    {
                        SlotUserPositionDict = positions,
                        SlotDetails = details == null
                            ? null
                            : new GameHistorySlotResultDetailModel { SlotDetails = details }
                    }
                }
            };
        }

        // ----- GetTotalBet -------------------------------------------------------------------------

        [TestMethod]
        public void GetTotalBet_parses_the_slot_models_bet()
        {
            var slot = new GameHistoryGameInfoSlotModel { Bet = "2.50" };
            var reader = new SlotRoundReader(RoundWith(slot));

            Assert.AreEqual(2.50m, reader.GetTotalBet());
        }

        [TestMethod]
        public void GetTotalBet_is_culture_invariant()
        {
            // A dot is the decimal point regardless of the server locale (see ComputationHelpers.TryParseMoney).
            var slot = new GameHistoryGameInfoSlotModel { Bet = "1000.75" };
            var reader = new SlotRoundReader(RoundWith(slot));

            Assert.AreEqual(1000.75m, reader.GetTotalBet());
        }

        [TestMethod]
        public void GetTotalBet_is_null_when_the_bet_is_unparseable()
        {
            var slot = new GameHistoryGameInfoSlotModel { Bet = "not-a-number" };
            var reader = new SlotRoundReader(RoundWith(slot));

            Assert.IsNull(reader.GetTotalBet());
        }

        [TestMethod]
        public void GetTotalBet_is_null_when_there_is_no_slot_model()
        {
            var reader = new SlotRoundReader(RoundWith(slot: null));

            Assert.IsNull(reader.GetTotalBet());
        }

        [TestMethod]
        public void GetTotalBet_is_null_for_a_null_round()
        {
            Assert.IsNull(new SlotRoundReader(null).GetTotalBet());
        }

        // ----- GetSlotModel / GetGameName ----------------------------------------------------------

        [TestMethod]
        public void GetSlotModel_returns_the_slot_model()
        {
            var slot = new GameHistoryGameInfoSlotModel { GameName = "Fortune" };
            var reader = new SlotRoundReader(RoundWith(slot));

            Assert.AreSame(slot, reader.GetSlotModel());
        }

        [TestMethod]
        public void GetSlotModel_is_null_for_a_null_round()
        {
            Assert.IsNull(new SlotRoundReader(null).GetSlotModel());
        }

        [TestMethod]
        public void GetGameName_returns_the_slot_models_game_name()
        {
            var slot = new GameHistoryGameInfoSlotModel { GameName = "Fortune" };
            var reader = new SlotRoundReader(RoundWith(slot));

            Assert.AreEqual("Fortune", reader.GetGameName());
        }

        [TestMethod]
        public void GetGameName_is_null_when_there_is_no_slot_model()
        {
            var reader = new SlotRoundReader(RoundWith(slot: null));

            Assert.IsNull(reader.GetGameName());
        }

        // ----- GetUserPositionDict -----------------------------------------------------------------

        [TestMethod]
        public void GetUserPositionDict_returns_the_position_dict()
        {
            var positions = new List<SlotUserPositionKeyValuePair>
            {
                new SlotUserPositionKeyValuePair { Key = "Spin1" }
            };
            var reader = new SlotRoundReader(RoundWith(positions: positions));

            Assert.AreSame(positions, reader.GetUserPositionDict());
        }

        [TestMethod]
        public void GetUserPositionDict_is_null_for_a_null_round()
        {
            Assert.IsNull(new SlotRoundReader(null).GetUserPositionDict());
        }

        // ----- GetSlotDetails ----------------------------------------------------------------------

        [TestMethod]
        public void GetSlotDetails_returns_the_detail_entries()
        {
            var details = new List<GameHistorySlotPositionDetailModel>
            {
                new GameHistorySlotPositionDetailModel { Details = "Type: Basic_Scatter, WinAmount: 20" }
            };
            var reader = new SlotRoundReader(RoundWith(details: details));

            Assert.AreSame(details, reader.GetSlotDetails());
        }

        [TestMethod]
        public void GetSlotDetails_is_null_for_a_null_round()
        {
            Assert.IsNull(new SlotRoundReader(null).GetSlotDetails());
        }
    }
}
