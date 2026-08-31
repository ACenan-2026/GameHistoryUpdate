//  $Header$
//
//========================================================================================
//
//  Description:    Implementation file for the GameHistoryGameInfoModel classes.
//
//  Copyright:  (c) Ainsworth Gaming Technology. All Rights Reserved.
//
//========================================================================================
//  Revision History
//  04/02/2015  SC          Added support for up to 15 visible reels.  
//========================================================================================
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;


namespace GameHistory.Models
{
  // ------------------------- LEVEL 1: GAME INFO ---------------------

  public class GameHistoryGameInfoModel
  {
    public bool IsGameHistoryGameInfoGenericVisible { get; set; }
    public GameHistoryGameInfoGenericModel GameHistoryGameInfoGenericModel { get; set; }

    public bool IsGameHistoryGameInfoSlotVisible { get; set; }
    public GameHistoryGameInfoSlotModel GameHistoryGameInfoSlotModel { get; set; }

    public bool IsGameHistoryGameInfoRouletteVisible { get; set; }
    public GameHistoryGameInfoRouletteModel GameHistoryGameInfoRouletteModel { get; set; }

    public bool IsGameHistoryGameInfoBaccaratVisible { get; set; }
    public GameHistoryGameInfoBaccaratModel GameHistoryGameInfoBaccaratModel { get; set; }

    public bool IsGameHistoryGameInfoDiceVisible { get; set; }
    public GameHistoryGameInfoDiceModel GameHistoryGameInfoDiceModel { get; set; }

    public bool IsGameHistoryGameInfoSlotFallVisible { get; set; }
    public GameHistoryGameInfoSlotFallModel GameHistoryGameInfoSlotFallModel { get; set; }

    public bool IsUserListSectionVisible { get; set; }
    public GameHistoryUsersModel Users { get; set; }

    public bool IsUserPositionsSectionVisible { get; set; }
    public GameHistoryUserPositionsModel UserPositions { get; set; }

    public bool LoadGameDetailsSectionOnDocumentReady { get; set; }
  }

  public class GameHistoryGameInfoGenericModel
  {
    public string GameId { get; set; }
    public string GameIdentifier { get; set; }
    public string GameName { get; set; }
    public string StartTime { get; set; }
    public string StopTime { get; set; }
    public string Stake { get; set; }
    public string Won { get; set; }
    public string GameType { get; set; }
    public int UserId { get; set; }
    public string ExtUserId { get; set; }
    public string UserName { get; set; }
    public int ConfiguredContent { get; set; }    
  }

  public class GameHistoryGameInfoRouletteModel : GameHistoryGameInfoGenericModel
  {
    public string WinningNumber { get; set; }
  }

  public class GameHistoryGameInfoSlotModel : GameHistoryGameInfoGenericModel
  {
    public string Bet { get; set; }
    public string Revenue { get; set; }
    public string NumberOfLines { get; set; }

    public SlotSymbolTableViewModel[] Symbols { get; set; }
  }

  public class GameHistoryGameInfoBaccaratModel : GameHistoryGameInfoGenericModel
  {
    public string Bet { get; set; }
    public string Revenue { get; set; }
  }

  public class GameHistoryGameInfoDiceModel : GameHistoryGameInfoGenericModel
  {
    public string Bet { get; set; }
    public string Revenue { get; set; }
  }

  public class GameHistoryGameInfoSlotFallModel : GameHistoryGameInfoGenericModel
  {
    public string Bet { get; set; }
    public string Revenue { get; set; }
  }

  // ------------------------- LEVEL 2a: USERS ---------------------

  public class GameHistoryUsersModel
  {
    public List<GameHistoryUserModel> UsersList { get; set; }

    public string TotalBet { get; set; }
    public string TotalWon { get; set; }
  }

