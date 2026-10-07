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
        // Recorded scatter wins. The List<decimal> setters are shorthand for plain single-symbol wins (NumSymbols = 1),
        // which is what every pre-NumSymbols test needs. A test that cares about NumSymbols sets the *Entries form
        // instead; when set, it takes precedence over the shorthand.
        public List<List<decimal>> ScatterWins { get; set; }
        public List<decimal> OneSpinScatterWins { get; set; }
        public List<List<RecordedScatterWin>> ScatterWinEntries { get; set; }
        public List<RecordedScatterWin> OneSpinScatterWinEntries { get; set; }
        public List<SlotUserPositionKeyValuePair> UserPositionDict { get; set; }
        public List<GameHistorySlotPositionDetailModel> SlotDetails { get; set; }

        public decimal? GetTotalBet() => TotalBet;
        public decimal GetScatterWinsTotal() => ScatterWinsTotal;
        public string GetGameName() => GameName;
        public GameHistoryGameInfoSlotModel GetSlotModel() => SlotModel;

        // Hand out fresh entries on every call so a caller that consumes the pool (the validator/gate decrement
        // SharesLeft) cannot corrupt the fake's configured data across calls — mirroring the real reader.
        public List<List<RecordedScatterWin>> GetScatterWins()
        {
            if (ScatterWinEntries != null)
                return ScatterWinEntries.Select(s => s.Select(w => w.Copy()).ToList()).ToList();
            return ScatterWins == null
                ? new List<List<RecordedScatterWin>>()
                : ScatterWins.Select(spin => Plain(spin)).ToList();
        }

        public List<RecordedScatterWin> GetOneSpinScatterWins(string details)
        {
            if (OneSpinScatterWinEntries != null)
                return OneSpinScatterWinEntries.Select(w => w.Copy()).ToList();
            return Plain(OneSpinScatterWins);
        }

        private static List<RecordedScatterWin> Plain(List<decimal> amounts) =>
            amounts == null
                ? new List<RecordedScatterWin>()
                : amounts.Select(a => new RecordedScatterWin(a)).ToList();

        public List<SlotUserPositionKeyValuePair> GetUserPositionDict() => UserPositionDict;
        public List<GameHistorySlotPositionDetailModel> GetSlotDetails() => SlotDetails;
    }
}
