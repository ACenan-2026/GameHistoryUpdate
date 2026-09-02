using GameHistory.Models;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// Phase 1 validation (see MultiplierValidationNotes.md).
    /// Cross-checks the computed multiplier amounts (base * value) against the amounts actually recorded in each
    /// spin's located-scatter win details, and logs divergences. This is LOG-ONLY: it never changes what is
    /// displayed. Its purpose is to turn a silently-wrong computed value into a logged one, and to surface a bad
    /// config value or base.
    ///
    /// Scope + known limits (deliberate for Phase 1):
    ///  - Only located-scatter multipliers (paid == true) are checked. Wild multipliers fold into payline wins
    ///    with no isolated recorded amount, so they cannot be validated this way.
    ///  - Recorded wins cannot be attributed to a specific grid cell (the Symbols field mislabels, and located
    ///    scatters carry no coordinates), so matching is done as a per-spin multiset by amount, not per cell.
    ///  - Grid spins and detail spins are assumed to align by index, which is the same assumption the existing
    ///    render loop in HomeController makes.
    /// </summary>
    public class MultiplierComputationValidator
    {
        private static readonly ILog sLog = LogManager.GetLogger(typeof(MultiplierComputationValidator));

        /// <summary>
        /// Validates one game round. <paramref name="computedBySymbol"/> is the symbol -> finalised-amount map
        /// produced by <see cref="WonAmountsComputer.ComputeScatterAmounts"/>. Returns a result describing any
        /// discrepancies (also logged); the result is returned mainly for testing and for callers that want to act.
        /// </summary>
        public MultiplierValidationResult ValidateRound(
            GameHistoryGameInfoModel gameInfo,
            MultiplierSymbolMapping mapping,
            IReadOnlyDictionary<string, decimal> computedBySymbol)
        {
            var result = new MultiplierValidationResult();

            var slotPositions = gameInfo?.UserPositions?.SlotUsersPositionsAndDetails;
            var grids = slotPositions?.SlotUserPositionDict;
            var details = slotPositions?.SlotDetails?.SlotDetails;
            if (grids == null || details == null || mapping == null || computedBySymbol == null)
            {
                return result;
            }

            string gameName = gameInfo.GameHistoryGameInfoSlotModel?.GameName ?? "(unknown game)";

            int spinCount = Math.Min(grids.Count, details.Count);
            if (grids.Count != details.Count)
            {
                sLog.DebugFormat(
                    "Game '{0}': grid spin count ({1}) does not match detail spin count ({2}); validating the first {3}.",
                    gameName, grids.Count, details.Count, spinCount);
            }

            for (int i = 0; i < spinCount; i++)
            {
                string spinKey = grids[i]?.Key ?? i.ToString();
                var computed = CollectComputedPaidMultipliers(grids[i], mapping, computedBySymbol);
                var recorded = ParseLocatedScatterAmounts(details[i]?.Details);
                Reconcile(gameName, spinKey, computed, recorded, result);
            }

            return result;
        }

        /// <summary>
        /// Gathers every paid-multiplier symbol occurrence in a spin's grid, paired with its computed amount.
        /// </summary>
        private static List<KeyValuePair<string, decimal>> CollectComputedPaidMultipliers(
            SlotUserPositionKeyValuePair grid,
            MultiplierSymbolMapping mapping,
            IReadOnlyDictionary<string, decimal> computedBySymbol)
        {
            var computed = new List<KeyValuePair<string, decimal>>();
            var rows = grid?.Value;
            if (rows == null) return computed;

            foreach (var row in rows)
            {
                var positions = row?.Positions;
                if (positions == null) continue;

                foreach (var symbol in positions)
                {
                    if (string.IsNullOrEmpty(symbol)) continue;
                    if (mapping.TryGet(symbol, out var p) && p.Paid
                        && computedBySymbol.TryGetValue(symbol, out var amount))
                    {
                        computed.Add(new KeyValuePair<string, decimal>(symbol, amount));
                    }
                }
            }
            return computed;
        }

        /// <summary>
        /// Extracts the WinAmount of every located-scatter win from a spin's semi-structured Details string.
        /// Entries with an empty WinAmount are skipped; zeros are kept (a located scatter that did not pay).
        /// </summary>
        private static List<decimal> ParseLocatedScatterAmounts(string details)
        {
            var amounts = new List<decimal>();
            if (string.IsNullOrEmpty(details)) return amounts;

            var lines = details.Split(new[] { "<br/>" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var fields = ParseFields(line);
                if (fields.TryGetValue("ScatterType", out var scatterType)
                    && scatterType.Equals("LocatedScatter", StringComparison.OrdinalIgnoreCase)
                    && fields.TryGetValue("WinAmount", out var winAmount)
                    && ComputationHelpers.TryParseMoney(winAmount, out var amount))
                {
                    amounts.Add(amount);
                }
            }
            return amounts;
        }

        /// <summary>
        /// Parses one "key: value,key: value,..." Details line into a case-insensitive field lookup.
        /// </summary>
        private static Dictionary<string, string> ParseFields(string line)
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in line.Split(','))
            {
                int idx = part.IndexOf(':');
                if (idx <= 0) continue;
                string key = part.Substring(0, idx).Trim();
                string val = part.Substring(idx + 1).Trim();
                if (!fields.ContainsKey(key)) fields[key] = val;
            }
            return fields;
        }

        /// <summary>
        /// Matches computed paid-multiplier values against recorded located-scatter amounts for one spin (as a
        /// multiset, by amount). Recorded zeros are not match targets (they represent non-paying located scatters).
        ///  - A recorded amount left unmatched is a real payout config/base cannot explain -> WARN (actionable).
        ///  - A computed value left unmatched often just means the symbol was on the reels but did not trigger a
        ///    located win this spin -> DEBUG (expected, low signal).
        /// </summary>
        private static void Reconcile(
            string gameName,
            string spinKey,
            List<KeyValuePair<string, decimal>> computed,
            List<decimal> recorded,
            MultiplierValidationResult result)
        {
            var recordedPool = recorded.Where(a => a != 0m).ToList();

            foreach (var c in computed)
            {
                int idx = recordedPool.IndexOf(c.Value);
                if (idx >= 0)
                {
                    recordedPool.RemoveAt(idx); // matched
                }
                else
                {
                    result.UnmatchedComputed.Add(new MultiplierDiscrepancy(spinKey, c.Key, c.Value));
                    sLog.DebugFormat(
                        "Game '{0}', spin '{1}': computed multiplier {2}={3} has no matching recorded located-scatter win (may not have triggered).",
                        gameName, spinKey, c.Key, c.Value);
                }
            }

            foreach (var amount in recordedPool)
            {
                result.UnexplainedRecorded.Add(new MultiplierDiscrepancy(spinKey, null, amount));
                sLog.WarnFormat(
                    "Game '{0}', spin '{1}': recorded located-scatter win {2} has no matching computed multiplier value; check config/base.",
                    gameName, spinKey, amount);
            }
        }
    }

    /// <summary>
    /// A single computed-vs-recorded discrepancy. <see cref="Symbol"/> is null when the discrepancy is a recorded
    /// amount that no computed value explained.
    /// </summary>
    public sealed class MultiplierDiscrepancy
    {
        public string SpinKey { get; }
        public string Symbol { get; }
        public decimal Amount { get; }

        public MultiplierDiscrepancy(string spinKey, string symbol, decimal amount)
        {
            SpinKey = spinKey;
            Symbol = symbol;
            Amount = amount;
        }
    }

    /// <summary>
    /// Outcome of validating a round.
    /// </summary>
    public sealed class MultiplierValidationResult
    {
        /// <summary>Recorded located-scatter payouts with no matching computed value (likely a config/base gap).</summary>
        public List<MultiplierDiscrepancy> UnexplainedRecorded { get; } = new List<MultiplierDiscrepancy>();

        /// <summary>Computed paid-multiplier values with no matching recorded payout (often a non-triggering occurrence).</summary>
        public List<MultiplierDiscrepancy> UnmatchedComputed { get; } = new List<MultiplierDiscrepancy>();

        public bool HasDiscrepancies => UnexplainedRecorded.Count > 0 || UnmatchedComputed.Count > 0;
    }
}
