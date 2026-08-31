/* ============================================================================
|
|  Copyright (c) 2010 ComTrade d.o.o. All rights reserved.
|
|  Posession  of this  software does not  grant any  rights to  use, reproduce,
|  modify  or distribute it or to use any concept it may contain.
|
|  Licensed under ComTrade d.o.o. license ("the License"); you may not use
|  this software unless in compliance with the License. Any use of the software
|  without such license is a violation of copyright  laws and may be subject to
|  legal actions (remedies and/or criminal prosecution).
|
|  NOTE:
|   If  you receive this  content in  error, please  let us know  by contacting
|   ComTrade d.o.o. legal  department (legal-department@comtrade.com) and destroy any copy
|   you may have.
|
+=============================================================================*/
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameHistory.Models
{
  public class PlayerActivityLogModel
  {
    public Int64 GameId { get; set; }
    public string GameName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? StopTime { get; set; }
    public int ConfiguredContent { get; set; }
    public string ConfiguredContentName { get; set; }
    public decimal Stake { get; set; }
    public string Won { get; set; }
    public decimal? Lost { get; set; }
    public string DisplayName { get; set; }
    public Guid Identifier { get; set; }
    public string ExtGameIdentifier { get; set; }
    public bool Voided { get; set; }
    public int ContentTypeId { get; set; }

    public string FormatedStartTime { get; set; }
    public string FormatedStopTime { get; set; }
    
    public string StartBalance { get; set; }
    public string EndBalance { get; set; }
    public decimal StartBonusBalance { get; set; }
    public decimal EndBonusBalance { get; set; }
    public string Currency { get; set; }
  }
}
