using System.Collections.Generic;
using System.Linq;
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

        // Optional backing data for the reader's collection accessors. Each defaults to a benign value so tests
        // that don't touch them behave exactly as before; a test that needs a specific outcome sets the field.
        public List<List<decimal>> ScatterWins { get; set; }
        public List<decimal> OneSpinScatterWins { get; set; }
        public List<SlotUserPositionKeyValuePair> UserPositionDict { get; set; }
        public List<GameHistorySlotPositionDetailModel> SlotDetails { get; set; }

        public decimal? GetTotalBet() => TotalBet;
        public decimal GetScatterWinsTotal() => ScatterWinsTotal;
        public string GetGameName() => GameName;
        public GameHistoryGameInfoSlotModel GetSlotModel() => SlotModel;

        // Hand out fresh copies so a caller that mutates the lists (the validator/gate consume them with RemoveAt)
        // cannot corrupt the fake's configured data across calls — mirroring the real reader's copy-on-read.
        public List<List<decimal>> GetScatterWins() =>
            ScatterWins == null
                ? new List<List<decimal>>()
                : ScatterWins.Select(s => new List<decimal>(s)).ToList();

        public List<decimal> GetOneSpinScatterWins(string details) =>
            OneSpinScatterWins == null ? new List<decimal>() : new List<decimal>(OneSpinScatterWins);

        public List<SlotUserPositionKeyValuePair> GetUserPositionDict() => UserPositionDict;
        public List<GameHistorySlotPositionDetailModel> GetSlotDetails() => SlotDetails;
    }
}
