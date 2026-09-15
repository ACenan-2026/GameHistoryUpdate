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
        public decimal? TotalBet { get; set; }
        public decimal ScatterWinsTotal { get; set; }
        public string GameName { get; set; } = "TestGame";
        // Non-null by default so WonAmountsComputer doesn't short-circuit its "no slot model" guard; set to null
        // to test that guard.
        public GameHistoryGameInfoSlotModel SlotModel { get; set; } = new GameHistoryGameInfoSlotModel();

        public decimal? GetTotalBet() => TotalBet;
        public decimal GetScatterWinsTotal() => ScatterWinsTotal;
        public string GetGameName() => GameName;
        public GameHistoryGameInfoSlotModel GetSlotModel() => SlotModel;

        public List<List<decimal>> GetScatterWins() => new List<List<decimal>>();
        public List<decimal> GetOneSpinScatterWins(string details) => new List<decimal>();
        public List<SlotUserPositionKeyValuePair> GetUserPositionDict() => null;
        public List<GameHistorySlotPositionDetailModel> GetSlotDetails() => null;
    }
}
