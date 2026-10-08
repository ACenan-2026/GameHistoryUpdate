using System.Collections.Generic;
using GameHistory.Models;
using GameHistory.MultiplierRecompute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameHistory.Tests.SlotRoundReaderTests
{
    // Covers the plain, null-safe accessors in SlotRoundReader.Getters.cs. These simply navigate the pulled round
    // model, so the tests build the minimal DTO graph each accessor walks and assert it is returned (or null when
    // a link in the chain is absent).
    [TestClass]
    public class SlotRoundReaderGettersTests
    {
        private static GameHistoryGameInfoModel RoundWith(
            GameHistoryGameInfoSlotModel slot = null,
            List<GameHistorySlotPositionDetailModel> details = null)
        {
            return new GameHistoryGameInfoModel
            {
                GameHistoryGameInfoSlotModel = slot,
                UserPositions = new GameHistoryUserPositionsModel
                {
                    SlotUsersPositionsAndDetails = new GameHistorySlotUserPositionsAndDetailsModel
                    {
                        SlotDetails = details == null
                            ? null
                            : new GameHistorySlotResultDetailModel { SlotDetails = details }
                    }
                }
            };
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
