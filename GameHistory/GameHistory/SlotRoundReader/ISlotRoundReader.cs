using GameHistory.Models;
using System.Collections.Generic;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// Reads the parts of a pulled Game History round (the deserialized <see cref="GameHistoryGameInfoModel"/>)
    /// that the multiplier-recompute feature needs: the total bet, the per-spin grids and detail entries, and the
    /// recorded located-scatter win amounts. This is the single surface that knows the model's shape and the
    /// semi-structured "Details" format, so no other code has to navigate the DTO or parse that string.
    /// </summary>
    public interface ISlotRoundReader
    {
        /// <summary>The round's total bet, or null when it cannot be parsed.</summary>
        decimal? GetTotalBet();

        /// <summary>Per-spin recorded scatter win amounts (one inner list per spin; empty when a spin has none).</summary>
        List<List<decimal>> GetScatterWins();

        /// <summary>Recorded scatter win amounts parsed from a single spin's Details string.</summary>
        List<decimal> GetOneSpinScatterWins(string details);

        /// <summary>Sum of every recorded scatter win across the round.</summary>
        decimal GetScatterWinsTotal();

        /// <summary>The per-spin grid stop positions.</summary>
        List<SlotUserPositionKeyValuePair> GetUserPositionDict();

        /// <summary>The per-spin detail entries (bet, outcome, Details string, ...).</summary>
        List<GameHistorySlotPositionDetailModel> GetSlotDetails();

        /// <summary>The round-level slot model (bet, game name, times, ...).</summary>
        GameHistoryGameInfoSlotModel GetSlotModel();

        /// <summary>The game name, or null when unavailable.</summary>
        string GetGameName();
    }
}
