using GameHistory.Models;
using System.Collections.Generic;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// <see cref="SlotRoundReader"/>: simple, null-safe accessors over the pulled round model
    /// (<see cref="GameHistoryGameInfoModel"/>).
    /// </summary>
    public class SlotRoundReader : ISlotRoundReader
    {
        private readonly GameHistoryGameInfoModel _gameInfo;

        public SlotRoundReader(GameHistoryGameInfoModel gameInfo)
        {
            _gameInfo = gameInfo;
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
