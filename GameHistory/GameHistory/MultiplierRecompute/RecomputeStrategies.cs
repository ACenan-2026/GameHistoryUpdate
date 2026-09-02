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
    /// Defines the interface for multiplier base strategies. Each strategy will implement this interface to provide a
    /// specific way to calculate the base value for multipliers based on the game history slot model.
    /// Returns null when the base cannot be determined, so the caller can fall back to rendering the plain
    /// multiplier symbol rather than a wrong (or missing) value.
    /// </summary>
    public interface IMultiplierBaseStrategy
    {
        decimal? GetBase(GameHistoryGameInfoSlotModel gameInfoSlotModel);
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

        public decimal? GetBase(GameHistoryGameInfoSlotModel s) =>
            ComputationHelpers.TryParseMoney(s?.Bet, out decimal value) ? value : (decimal?)null;
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

        public decimal? GetBase(GameHistoryGameInfoSlotModel s) =>
            ComputationHelpers.TryParseMoney(s?.Bet, out var totalBet) 
                ? decimal.Round(totalBet * _numerator / _denominator, 2, System.MidpointRounding.AwayFromZero)
                : (decimal?)null;
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
                default:
                    return null;
            }
        }
    }

}