  public class GameHistoryUserModel
  {
    public string Number { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string Bet { get; set; }
    public string Won { get; set; }
    public string CasinoRevenue
    {
      get
      {
        decimal bet = !String.IsNullOrEmpty(Bet) ? Convert.ToDecimal(Bet) : 0M;
        decimal won = !String.IsNullOrEmpty(Won) ? Convert.ToDecimal(Won) : 0M;
        return (bet - won).ToString();
      }
    }
  }

  // ------------------------- LEVEL 2b: USER POSITIONS ---------------------

  public class GameHistoryUserPositionsModel
  {
    public bool IsGenericUserPositionsAndDetailsVisible { get; set; }
    public GameHistoryGenericUserPositionsAndDetailsModel GenericUserPositionsAndDetails { get; set; }

    public bool IsBlackUserPositionsAndDetailsVisible { get; set; }
    public GameHistoryBlackJackUserPositionsAndDetailsModel BlackJackUserPositionsAndDetails { get; set; }

    public bool IsRouletteUserPositionsAndDetailsVisible { get; set; }
    public GameHistoryRouletteUserPositionAndDetailsModel RouletteUserPositionsAndDetails { get; set; }

    public bool IsVideoPokerUserPositionsAndDetailsVisible { get; set; }
    public GameHistoryVideoPokerUserPositionsAndDetailsModel VideoPokerUserPositionsAndDetails { get; set; }

    public bool IsSlotUserPositionsAndDetailsVisible { get; set; }
    public GameHistorySlotUserPositionsAndDetailsModel SlotUsersPositionsAndDetails { get; set; }

    public bool IsBaccaratUserPositionsAndDetailsVisible { get; set; }
    public GameHistoryBaccaratUserPositionsAndDetailsModel BaccaratUserPositionsAndDetails { get; set; }

    public bool IsDiceUserPositionsAndDetailsVisible { get; set; }
    public GameHistoryDiceUserPositionsAndDetailsModel DiceUserPositionsAndDetails { get; set; }

    public bool IsSlotFallUserPositionsAndDetailsVisible { get; set; }
    public GameHistorySlotFallUserPositionsAndDetailsModel SlotFallUserPositionsAndDetails { get; set; }
  }

  public class GameHistoryGenericUserPositionsAndDetailsModel
  {
    public List<GameHistoryGenericPositionModel> GenericUserPositionList { get; set; }
  }

  public class GameHistoryBlackJackUserPositionsAndDetailsModel
  {
    public List<GameHistoryBlackJackPositionModel> BlackJackUserPositionList { get; set; }
    public string TotalBet { get; set; }
    public string TotalWon { get; set; }

    public GameHistoryBlackJackResultDetailsModel BlackJackDetails { get; set; }
  }

  public class GameHistoryBaccaratUserPositionsAndDetailsModel
  {
    public GameHistoryBaccaratBetsInfoModel BaccaratBetsInfoModel { get; set; }
    public List<GameHistoryBaccaratPositionDetailModel> BaccaratInitialDeal { get; set; }
    public List<GameHistoryBaccaratPositionDetailModel> BaccaratFinalDeal { get; set; }


    //public List<GameHistoryCardResultModel> BankCardsInitialDeal { get; set; }
    //public List<GameHistoryCardResultModel> BankCardsFinalDeal { get; set; }
    //public List<GameHistoryCardResultModel> PlayerCardsInitialDeal { get; set; }
    //public List<GameHistoryCardResultModel> PlayerCardsFinalDeal { get; set; }
    //public int PlayerCardsInitialSum { get; set; }
    //public int PlayerCardsFinalSum { get; set; }
    //public int BankCardsInitialSum { get; set; }
    //public int BankCardsFinalSum { get; set; }
  }

  public class GameHistoryBonusGame
  {
    public bool IsBonusGame { get; set; } //temporary field
    public decimal? BonusWin { get; set; } //temporary field
  }

