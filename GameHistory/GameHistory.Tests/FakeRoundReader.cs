using System.Collections.Generic;
using GameHistory.Models;
using GameHistory.MultiplierRecompute;

namespace GameHistory.Tests
{
    /// <summary>
    /// Hand-rolled stand-in for <see cref="ISlotRoundReader"/>. Set only the fields a given test cares about;
    /// everything else returns a harmless default. A hand fake keeps the setup readable and needs no mocking
    /// library (handy here, where only the offline package feed is available).
    /// </summary>
    internal sealed class FakeRoundReader : ISlotRoundReader
    {
        public string GameName { get; set; } = "TestGame";
        public GameHistoryGameInfoSlotModel SlotModel { get; set; } = new GameHistoryGameInfoSlotModel();
        public List<GameHistorySlotPositionDetailModel> SlotDetails { get; set; }

        public string GetGameName() => GameName;
        public GameHistoryGameInfoSlotModel GetSlotModel() => SlotModel;
        public List<GameHistorySlotPositionDetailModel> GetSlotDetails() => SlotDetails;
    }
}
