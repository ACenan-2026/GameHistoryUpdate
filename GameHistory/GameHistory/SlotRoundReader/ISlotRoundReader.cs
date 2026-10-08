using GameHistory.Models;
using System.Collections.Generic;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// Reads the parts of a pulled Game History round (the deserialized <see cref="GameHistoryGameInfoModel"/>)
    /// that the details view and the multiplier overlay need: the per-spin detail entries, the round-level slot
    /// model and the game name. This is the single surface that knows the model's shape, so no other code has to
    /// navigate the DTO.
    /// </summary>
    public interface ISlotRoundReader
    {
        /// <summary>The per-spin detail entries (bet, outcome, Details string, ...).</summary>
        List<GameHistorySlotPositionDetailModel> GetSlotDetails();

        /// <summary>The round-level slot model (bet, game name, times, ...).</summary>
        GameHistoryGameInfoSlotModel GetSlotModel();

        /// <summary>The game name, or null when unavailable.</summary>
        string GetGameName();
    }
}
