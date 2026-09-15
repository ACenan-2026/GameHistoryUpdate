using System.Collections.Generic;
using System.Linq;

namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// <see cref="SlotRoundReader"/> — reads the recorded scatter-win amounts out of each spin's semi-structured,
    /// "
    /// br/>"-delimited Details string. This is the single owner of that fragile parse. The plain model
    /// accessors live in the sibling partial, SlotRoundReader.Getters.cs.
    /// </summary>
    public partial class SlotRoundReader
    {
        // The delimiters used to parse the Details field in the game history data. Fragile if the format of the
        // Details field changes, but this is the expected format based on current data.
        static readonly string DetailsSpinDelimiter = "<br/>";
        static readonly char DetailsFieldDelimiter = ',';
        static readonly char DetailsKeyValueDelimiter = ':';

        private static readonly HashSet<string> sScatterWinCategories =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "Basic_Scatter" };

        /// <summary>
        /// True when a Details entry's "Type" denotes a scatter-kind win (see <see cref="sScatterWinCategories"/>).
        /// Callers pair this with a numeric WinAmount to isolate the entry that actually paid from non-paying
        /// scatter markers and from payline wins.
        /// </summary>
        internal static bool IsScatterWinCategory(string type) =>
            !string.IsNullOrEmpty(type) && sScatterWinCategories.Contains(type);

        // Parsed-once cache of the recorded scatter wins for every spin. The round (_gameInfo) is immutable, so the
        // fragile Details parse is done a single time and reused across the validator, the render gate and the
        // TotalScatterWin strategy rather than repeated on every call.
        private List<List<decimal>> _scatterWinsCache;

        private List<List<decimal>> ScatterWins =>
            _scatterWinsCache ?? (_scatterWinsCache = ParseAllScatterWins());

        /// <summary>
        /// Retrieves a list of scatter wins from the game history. Each inner list corresponds to a single spin and
        /// contains the amounts of all scatter wins for that spin. If a spin has no scatter wins, the corresponding
        /// inner list will be empty. Each call returns a fresh copy, so a caller that consumes/mutates the lists
        /// (e.g. the validator's multiset reconcile) cannot corrupt the cache or another caller's view.
        /// </summary>
        /// <returns>A list of lists containing scatter win amounts.</returns>
        public List<List<decimal>> GetScatterWins()
        {
            return ScatterWins.Select(spin => new List<decimal>(spin)).ToList();
        }

        /// <summary>
        /// Parses the recorded scatter wins for every spin out of the Details strings. Invoked once, lazily, via
        /// <see cref="ScatterWins"/>; not called directly.
        /// </summary>
        private List<List<decimal>> ParseAllScatterWins()
        {
            var slotDetails = _gameInfo?.UserPositions?.SlotUsersPositionsAndDetails?.SlotDetails?.SlotDetails;
            var scatterWinsList = new List<List<decimal>>();
            if (slotDetails == null || slotDetails.Count == 0)
            {
                return scatterWinsList; // Return empty list if there are no slot details
            }

            foreach (var spin in slotDetails)
            {
                scatterWinsList.Add(GetOneSpinScatterWins(spin.Details));
            }
            return scatterWinsList;
        }

        public List<decimal> GetOneSpinScatterWins(string details)
        {
            var scatterWins = new List<decimal>();
            if (string.IsNullOrEmpty(details))
            {
                return scatterWins; // Return empty list if details are null or empty
            }

            string[] eachWinType = details.Split(new string[] { DetailsSpinDelimiter }, System.StringSplitOptions.RemoveEmptyEntries); // splitting on <br/>

            foreach (var entry in eachWinType)
            {
                decimal? winAmount = GetScatterWonAmount(entry);
                if (winAmount.HasValue)
                {
                    scatterWins.Add(winAmount.Value);
                }
            }
            return scatterWins.Where(d => d != 0m).ToList();
        }

        private decimal? GetScatterWonAmount(string entry)
        {
            decimal amount = 0m;
            bool validType = false;
            string[] keyValues = entry.Split(DetailsFieldDelimiter);        // splitting on ','

            if (keyValues.Length == 0) return null;
            foreach (var keyValue in keyValues)
            {
                string[] pair = keyValue.Split(DetailsKeyValueDelimiter);   // splitting on ':'
                if (pair.Length != 2) continue;
                string key = pair[0].Trim();
                string val = pair[1].Trim();
                if (key.Equals("Type", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsScatterWinCategory(val))
                    {
                        return null; // Not an allowed win type, skip this entry
                    }
                    validType = true;
                }
                else if (key.Equals("WinAmount", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!ComputationHelpers.TryParseMoney(val, out amount))
                    {
                        return null; // Invalid amount, skip this entry
                    }
                }
            }

            if (validType)
            {
                return amount;
            }
            return null;
        }

        public decimal GetScatterWinsTotal()
        {
            // Read-only aggregate over the cache; no copy needed since Sum does not mutate.
            return ScatterWins.SelectMany(row => row).Sum();
        }
    }
}
