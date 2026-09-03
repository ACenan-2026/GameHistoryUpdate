using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using System.Text;
using System.Net;

using GameHistory.Helpers;
using System.IO;
using GameHistory.Models;
using GameHistory.MultiplierRecompute;
using System.Runtime.Serialization.Formatters;
using System.Web.Script.Serialization;
using System.Globalization;
using System.Data;
using System.Web.UI.WebControls;
using log4net;
using System.Xml;

namespace GameHistory.Controllers
{
    public class HomeController : Controller
    {

        private static ILog sLog = LogManager.GetLogger(typeof(HomeController));
        private static bool isBusy = false;
        private XmlNodeList currencyNodeList = null;

        // ===== DEPLOY PIPELINE TEST MARKER — safe to delete after verifying =====
        // Browse to  /Home/Ping  on the local test site. Bump the "v1" text,
        // publish, then refresh: if the new text appears, source changes are
        // flowing through compile -> GameHistory.dll -> wwwroot correctly.
        [AcceptVerbs(HttpVerbs.Get)]
        public ActionResult Ping()
        {
            return Content("GameHistory deploy test — marker v1 — served " + DateTime.Now);
        }
        // =======================================================================

        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public ActionResult Index(string extUserId, string extGameId, string dateFrom = null, string dateTo = null, string pageSize = "10", string page = "1", string sessionId = null, string  operatorId = null, string platform = "Desktop", string clientType = null)
        {
        if (isBusy)
            {
                return View("BusyPage", (object)clientType);
            }
            else
            {
                isBusy = true;
            }
            {
                sLog.DebugFormat("Index with extUserId : {0}, extGameId : {1}, dateFrom: {2}, dateTo: {3}, pageSize: {4}, page : {5}, sessionId : {6} operatorId : {7} invoked.",
                  extUserId, extGameId, dateFrom, dateTo, pageSize, page, sessionId, operatorId);
            }

            GameHistory.Models.GameHistoryResponse x = null;
            try
            {
                string platformProvider = ConfigurationManager.AppSettings["PlatformProvider"];

                string dateFormat = ConfigurationManager.AppSettings["DateFormatCultureCode"];

                if (GetSessionChecked(sessionId, ref extUserId))
                {
                    sLog.Debug("Session is checked.");


                if (string.IsNullOrEmpty(dateFrom) || !dateFrom.IsISO8601())
                {
                    dateFrom = DateTime.Now.AddDays(- PageConfiguration.NumberOfDaysOfHistory).ConvertDateTimeToISO8601();
                    if (sLog.IsDebugEnabled)
                    {
                        sLog.DebugFormat("Set dateFrom: {0}.",
                          dateFrom);
                    }
                }
                if (string.IsNullOrEmpty(dateTo) || !dateTo.IsISO8601())
                {
                    dateTo = DateTime.Now.ConvertDateTimeToISO8601();
                    if (sLog.IsDebugEnabled)
                    {
                        sLog.DebugFormat("Set dateTo: {0}.",
                          dateTo);
                    }
                }

                string url = string.Format("{0}/getGameHistory", PageConfiguration.RGSAgentUrl);

                if (sLog.IsDebugEnabled)
                {
                    sLog.DebugFormat("Set URL: {0}.",
                      url);
                }

                // AGT Comment- Opening an XML file and creating a list of currency nodes
                if (currencyNodeList == null)
                {
                    CreateNodeList();
                }
                // AGT Comment - pageSize have to be > 0
                if (pageSize.Equals("0"))
                {
                    pageSize = "10";
                }

                var dataObject = new { extUserId = extUserId, extGameId = extGameId, operatorId = operatorId, platformProvider = platformProvider, dateFrom = dateFrom, dateTo = dateTo, pageSize = pageSize, pageNumber = page };

                string data = MakePOSTRequest(url, dataObject).ToString();

                x = JsonConvert.DeserializeObject<GameHistory.Models.GameHistoryResponse>(data);

                // AGT Comment - StartBalance, EndBalance and Won values are appended with appropriate currency symbols. 
                // Also updated to display up to 2 decimal points
                if (x != null)
                {
                    string lastCurrencyCode = null;
                    string currencySymbol = null;

                    for (int idx = 0; idx < x.GameHistoryMember.Count(); idx++)
                    {

                        if (x.GameHistoryMember[idx].StartBalance == "0")
                        {
                            x.GameHistoryMember[idx].StartBalance = "Incomplete Game";
                            x.GameHistoryMember[idx].EndBalance = "N/A";
                            x.GameHistoryMember[idx].Won = "N/A";
                        }
                        else
                        {
                            if (lastCurrencyCode != x.GameHistoryMember[idx].Currency)
                            {
                                currencySymbol = getCurrencySymbol(x.GameHistoryMember[idx].Currency);
                                lastCurrencyCode = x.GameHistoryMember[idx].Currency;
                            }

                            x.GameHistoryMember[idx].StartBalance = currencySymbol + String.Format("{0:0.00}", Convert.ToDecimal(x.GameHistoryMember[idx].StartBalance));
                            x.GameHistoryMember[idx].EndBalance = currencySymbol + String.Format("{0:0.00}", Convert.ToDecimal(x.GameHistoryMember[idx].EndBalance));
                        }

                        if (x.GameHistoryMember[idx].Won.Equals("N/A") == false)
                        {
                            x.GameHistoryMember[idx].Won = String.Format("{0:0.00}", Convert.ToDecimal(x.GameHistoryMember[idx].Won));
                        }


                        if (String.IsNullOrEmpty(dateFormat) == false)
                        {
                            x.GameHistoryMember[idx].FormatedStartTime = x.GameHistoryMember[idx].StartTime.ToString("g", CultureInfo.CreateSpecificCulture(dateFormat));
                            if (x.GameHistoryMember[idx].StopTime != null)
                            {
                                x.GameHistoryMember[idx].FormatedStopTime = ( (DateTime)x.GameHistoryMember[idx].StopTime).ToString("g", CultureInfo.CreateSpecificCulture(dateFormat));
                            }
                            
                        }
                        else
                        {
                            x.GameHistoryMember[idx].FormatedStartTime = x.GameHistoryMember[idx].StartTime.ToString("g");

                            if (x.GameHistoryMember[idx].StopTime != null)
                            {
                                x.GameHistoryMember[idx].FormatedStopTime = ((DateTime)x.GameHistoryMember[idx].StopTime).ToString("g");
                            }
                        }

                    }
                }
            }

                if (x == null)
                {
                    //Since this method is always called from a game session should be always alive. In normal scenario we should never come here.
                    sLog.ErrorFormat("Error in Index with extUserId: {0}, extGameId: {1}, dateFrom: {2}, dateTo: {3}, pageSize: {4}, page: {5}. Session is not alive.",
                      extUserId, extGameId, dateFrom, dateTo, pageSize, page);
                    //throw new ApplicationException("Authentication failed.");
                    isBusy = false;
                    return View("ErrorPage", (object)clientType);
                }
                isBusy = false;
                return View("GameHistoryLog", x.GameHistoryMember);
            }
            catch(Exception e)
            {
                isBusy = false;
                sLog.ErrorFormat(e.Message);
                return View("ErrorPage", (object)clientType);
            }
        }