  public class GameHistoryDiceUserPositionsAndDetailsModel : GameHistoryBonusGame
  {
    public Dictionary<string, DiceResultGameType> GameHistoryDiceResultPositionModel { get; set; }
    public List<DetailedDiceValue> GameHistoryDiceDetailedValues { get; set; }

  }

  public class GameHistorySlotFallUserPositionsAndDetailsModel
  {
    public List<GameHistorySlotFallPositionModel> SlotFallUserPositionList { get; set; }
    public GameHistorySlotFallResultDetailModel SlotFallDetails { get; set; }
  }

  public class GameHistoryBaccaratBetsInfoModel
  {
    public List<GameHistoryBaccaratPositionModel> BaccaratUserPositionList { get; set; }
  }

  public class GameHistoryRouletteUserPositionAndDetailsModel
  {
    public List<GameHistoryRoulettePositionModel> RouletteUserPositionList { get; set; }
  }

  public class GameHistorySlotUserPositionsAndDetailsModel
  {
    public List<SlotUserPositionKeyValuePair> SlotUserPositionDict { get; set; }
    public GameHistorySlotResultDetailModel SlotDetails { get; set; }
  }

  public class SlotUserPositionKeyValuePair
  {
    public string Key { get; set; }
    public List<GameHistorySlotPositionModel> Value { get; set; }
  }

  public class GameHistoryVideoPokerUserPositionsAndDetailsModel : GameHistoryBonusGame
  {
    public Dictionary<string, List<GameHistoryVideoPokerPositionModel>> VideoPokerPositionList { get; set; }
    // TODO: video poker details

  }

  public class GameCardsHistoryModel
  {
    public string Position { get; set; }
    public string CardTypeId { get; set; }
    public int Sequence { get; set; }
  }

  public class GameHistoryGenericPositionModel
  {
    public string Position { get; set; }
    public int UserId { get; set; }
    public string Stake { get; set; }
    public string Bet { get; set; }
    public string Won { get; set; }
  }

  public class GameHistoryGenericGroupedPositionModel
  {
    public string Position { get; set; }
    public decimal Stake { get; set; }
    public decimal Bet { get; set; }
    public decimal Won { get; set; }
  }

  public class GameHistoryBlackjackGroupedPositionModel
  {
    public string Position { get; set; }
    public string BetType { get; set; }
    public decimal Bet { get; set; }
    public decimal Won { get; set; }
  }

  public class GameHistoryBlackJackPositionModel : GameHistoryGenericPositionModel
  {
    public string StakeType { get; set; }
  }

  public class GameHistoryRoulettePositionModel : GameHistoryGenericPositionModel
  {
    public string WinningNumber { get; set; }
  }

  public class GameHistorySlotFallPositionModel : GameHistoryGenericPositionModel
  {

  }

  public class GameHistoryBaccaratPositionModel : GameHistoryGenericPositionModel
  {
    private string _commissionPercentage = string.Empty;

    public string CasinoRevenue
    {
      get
      {
        decimal bet = !String.IsNullOrEmpty(Bet) ? Convert.ToDecimal(Bet) : 0M;
        decimal won = !String.IsNullOrEmpty(Won) ? Convert.ToDecimal(Won) : 0M;
        return (bet - won).ToString();
      }
    }

    public string CommissionPercentage
    {
      get
      {
        return (String.IsNullOrEmpty(_commissionPercentage)) ? "" : string.Format("{0:0} %", decimal.Parse(_commissionPercentage));
      }
      set
      {
        _commissionPercentage = value;
      }
    }
  }

  public class GameHistoryVideoPokerPositionModel : GameHistoryGenericPositionModel
  {
    public string DealType { get; set; } //initial, holded, final, Gamble1, Gamble2 ....
    public string GameType { get; set; } //base game, free game 1 , free game 2 ....
    public List<GameHistoryCardResultModel> Cards { get; set; } // TODO: list with details (CardResultModel)
  }

