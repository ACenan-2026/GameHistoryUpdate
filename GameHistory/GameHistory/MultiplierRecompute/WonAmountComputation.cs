using GameHistory.Models;
using log4net;
using System.Collections.Generic;

namespace GameHistory.MultiplierRecompute
{
    public class WonAmountsComputer
    {
        private static readonly ILog sLog = LogManager.GetLogger(typeof(WonAmountsComputer));
        private readonly IMultiplierConfigParser _configParser;

        public WonAmountsComputer(IMultiplierConfigParser configParser) => _configParser = configParser;

        public IReadOnlyDictionary<string, decimal> ComputeScatterAmounts(GameHistoryGameInfoModel gameInfo)
        {
            var results = new Dictionary<string, decimal>();
            var slot = gameInfo?.GameHistoryGameInfoSlotModel;
            if (slot == null) return results;

            var mapping = _configParser.GetMultiplierParams();
            var baseCache = new Dictionary<string, decimal?>();   // strategyType -> base, computed once

            foreach (var entry in mapping.Mappings)
            {
                var p = entry.Value;

                var strategy = MultiplierBaseStrategyResolver.Resolve(p.StrategyType);
                if (strategy == null) { sLog.WarnFormat("No strategy '{0}' for symbol '{1}'.", p.StrategyType, entry.Key); continue; }

                if (!baseCache.TryGetValue(p.StrategyType, out var baseVal))
                    baseCache[p.StrategyType] = baseVal = strategy.GetBase(slot);

                if (baseVal == null) continue;        // base unknown -> no overlay for this symbol
                results[entry.Key] = baseVal.Value * p.Multiplier;
            }
            return results;
        }
    }
}