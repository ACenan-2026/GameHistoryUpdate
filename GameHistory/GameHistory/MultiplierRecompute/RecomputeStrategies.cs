using GameHistory.Models;
using log4net;
using System.Collections.Generic;
using System.Globalization;



namespace GameHistory.MultiplierRecompute
{
    internal class ComputationHelpers
    {
        /// <summary>
        /// Tries to parse a string representation of a monetary value into a decimal.
        /// The method uses the invariant culture to ensure consistent parsing regardless of the system's locale settings.
        /// Assumes no currency symbols are present in the string.
        /// </summary>
        /// <param name="s">The string representation of the monetary value.</param>
        /// <param name="value">The parsed decimal value.</param>
        /// <returns>true if the parsing was successful; otherwise, false.</returns>
        internal static bool TryParseMoney(string s, out decimal value)
        {

            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }
    }


    /// <summary>
    /// Defines the interface for multiplier strategies. Each strategy computes the finalised amount to
    /// overlay on a multiplier symbol, given the full game-round model and that symbol's config params.
    /// Most strategies compute base × params.Multiplier from round-level data (the total bet); some
    /// (e.g. <see cref="TotalScatterWinStrategy"/>) instead read the recorded outcome from the round. The full
    /// <see cref="GameHistoryGameInfoModel"/> is passed so a strategy can reach either.
    /// Returns null when the amount cannot be determined, so the caller can fall back to rendering the
    /// plain multiplier symbol rather than a wrong (or missing) value.
    /// </summary>
    public interface IMultiplierBaseStrategy
    {
        decimal? GetWonAmount(ISlotRoundReader slotRoundReader, MultiplierParams multiplierParams);
    }


    /// <summary>
    /// A concrete implementation of the IMultiplierBaseStrategy interface that calculates the base value for multipliers
    /// Uses the total bet amount from the game history slot model as the base value.
    /// </summary>
    public sealed class TotalBetStrategy : IMultiplierBaseStrategy
    {
        /// <summary>
        /// Shared stateless instance. TotalBetStrategy holds no per-game state, so a single instance can be reused.
        /// </summary>
        public static readonly TotalBetStrategy Instance = new TotalBetStrategy();

        public decimal? GetWonAmount(ISlotRoundReader slotRoundReader, MultiplierParams multiplierParams) =>
            slotRoundReader.GetTotalBet() * multiplierParams.Multiplier;
    }

    /// <summary>
    /// Computes base = total_bet * numerator / denominator (multiply before divide, so an unreduced ratio like
    /// 50/75 is as exact as 2/3). This is the line-bet total expressed as a fixed fraction of the total bet.
    ///
    /// ASSUMES the ratio is constant for the game. It is fed either as a raw ratio ("LineBetTotal") or as the
    /// game constants numLines/staticBetMultiplier ("LineBetFromStaticMultiplier") — both resolve to this class.
    /// Do NOT use for games where the line count (or the bet multiplier) can vary per spin: there the ratio
    /// is not constant and total_bet alone cannot recover the base.
    /// </summary>
    public sealed class LineBetTotalStrategy : IMultiplierBaseStrategy
    {
        // numerator/denominator of the fixed line-bet-total : total-bet ratio
        private readonly decimal _numerator;
        private readonly decimal _denominator;

        public LineBetTotalStrategy(decimal numerator, decimal denominator)
        {
            _numerator = numerator;
            _denominator = denominator;
        }

        public decimal? GetWonAmount(ISlotRoundReader slotRoundReader, MultiplierParams multiplierParams) =>
            slotRoundReader.GetTotalBet() is decimal totalBet
                ? decimal.Round(totalBet * _numerator / _denominator, 2, System.MidpointRounding.AwayFromZero) * multiplierParams.Multiplier
                : (decimal?)null;
    }

