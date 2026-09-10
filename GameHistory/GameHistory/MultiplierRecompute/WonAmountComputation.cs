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

        public IReadOnlyDictionary<string, decimal> ComputeScatterAmounts(ISlotRoundReader slotRoundReader)
        {
            var results = new Dictionary<string, decimal>();
            var slot = slotRoundReader.GetSlotModel();
            if (slot == null) return results;

            var mapping = _configParser.GetMultiplierParams();

            foreach (var entry in mapping.Mappings)
            {
                var p = entry.Value;

                var strategy = MultiplierBaseStrategyResolver.Resolve(p.Strategy?.Type, p.Strategy?.Attributes);
                if (strategy == null) { sLog.WarnFormat("No strategy '{0}' for symbol '{1}'.", p.Strategy?.Type, entry.Key); continue; }

                var wonAmountVal = strategy.GetWonAmount(slotRoundReader, p);

                if (wonAmountVal == null) continue;        // wonAmount unknown -> no overlay for this symbol
                results[entry.Key] = wonAmountVal.Value;
            }
            return results;
        }
    }
}