        // AGT Comment - platformType is passed from client to load the symbols depending on what platform the client is loaded on
        public ActionResult GameHistoryDetails(string id, string identifier, string sessionId = null, string extUserId = null, string platformType = "Desktop")
        {
            if (sLog.IsDebugEnabled)
            {
                sLog.DebugFormat("GameHistoryDetails with id: {0}, identifier: {1} invoked.", id, identifier);
                sLog.Debug("Platform type sent from client is : " + platformType);
            }

            if (isBusy)
            {
                return View("BusyPage", (object)("HTML"));
            }
            else
            {
                isBusy = true;
            }

            try
            {
                GameHistoryDetailsResponse data = null;
                if (GetSessionChecked(sessionId, ref extUserId))
                {
                    if (sLog.IsDebugEnabled)
                    {
                        sLog.Debug("Session is checked.");
                    }

                string url = string.Format("{0}/getGameHistoryDetails?gameId={1}&gameIdentifier={2}", PageConfiguration.RGSAgentUrl, id, identifier);
                string json = MakePOSTRequest(url, null).ToString();

                data = (GameHistoryDetailsResponse)JsonConvert.DeserializeObject(json, typeof(GameHistoryDetailsResponse), new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.All,
                    TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple
                });


                // Current game is not completed
                if (data.GameHistoryDetailsMember.GameHistoryGameInfoSlotModel.StopTime == null)
                {
                    return PartialView("GameInRestore", null);
                }
                if (sLog.IsDebugEnabled)
                {
                    sLog.Debug("Game Name in content from where the symbols are read: " + data.GameHistoryDetailsMember.GameHistoryGameInfoSlotModel.GameName);
                }
                string html = "";
                // AGT Comment - WinCombo symbols are replaced with reel stop position symbols
                if (data.GameHistoryDetailsMember.GameHistoryGameInfoSlotModel != null && data.GameHistoryDetailsMember.UserPositions.SlotUsersPositionsAndDetails != null)
                {
                    data.GameHistoryDetailsMember.GameHistoryGameInfoSlotModel.Symbols = getSymbols(data.GameHistoryDetailsMember);

                    // Multiplier recompute: compute the finalised amounts (and run the log-only validation). The
                    // returned context drives the amount overlay in the tile loop below; null => render plain symbols.
                    string gameName = data.GameHistoryDetailsMember.GameHistoryGameInfoSlotModel.GameName;
                    MultiplierOverlayContext multiplierCtx = PrepareMultiplierData(data.GameHistoryDetailsMember);

                    #region Replace symbol names with symbol images
                    int counter = 0;
                    foreach (var slotDetailsItem in data.GameHistoryDetailsMember.UserPositions.SlotUsersPositionsAndDetails.SlotDetails.SlotDetails)
                    {
                        html = "";

                        if (counter < data.GameHistoryDetailsMember.GameHistoryGameInfoSlotModel.Symbols.Count())
                        {
                            SlotSymbolTableViewModel positionItem = data.GameHistoryDetailsMember.GameHistoryGameInfoSlotModel.Symbols[counter];
                            html += "<table align=\"center\">";
                            html += "<tr>";

                            foreach (var reelItem in positionItem.Reels)
                            {
                                html += "<td>";
                                html += "<table>";
                                foreach (var floorItem in reelItem.Floors)
                                {
                                    html += "<tr margin=\"2px 2px 2px 2px\">";
                                    // Figuring out where the symbol images are stored based on the platform type sent from client
                                    string symbolUrl = Url.Content(floorItem.SymbolName.ToSlotSymbolUrl(gameName, platformType));
                                    // For a configured multiplier symbol the finalised amount is overlaid on the tile;
                                    // every other symbol renders exactly as before.
                                    html += BuildMultiplierTile(symbolUrl, floorItem.SymbolName, multiplierCtx);
                                    html += "</tr>";
                                    html += "<br/>";
                                }
                                html += "</table>";
                                html += "</td>";
                            }

                            html += "</tr>";
                            html += "</table>";

                            slotDetailsItem.Symbols = html;

                            // AGT Comment - Update Bet field
                            slotDetailsItem.BetDetail = slotDetailsItem.Bet;
                            if (slotDetailsItem.Position.Equals("ADDITIONAL_GAME"))
                            {
                                if (slotDetailsItem.Type.Contains("CHOICE GAME"))
                                {
                                    slotDetailsItem.BetDetail = "Choice Game";
                                }
                                else
                                {
                                    slotDetailsItem.BetDetail = "Free Game";
                                }
                            }

                            // AGT Comment - Type is replaced with the combination of type, position outcome and won outcome
                            if (slotDetailsItem.Type != null && !slotDetailsItem.Type.Equals("") && !slotDetailsItem.Type.Equals("N/A"))
                            {
                                string tempType = slotDetailsItem.Type.Replace("<br/>", "|");
                                string tempPositionOutcome = slotDetailsItem.PositionOutcome.Replace("<br/>", "|");
                                string tempWonOutcome = slotDetailsItem.WonOutcome.Replace("<br/>", "|");

                                string[] typeList = tempType.Split('|');
                                string[] positionList = tempPositionOutcome.Split('|');
                                string[] wonList = tempWonOutcome.Split('|');

                                if (typeList.Length == positionList.Length && typeList.Length == wonList.Length)
                                {
                                    int count = 0;
                                    slotDetailsItem.WinDetails = "";
                                    while (count < typeList.Length - 1)
                                    {
                                        if (typeList[count].Equals("FreeGame"))
                                        {
                                            // Free Games are currently not logged
                                        }
                                        else if (checkForZerostring(wonList[count]) == false) // Only show the wins that Pays
                                        {
                                            if (typeList[count].Equals("Payline"))
                                            {
                                                slotDetailsItem.WinDetails += "Line ";
                                                slotDetailsItem.WinDetails += positionList[count];
                                            }
                                            else if (typeList[count].Equals("Scatter"))
                                            {
                                                slotDetailsItem.WinDetails += "Scatter ";
                                            }

                                            slotDetailsItem.WinDetails += " Won " + wonList[count] + "<br/>";
                                        }

                                        ++count;
                                    }
                                }
                            }
                            else
                            {
                                slotDetailsItem.WinDetails = "N/A";
                            }

                            counter++;

                        }

                        if (!string.IsNullOrEmpty(slotDetailsItem.Details) && slotDetailsItem.Details.Contains("GambleOutcome") && slotDetailsItem.Details.Contains("SelectedGamble"))
                        {
                            html = "";
                            html += "<table>";
                            html += "<tr>";
                            int gambleOutcomeIndex = slotDetailsItem.Details.IndexOf("GambleOutcome=");
                            string GambleOutcome = slotDetailsItem.Details.Substring(gambleOutcomeIndex + 14, 1);

                            int selectedGambleIndex = slotDetailsItem.Details.IndexOf("SelectedGamble=");
                            string SelectedGamble = slotDetailsItem.Details.Substring(selectedGambleIndex + 15, 1);

                            html += "<td>";
                            html += "Selection";
                            html += "</td>";

                            html += "<td>";
                            html += string.Format(" <img src=\"{0}\" />", Url.Content(SelectedGamble.ToCardSymbolUrl()));
                            html += "</td>";
                            html += "</tr>";

                            html += "<td>";
                            html += "Outcome";
                            html += "</td>";

                            html += "<td>";
                            html += string.Format(" <img src=\"{0}\" />", Url.Content(GambleOutcome.ToCardSymbolUrl()));
                            html += "</td>";
                            html += "</tr>";

                            html += "</table>";
                            slotDetailsItem.Symbols = html;

                            // AGT Comment - Update slotDetailsItem details so that it can be presented to player in simpler format
                            slotDetailsItem.BetDetail = slotDetailsItem.VirtualBet;
                            slotDetailsItem.Won = slotDetailsItem.VirtualWon;
                            if (checkForZerostring(slotDetailsItem.Won))
                            {
                                slotDetailsItem.WinDetails = "Gamble Lost";
                            }
                            else
                            {
                                slotDetailsItem.WinDetails = "Gamble Won: " + slotDetailsItem.VirtualWon;
                            }
                        }
                    }
                    #endregion

                    isBusy = false;

                        return PartialView("GameHistoryDetails", data.GameHistoryDetailsMember);
                    }
                    else
                    {
                        isBusy = false;
                        return PartialView("InvalidData", null);
                    }
                }
                if (data == null)
                {
                    //Since this method is always called from a game session should be always alive. In normal scenario we should never come here.
                    sLog.ErrorFormat("Error in GetHistoryDetails with id: {0}, identifier: {1}. Session is not alive.",
                      id,
                      identifier);
                    isBusy = false;
                    throw new ApplicationException("Authentication failed.");
                }
                isBusy = false;
                return PartialView("GameHistoryDetails", data.GameHistoryDetailsMember);
            }
            catch(Exception e)
            {
                isBusy = false;
                sLog.ErrorFormat(e.Message);
                return PartialView("InvalidData", null);
            }
        }

