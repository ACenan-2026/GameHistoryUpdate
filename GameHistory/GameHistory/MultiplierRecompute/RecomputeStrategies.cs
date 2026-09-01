using GameHistory.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace GameHistory.MultiplierRecompute
{
    /// <summary>
    /// Defines the interface for multiplier base strategies. Each strategy will implement this interface to provide a 
    /// specific way to calculate the base value for multipliers based on the game history slot model.
    /// </summary>
    public interface IMultiplierBaseStrategy
    {
        decimal GetBase(GameHistoryGameInfoSlotModel gameInfoSlotModel);
    }

    /// <summary>
    /// A concrete implementation of the IMultiplierBaseStrategy interface that calculates the base value for multipliers
    /// Uses the total bet amount from the game history slot model as the base value.
    /// </summary>
    public sealed class TotalBetStrategy : IMultiplierBaseStrategy
    {
        public decimal GetBase(GameHistoryGameInfoSlotModel s) => Decimal.Parse(s.Bet);
    }

    /// <summary>
    /// Factory class to resolve the appropriate multiplier base strategy based on a given type. 
    /// This allows for easy extension and addition of new strategies in the future.
    /// </summary>
    public static class MultiplierBaseStragetyResolver
    {
        public static IMultiplierBaseStrategy Resolve(string type)
        {
            switch(type)
            {
                case "TotalBet":
                    return new TotalBetStrategy();
                default:
                    throw new NotImplementedException($"Multiplier base strategy {type} is not implemented");
            }
        }
    }

}