  // AGT Comment - 
  // Position data like Position1, Position2, Position3 ... have been hard coded. With CT code we could not support more than 
  // 5 visible reels. Extended this even though it is not an elegant solution to support up to 15 reels which is again hard coded. 
  public class GameHistorySlotPositionModel
  {
    public int NumberOfReels { get; set; }
    public string Position1 { get; set; }
    public string Position2 { get; set; }
    public string Position3 { get; set; }
    public string Position4 { get; set; }
    public string Position5 { get; set; }
    public string Position6 { get; set; }
    public string Position7 { get; set; }
    public string Position8 { get; set; }
    public string Position9 { get; set; }
    public string Position10 { get; set; }
    public string Position11 { get; set; }
    public string Position12 { get; set; }
    public string Position13 { get; set; }
    public string Position14 { get; set; }
    public string Position15 { get; set; }
    public List<string> Positions { get; set; }
  }


  // ------------------------- LEVEL 3: DETAILS ---------------------

  public class GameHistoryBlackJackResultDetailsModel
  {
    public GameHistoryBlackJackResultPositionModel DealerCards { get; set; }
    public List<GameHistoryBlackJackResultPositionModel> PositionsWithCards { get; set; }
  }

  public class GameHistoryBlackJackResultPositionModel
  {
    public string User { get; set; }
    public string Position { get; set; }
    public string PositionHeader { get; set; }
    public List<GameHistoryCardResultModel> Cards { get; set; }
    public string CardsSum { get; set; }
    public bool IsBlackJackSum { get; set; }
  }

  public class GameHistoryCardResultModel
  {
    public string Sequence { get; set; }
    public string CardName { get; set; }
    public string CardUrl { get; set; }
    public string Decision { get; set; }
    public string Position { get; set; } // added Position 1-5
    public int CardValue { get; set; }
    public static GameHistoryCardResultModel Copy(GameHistoryCardResultModel aModel)
    {
      if (aModel == null)
      {
        return null;
      }

      GameHistoryCardResultModel newModel = new GameHistoryCardResultModel { Sequence = aModel.Sequence, CardName = aModel.CardName, CardUrl = aModel.CardUrl, CardValue = aModel.CardValue, Decision = aModel.Decision, Position = aModel.Position };
      return newModel;
    }
  }

  #region SlotFall


  public class GameHistorySlotFallResultDetailModel
  {
    public List<GameHistorySlotFallPositionDetailModel> SlotFallDetails { get; set; }
  }

  public class GameHistorySlotFallPositionDetailModel
  {
    public bool IsSelectOneGame { get; set; }
    public string SelectOneText { get; set; }
    public GameHistorySlotFallModel InitialGame { get; set; }
    public List<GameHistorySlotFallModel> AvalancheList { get; set; }
  }

  public class GameHistorySlotFallModel
  {
    public PositionSymbol[][] SymbolsList { get; set; }
    public string BetType { get; set; }
    public decimal TotalWin { get; set; }
  }


  public class Symbol
  {

    public string Id
    {
      get;
      set;
    }
    public Symbol()
    {
    }
    public Symbol(Symbol other)
    {
      Id = other.Id;
    }
  }


  public class PositionSymbol : Symbol
  {
    /// <summary>
    /// Name of the symbol (ex. WC, FGWC, KI, JA...)
    /// </summary>
    public string Name
    {
      get;
      set;
    }
    public string ClassName
    {
      get;
      set;
    }
    /// <summary>
    /// Is symbol scatter symbol
    /// </summary>
    public bool Scatter
    {
      get;
      set;
    }
    /// <summary>
    /// Is symbol wild symbol
    /// </summary>
    public bool Wild
    {
      get;
      set;
    }
    /// <summary>
    /// Multiplier to apply if symbol is in won combo (for scatters, wilds)
    /// </summary>
    public int? Multiplier
    {
      get;
      set;
    }
    public PositionSymbol()
    {
    }
    public PositionSymbol(PositionSymbol other)
    {
      Name = other.Name;
      Scatter = other.Scatter;
      Wild = other.Wild;
      Multiplier = other.Multiplier;
      ClassName = other.ClassName;
    }
    public override string ToString()
    {
      return "Id: " + Id + ",Name: " + Name + ",Scatter: " + Scatter + ",Wild: " + Wild + ",Multiplier: " + Multiplier + ",Class: " + ClassName;
    }
  }

