using GameHistory.Models;
using System.Collections.Generic;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// <see cref="SlotRoundReader"/> — simple, null-safe accessors over the pulled round model
    /// (<see cref="GameHistoryGameInfoModel"/>). The recorded scatter-win parsing lives in the sibling partial,
    /// SlotRoundReader.Scatter.cs.
    /// </summary>
    public partial class SlotRoundReader : ISlotRoundReader
    {
        private readonly GameHistoryGameInfoModel _gameInfo;

        public SlotRoundReader(GameHistoryGameInfoModel gameInfo)
        {
            _gameInfo = gameInfo;
        }

        public decimal? GetTotalBet()
        {
            return ComputationHelpers.TryParseMoney(_gameInfo?.GameHistoryGameInfoSlotModel?.Bet, out decimal totalBet)
                ? totalBet
                : (decimal?)null;
        }

        public List<SlotUserPositionKeyValuePair> GetUserPositionDict()
        {
            return _gameInfo?.UserPositions?.SlotUsersPositionsAndDetails?.SlotUserPositionDict;
        }

        public List<GameHistorySlotPositionDetailModel> GetSlotDetails()
        {
            return _gameInfo?.UserPositions?.SlotUsersPositionsAndDetails?.SlotDetails?.SlotDetails;
        }

        public GameHistoryGameInfoSlotModel GetSlotModel()
        {
            return _gameInfo?.GameHistoryGameInfoSlotModel;
        }

        public string GetGameName()
        {
            return GetSlotModel()?.GameName;
        }
    }
}
