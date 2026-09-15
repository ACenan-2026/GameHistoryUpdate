using GameHistory.Models;
using log4net;
using System.Collections.Generic;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// Computes each configured multiplier symbol's finalised overlay amount by running its strategy against the
    /// round. Takes an already-parsed <see cref="MultiplierSymbolMapping"/> so the config XML is parsed once per
    /// round by the caller and the single mapping is shared (compute + validate + render), rather than re-parsed here.
    /// </summary>
    public class WonAmountsComputer
    {
        private static readonly ILog sLog = LogManager.GetLogger(typeof(WonAmountsComputer));

        /// <summary>
        /// Maps each multiplier symbol in <paramref name="mapping"/> to its finalised amount. A symbol whose strategy
        /// is unknown, or whose amount cannot be determined this round, is omitted (its tile then renders plain).
        /// Returns an empty dictionary when the round has no slot model or the mapping is null/empty.
        /// </summary>
        public IReadOnlyDictionary<string, decimal> ComputeWonAmounts(
            ISlotRoundReader slotRoundReader, MultiplierSymbolMapping mapping)
        {
            var results = new Dictionary<string, decimal>();
            var slot = slotRoundReader.GetSlotModel();
            if (slot == null || mapping == null) return results;

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
