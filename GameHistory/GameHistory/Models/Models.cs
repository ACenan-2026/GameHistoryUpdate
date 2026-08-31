using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

namespace GameHistory.Models
{
    public class BaseResponse
    {
      [JsonProperty("ErrorMessage")]
      public string ErrorMessage { get; set; }
      [JsonProperty("StatusCode")]
      public string StatusCode { get; set; }
    }
    
    public class GameHistoryResponse : BaseResponse
    {
      [JsonProperty("GameHistory")]
      public PlayerActivityLogModel[] GameHistoryMember { get; set; }
    }
  
    public class GameHistoryDetailsResponse : BaseResponse
    {
      [JsonProperty("GameHistoryDetails")]
      public GameHistoryGameInfoModel GameHistoryDetailsMember { get; set; }
    }

    public class IsSessionAliveResponse : BaseResponse
    {
      public bool IsSessionAlive { get; set; }
      // For integration where username is not sent as game launch parameter
      public string UserName { get; set; }

    }
}