  #endregion

  public class GameHistorySlotResultDetailModel
  {

    public List<GameHistorySlotPositionDetailModel> SlotDetails { get; set; }
  }

  public class GameHistorySlotPositionDetailModel
  {

    public string Position { get; set; }
    public string BetType { get; set; }
    public string Bet { get; set; }
    public string VirtualBet { get; set; }
    public string Won { get; set; }
    public string VirtualWon { get; set; }
    public string PositionDetails { get; set; }
    //position details properties
    public string Type { get; set; }
    public string WonOutcome { get; set; }
    public string PositionOutcome { get; set; }
    public string Symbols { get; set; }
    public string Details { get; set; }
    public string WinDetails { get; set; }
    public string BetDetail { get; set; }
    [JsonProperty("EnabledReels")]
    public List<KeyValuePair> EnabledReels { get; set; }
  }

  public class KeyValuePair
  {
    public string Key { get; set; }
    public string Value { get; set; }
  }

  public class GameHistoryBaccaratPositionDetailModel
  {
    public string Position { get; set; }
    public GameHistoryCardResultModel Card1 { get; set; }
    public GameHistoryCardResultModel Card2 { get; set; }
    public GameHistoryCardResultModel Card3 { get; set; }
    public string Sum { get; set; }
  }

  public class DiceResultGameType
  {
    public Dictionary<string,DiceBox> DiceBoxes { get; set; }
    public string Bet { get; set; }
    public string Won { get; set; }

    public DetailedDiceValue DetailedValues { get; set; }
  }

  public class DetailedDiceValue
  {
      public int StepNumber { get; set; }
      public string DecisionId { get; set; }
      public int SelectedBox { get; set; }

      public string Dice1 { get; set; }
      public string Dice2 { get; set; }
      public string Dice3 { get; set; }
     
  }

  public class DiceBox
  {
    public Dictionary<string, List<string>> DiceValues { get; set; }
  }
  //helper

  public class DiceResultPositionDetailsModelHelper
  {
    public List<DiceHistoryHelper> GameHistoryDiceResultPositionModelHelper { get; set; }

    public static implicit operator DiceResultPositionDetailsModelHelper(Dictionary<string, List<string>> ghDictionary)
    {
      DiceResultPositionDetailsModelHelper ret = new DiceResultPositionDetailsModelHelper();
      ret.GameHistoryDiceResultPositionModelHelper = new List<DiceHistoryHelper>();
      List<DiceHistoryHelper> dctList = new List<DiceHistoryHelper>();
      foreach (KeyValuePair<string, List<string>> item in ghDictionary)
      {
        List<string> tmpList = new List<string>();
        for (int i = 0; i < ghDictionary.Count; i++)
        {
          tmpList.Add(ghDictionary[i.ToString()].ElementAt(Int32.Parse(item.Key)));
        }
        dctList.Add(tmpList);
      }
      ret.GameHistoryDiceResultPositionModelHelper = dctList;
      return ret;
    }
  }

  public class DiceHistoryHelper
  {
    public string DiceValue1 { get; set; }
    public string DiceValue2 { get; set; }
    public string DiceValue3 { get; set; }

    public static implicit operator DiceHistoryHelper(List<string> ghList)
    {
      DiceHistoryHelper ret = new DiceHistoryHelper();
      ret.DiceValue1 = ghList.ElementAt(0);
      ret.DiceValue2 = ghList.ElementAt(1);
      ret.DiceValue3 = ghList.ElementAt(2);
      return ret;
    }

  }
}