    /// <summary>
    /// Reads the finalised amount straight from the recorded outcome instead of computing it. Use this
    /// for games where the multiplier is resolved by a mechanic that history does not fully record — for
    /// example a fortune wheel that can land a base×wheel multiplier OR a fixed jackpot (Mini/Minor/
    /// Major/Grand) — but that always records the resulting located-scatter win.
    ///
    /// It returns the scatter-win WinAmount(s) recorded in the spin Details, which isolates the
    /// multiplier win from other wins: each Details entry carries a "Type" — a payline win is "Type: Payline",
    /// a scatter-kind win is "Type: Basic_Scatter" — and the entry that actually PAID carries a non-empty
    /// WinAmount. Which "Type" tokens count is a code-owned concern (see
    /// <see cref="SlotRoundReader.IsScatterWinCategory"/>), so the game config never has to name a database
    /// token. When no paying scatter win is recorded — e.g. a spin where the Wh symbol appeared but the wheel did
    /// not trigger and only a payline paid — it returns null so the tile renders plain rather than showing an
    /// unrelated win. It deliberately does NOT fall back to the round's total Won, which would include payline wins.
    /// It is stateless — no config values are needed; the config only lists which symbol(s) are the target.
    ///
    /// ASSUMES one located pay per record, shown on the one visible overlay symbol. If a record ever
    /// carried multiple located pays this returns their sum, which is only meaningful for a single tile.
    /// Returns null when no located-scatter amount is recorded, so the caller renders the plain symbol.
    /// </summary>
    public sealed class TotalScatterWinStrategy : IMultiplierBaseStrategy
    {
        /// <summary>Shared stateless instance — the strategy reads only the recorded outcome.</summary>
        public static readonly TotalScatterWinStrategy Instance = new TotalScatterWinStrategy();
        public decimal? GetWonAmount(ISlotRoundReader slotRoundReader, MultiplierParams multiplierParams)
        {
            // Overlay ONLY a paying scatter-kind win. A spin can show a multiplier symbol without the wheel
            // feature triggering — e.g. only a payline win, with no paying scatter. The Details entries
            // distinguish these by "Type" (a payline win is "Type: Payline"; a scatter-kind win is
            // "Type: Basic_Scatter" — see SlotRoundReader.IsScatterWinCategory) plus a non-empty WinAmount, so
            // a payline win is ignored and we return null → the Wh tile renders plain rather than greedily showing
            // an unrelated win. Do NOT fall back to the round's total Won: that total includes payline wins and
            // would be painted onto an uninvolved tile.
            var total = slotRoundReader.GetScatterWinsTotal();
            return total > 0m ? total : (decimal?)null;
        }
    }


    /// <summary>
    /// Factory class to resolve the appropriate multiplier base strategy based on a given type.
    /// This allows for easy extension and addition of new strategies in the future.
    /// Returns null for an unknown / not-yet-implemented strategy type so the caller can degrade gracefully;
    /// the caller should log the null so a misconfigured strategy name surfaces.
    /// </summary>
    public static class MultiplierBaseStrategyResolver
    {

        private static readonly ILog sLog = LogManager.GetLogger(typeof(MultiplierBaseStrategyResolver));

        /// <summary>
        /// Given a dictionary, attempts to get the integer value assoicated with a specified key.
        /// Returns true if the key exists and the value can be parsed as an integer; otherwise, returns false.
        /// </summary>
        /// <param name="attrs">The dictionary to read from</param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool TryGetInt(IReadOnlyDictionary<string, string> attrs, string key, out int value)
        {
            value = 0;
            return attrs != null
                && attrs.TryGetValue(key, out var raw)
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static IMultiplierBaseStrategy Resolve(string type, IReadOnlyDictionary<string, string> attributes)
        {
            switch(type)
            {
                case "TotalBet":
                    return TotalBetStrategy.Instance;
                case "LineBetTotal":
                    // expects properties "ratioNumerator" and "ratioDenominator" to be present in the attributes dictionary
                    if (!TryGetInt(attributes, "ratioNumerator", out int num) || 
                        !TryGetInt(attributes, "ratioDenominator", out int denom) ||
                        denom == 0)
                    {
                        sLog.WarnFormat("LineBetTotal strategy requires 'ratioNumerator' and non-zero 'ratioDenominator' attributes.");
                        return null;
                    }
                    return new LineBetTotalStrategy(num, denom);
                case "LineBetFromStaticMultiplier":
                    // Self-documenting, game-constant form: base = total_bet * numLines / staticBetMultiplier.
                    // Assumes both are fixed for the game (see LineBetTotalStrategy); not for variable-line games.
                    if (!TryGetInt(attributes, "numLines", out int lines) ||
                        !TryGetInt(attributes, "staticBetMultiplier", out int staticMult) ||
                        staticMult == 0)
                    {
                        sLog.WarnFormat("LineBetFromStaticMultiplier strategy requires 'numLines' and non-zero 'staticBetMultiplier' attributes.");
                        return null;
                    }
                    return new LineBetTotalStrategy(lines, staticMult);
                case "TotalScatterWin":
                    // Reads the finalised amount straight from the recorded located-scatter win — for
                    // wheel/jackpot games where the amount can't be reconstructed from config values.
                    return TotalScatterWinStrategy.Instance;
                default:
                    return null;
            }
        }
    }

}