        #region Private Methods

        private object MakePOSTRequest(string url, object data)
        {
            if (sLog.IsDebugEnabled)
            {
                sLog.DebugFormat("MakePOSTRequest with url: {0}, data: {1} invoked.", url, data);
            }
            string returnValue;


            string postData = JsonConvert.SerializeObject(data);
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);

            req.Method = "POST";

            req.Headers.Add("username", PageConfiguration.RGSAgentUsername);
            req.Headers.Add("password", PageConfiguration.RGSAgentPassword);


            req.ContentLength = byteArray.Length;
            req.ContentType = "application/json; charset=UTF-8";
            try
            {

                Stream dataStream = req.GetRequestStream();
                if (byteArray != null)
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }

                HttpWebResponse resp = req.GetResponse() as HttpWebResponse;

                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    sLog.Debug("Web response is ok.");
                    using (Stream respStream = resp.GetResponseStream())
                    {
                        StreamReader reader = new StreamReader(respStream, Encoding.UTF8);

                        returnValue = reader.ReadToEnd();
                        if (sLog.IsDebugEnabled)
                        {
                            sLog.DebugFormat("MakePOSTRequest finished with {0}.", returnValue);
                        }
                        return returnValue;
                    }
                }
            }
            catch (WebException e)
            {
                sLog.ErrorFormat("Error in MakePOSTRequest with url: {0}, data: {1}. {2}", url, data, e);
                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
                    using (Stream responseData = response.GetResponseStream())
                    using (var reader = new StreamReader(responseData))
                    {
                        string text = reader.ReadToEnd();
                        Console.WriteLine(text);
                    }
                }
            }

            return null;
        }


        /// <summary>
        /// Holds everything the render loop needs to overlay finalised multiplier amounts onto the outcome tiles:
        /// the symbol -> params mapping (for the paid flag) and the symbol -> computed_amount map. Also carries
        /// whether unpaid (TB) multipliers should be overlaid. Produced once per game round by
        /// <see cref="PrepareMultiplierData"/>; null means "render the plain symbols as before".
        /// </summary>
        private sealed class MultiplierOverlayContext
        {
            public bool IncludeUnpaid { get; set; }
            public MultiplierSymbolMapping Mapping { get; set; }
            public IReadOnlyDictionary<string, decimal> Computed { get; set; }
        }

        /// <summary>
        /// Multiplier-recompute feature entry point for the details view. Resolves this game's history config
        /// (wwwroot\GameConfig\<GameName>\<GameName>_history.xml), computes the finalised multiplier amounts,
        /// runs the LOG-ONLY Phase 1 validation (cross-checks computed vs. recorded located-scatter wins and logs
        /// any divergence), and returns a context the render loop uses to overlay those amounts onto the outcome
        /// tiles. Wrapped so any failure is non-fatal to the history page: on error/disabled/no-config it returns
        /// null and the tiles render exactly as before.
        ///
        /// Gated by the "MultiplierRecompute.Enabled" appSetting (off unless explicitly set to true).
        /// Overlay scope is controlled by "MultiplierRecompute.OverlayIncludesUnpaid": false (default) overlays
        /// only paid (B) located-scatter multipliers; true also overlays unpaid (TB) ones.
        /// </summary>
        private MultiplierOverlayContext PrepareMultiplierData(GameHistoryGameInfoModel member)
        {
            try
            {
                bool.TryParse(ConfigurationManager.AppSettings["MultiplierRecompute.Enabled"], out bool enabled);
                if (!enabled)
                {
                    return null;
                }

                string gameName = member?.GameHistoryGameInfoSlotModel?.GameName;
                if (string.IsNullOrEmpty(gameName))
                {
                    return null;
                }

                // Resolve the GameConfig root. Prefer the explicit "MultiplierRecompute.GameConfigRoot" appSetting
                // (an absolute path, or an app-relative "~/..." path) so it works whether the app runs from the
                // deployed wwwroot copy or straight from the source project. If unset, fall back to the historical
                // assumption that GameConfig is a sibling of the app root (...\wwwroot\GameConfig for ...\wwwroot\GameHistory).
                string gameConfigRoot = ResolveGameConfigRoot();
                if (gameConfigRoot == null)
                {
                    return null;
                }
                string configPath = Path.Combine(gameConfigRoot, gameName, gameName + "_history.xml");
                if (!System.IO.File.Exists(configPath))
                {
                    if (sLog.IsDebugEnabled)
                    {
                        sLog.DebugFormat("No multiplier config for game '{0}' at {1}; skipping overlay/validation.", gameName, configPath);
                    }
                    return null;
                }

                IMultiplierConfigParser parser = new MultiplierConfigParser(configPath);
                MultiplierSymbolMapping mapping = parser.GetMultiplierParams();
                
                // maps multiplier name to finalised amount
                IReadOnlyDictionary<string, decimal> computed = new WonAmountsComputer(parser).ComputeScatterAmounts(member);

                if (sLog.IsDebugEnabled)
                {
                    foreach (var kv in computed)
                    {
                        sLog.DebugFormat("Computed multiplier {0} -> {1} for game '{2}'.", kv.Key, kv.Value, gameName);
                    }
                }

                // Phase 1 validation stays log-only; it never alters what the loop below renders.
                new MultiplierComputationValidator().ValidateRound(member, mapping, computed);

                bool.TryParse(ConfigurationManager.AppSettings["MultiplierRecompute.OverlayIncludesUnpaid"], out bool includeUnpaid);

                return new MultiplierOverlayContext
                {
                    IncludeUnpaid = includeUnpaid,
                    Mapping = mapping,
                    Computed = computed
                };
            }
            catch (Exception ex)
            {
                // The multiplier feature must never take down the history page; degrade to plain symbols.
                sLog.ErrorFormat("Multiplier overlay/validation failed (non-fatal): {0}", ex);
                return null;
            }
        }

        /// <summary>
        /// Resolves the absolute folder that contains the per-game history configs
        /// (<root></root><GameName></GameName><GameName></GameName>_history.xml). Order of preference:
        ///  1. The "MultiplierRecompute.GameConfigRoot" appSetting — an absolute path (e.g. C:\inetpub\wwwroot\GameConfig)
        ///     or an app-relative "~/..." path (resolved via Server.MapPath). Use this whenever the app does not run
        ///     from the deployed wwwroot copy (e.g. IIS Express / VS debugging against the source project).
        ///  2. Fallback: GameConfig as a sibling of the app root (works for the deployed ...\wwwroot\GameHistory app).
        /// Returns null if neither can be resolved.
        /// </summary>
        private string ResolveGameConfigRoot()
        {
            string configured = ConfigurationManager.AppSettings["MultiplierRecompute.GameConfigRoot"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                configured = configured.Trim();
                // Allow an app-relative path, though GameConfig normally lives outside the app.
                return configured.StartsWith("~") ? Server.MapPath(configured) : configured;
            }

            string appRoot = Server.MapPath("~");                       // ...\wwwroot\GameHistory (when deployed)
            string parent = Directory.GetParent(appRoot)?.FullName;     // ...\wwwroot
            return parent == null ? null : Path.Combine(parent, "GameConfig");
        }

        /// <summary>
        /// Builds the HTML for a single outcome tile. For a configured multiplier symbol that is in scope
        /// (paid, or unpaid when OverlayIncludesUnpaid is set) and has a computed amount, the finalised amount is
        /// overlaid on top of the symbol artwork; otherwise the plain symbol image is returned unchanged.
        /// <paramref name="symbolUrl"/> must already be resolved via Url.Content.
        /// </summary>
        private static string BuildMultiplierTile(string symbolUrl, string symbolName, MultiplierOverlayContext ctx)
        {
            MultiplierParams p;
            // Fallback in case the symbol is not in the mapping or has no computed amount: render the plain symbol image.
            decimal amount;
            if (ctx == null
                || string.IsNullOrEmpty(symbolName)
                || !ctx.Mapping.TryGet(symbolName, out p)
                || (!p.Paid && !ctx.IncludeUnpaid)
                || !ctx.Computed.TryGetValue(symbolName, out amount))
            {
                return "<img src=\"" + symbolUrl + "\" >";
            }

            string text = HttpUtility.HtmlEncode(FormatOverlayAmount(amount));
            var sb = new StringBuilder();
            sb.Append("<span style=\"position:relative; display:inline-block; line-height:0;\">");
            sb.Append("<img src=\"").Append(symbolUrl).Append("\" >");
            sb.Append("<span style=\"position:absolute; top:50%; left:50%; transform:translate(-50%,-50%); ")
              .Append("font-family:Arial,Helvetica,sans-serif; font-weight:bold; font-size:18px; ")
              .Append("color:#FFFFFF; text-shadow:-1px -1px 0 #000,1px -1px 0 #000,-1px 1px 0 #000,1px 1px 0 #000,0 0 3px #000; ")
              .Append("white-space:nowrap; pointer-events:none;\">");
            sb.Append(text);
            sb.Append("</span></span>");
            return sb.ToString();
        }

        /// <summary>
        /// Formats a finalised multiplier amount for display on a tile: no currency symbol, trailing zeros trimmed
        /// (e.g. 200, 25, 5.5), invariant culture for a stable decimal point.
        /// </summary>
        private static string FormatOverlayAmount(decimal amount)
        {
            return amount.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private SlotSymbolTableViewModel[] getSymbols(GameHistoryGameInfoModel gameInfoModel)
        {
            if (sLog.IsDebugEnabled)
            {
                sLog.DebugFormat("getSymbols with gameInfoModel: {0} invoked.", gameInfoModel);
            }
            int NumberOfStopPositions = gameInfoModel.UserPositions.SlotUsersPositionsAndDetails.SlotUserPositionDict.Count();

            SlotSymbolTableViewModel[] result = new SlotSymbolTableViewModel[NumberOfStopPositions];

            for (int a = 0; a < NumberOfStopPositions; a++)
            {
                result[a] = new SlotSymbolTableViewModel();
                int down = 0;
                if (gameInfoModel.UserPositions.SlotUsersPositionsAndDetails.SlotUserPositionDict.ElementAt(a).Value.Count > 0)
                {
                    down = gameInfoModel.UserPositions.SlotUsersPositionsAndDetails.SlotUserPositionDict.ElementAt(a).Value.ElementAt(0).Positions.Count;
                }

                result[a].Reels = new List<SlotSymbolReelViewModel>();
                for (int i = 0; i < down; i++)
                {
                    result[a].Reels.Add(new SlotSymbolReelViewModel(i.ToString()) { Floors = new List<SlotSymbolViewModel>() });
                }

                for (int i = 0; i < gameInfoModel.UserPositions.SlotUsersPositionsAndDetails.SlotUserPositionDict.ElementAt(a).Value.Count; i++)
                {
                    for (int j = 0; j < down; j++)
                    {
                        string tmpSymbolName = gameInfoModel.UserPositions.SlotUsersPositionsAndDetails.SlotUserPositionDict.ElementAt(a).Value.ElementAt(i).Positions.ElementAt(j);

                        if (!String.IsNullOrEmpty(tmpSymbolName))
                        {
                            result[a].Reels[j].Floors.Add(new SlotSymbolViewModel() { SymbolName = tmpSymbolName });
                        }
                      
                    }
                }
            }
            sLog.Debug("getSymbols finished.");
            return result;
        }

        private bool IsSessionAlive(string sessionId, ref string extUserId)
        {
            if (sLog.IsDebugEnabled)
            {
                sLog.DebugFormat("IsSessionActive with sessionId: {0}, extUserId: {1} invoked.", sessionId, extUserId);
            }
            bool result = false;
            if (!string.IsNullOrEmpty(sessionId))
            {
                sLog.Debug("We have session Id.");
                string url = string.Format("{0}/isSessionAlive", PageConfiguration.RGSAgentUrl);
                var dataObject = new { sessionId = sessionId, username = extUserId };
                object o = MakePOSTRequest(url, dataObject);
                IsSessionAliveResponse isa = JsonConvert.DeserializeObject<GameHistory.Models.IsSessionAliveResponse>((string)o);
                if (isa != null)
                {
                    sLog.Debug("We have IsSessionAliveResponse");
                    result = isa.IsSessionAlive;
                    // For integration where username is not passed as a launch parameter
                    if(extUserId == "")
                    {
                        extUserId = isa.UserName;
                    }
                }
            }

            if (sLog.IsDebugEnabled)
            {
                sLog.DebugFormat("IsSessionActive finished with: {0}.", result);
            }
            return result;
        }

        private string GetIncommingHeaderValue(string key)
        {
            if (sLog.IsDebugEnabled)
            {
                sLog.DebugFormat("GetIncommingHeaderValue with key {0} invoked.", key);
            }
            string result = null;
            string[] s = Request.Headers.GetValues(key);
            if (s != null && s.Length == 1)
            {
                result = s[0];
            }
            if (sLog.IsDebugEnabled)
            {
                sLog.DebugFormat("GetIncommingHeaderValue finished with {0}", result);
            }
            return result;
        }

        private bool GetSessionChecked(string sessionId, ref string extUserId)
        {
            if (sLog.IsDebugEnabled)
            {
                sLog.DebugFormat("GetSessionChecked with sessionId:{0}, extUserId: {1} invoked.", sessionId, extUserId);
            }
            bool sessionChecked = !PageConfiguration.CheckSession;
            if (!sessionChecked)
            {
                sLog.Debug("Check session.");
                sessionChecked = IsSessionAlive(sessionId, ref extUserId);
            }
            if (sLog.IsDebugEnabled)
            {
                sLog.DebugFormat("GetSessionChecked finished with {0}.", sessionChecked);
            }
            return sessionChecked;
        }

        // AGT Comment - to read the XML file for list of available currency symbols
        private void CreateNodeList()
        {
            XmlDocument xmlDoc = new XmlDocument();
            string filePath = Request.PhysicalPath + "\\CurrencyCode.xml";

            if (System.IO.File.Exists(filePath))
            {
                xmlDoc.Load(filePath);
                currencyNodeList = xmlDoc.GetElementsByTagName("CurrencyCode");
            }
        }

        // AGT Comment - To  get the currency code from the XML file currency code nodes
        private string getCurrencySymbol(string currencyCode)
        {
            string currencySymbol = null;
            int nodeCount = currencyNodeList.Count;
            while (nodeCount > 0)
            {
                if (currencyNodeList.Item(nodeCount - 1).Attributes["Name"].Value == currencyCode)
                {
                    currencySymbol = currencyNodeList.Item(nodeCount - 1).Attributes["Symbol"].Value;
                    break;
                }
                nodeCount--;
            }

            return currencySymbol;
        }

        private bool checkForZerostring(string stringToCheck)
        {
            return stringToCheck.Equals("0.00") || stringToCheck.Equals("0.0") || stringToCheck.Equals("")
                || stringToCheck.Equals(" ") || stringToCheck.Equals(null);
        }

        #endregion

    }

}
