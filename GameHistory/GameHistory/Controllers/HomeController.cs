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

                var slotRoundReader = new SlotRoundReader(data.GameHistoryDetailsMember);


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
                    string gameName = slotRoundReader.GetGameName();
                    MultiplierOverlayContext multiplierCtx = PrepareMultiplierData(slotRoundReader);

                    #region Replace symbol names with symbol images
                    int counter = 0;
                    foreach (var slotDetailsItem in slotRoundReader.GetSlotDetails())
                    {
                        html = "";

                        if (counter < data.GameHistoryDetailsMember.GameHistoryGameInfoSlotModel.Symbols.Count())
                        {
                            SlotSymbolTableViewModel positionItem = data.GameHistoryDetailsMember.GameHistoryGameInfoSlotModel.Symbols[counter];
                            html += "<table align=\"center\">";
                            html += "<tr>";

                            // For "once" placement groups, resolve up-front the single winning cell (per group)
                            // that should carry the overlay for THIS spin; every other in-group occurrence renders
                            // plain. Empty for the common case of only "all" placement groups.
                            var onceOverlayCells = ResolveOnceOverlayCells(positionItem, multiplierCtx);

                            // Resolve up-front which occurrences this spin actually paid (matched against the
                            // recorded located-scatter wins). Computed for EVERY round now, not just when gating
                            // is on: the tile builder uses it to pick the paid vs unpaid render style, and — only
                            // when GateOnRecordedWin is set — to suppress the non-payers entirely. null means the
                            // outcome could not be read for this spin => the tile builder fails open (paid look,
                            // never suppressed).
                            RecordedOverlayGate recordedGate =
                                (multiplierCtx != null)
                                    ? ResolveRecordedOverlayGate(positionItem, slotDetailsItem.Details, multiplierCtx, slotRoundReader)
                                    : null;

                            int reelIdx = 0;
                            foreach (var reelItem in positionItem.Reels)
                            {
                                html += "<td>";
                                html += "<table>";
                                int floorIdx = 0;
                                foreach (var floorItem in reelItem.Floors)
                                {
                                    html += "<tr margin=\"2px 2px 2px 2px\">";
                                    // Figuring out where the symbol images are stored based on the platform type sent from client
                                    string symbolUrl = Url.Content(floorItem.SymbolName.ToSlotSymbolUrl(gameName, platformType));
                                    // For a configured multiplier symbol the finalised amount is overlaid on the tile;
                                    // every other symbol renders exactly as before.
                                    html += BuildMultiplierTile(symbolUrl, floorItem.SymbolName, multiplierCtx, reelIdx, floorIdx, onceOverlayCells, recordedGate);
                                    html += "</tr>";
                                    html += "<br/>";
                                    floorIdx++;
                                }
                                html += "</table>";
                                html += "</td>";
                                reelIdx++;
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
        /// the symbol -> params mapping (for the paid flag) and the symbol -> computed_amount map. Produced once
        /// per game round by <see cref="PrepareMultiplierData"/>; null means "render the plain symbols as before".
        /// </summary>
        private sealed class MultiplierOverlayContext
        {
            public bool GateOnRecordedWin { get; set; }
            public MultiplierSymbolMapping Mapping { get; set; }
            public IReadOnlyDictionary<string, decimal> Computed { get; set; }

            /// <summary>
            /// Whether a symbol's overlay is in scope for DISPLAY (i.e. a number is drawn at all). With gating OFF
            /// every configured multiplier symbol is in scope — both paid (B) and statically-unpaid (TB) — and the
            /// paid vs unpaid render STYLE (decided in BuildMultiplierTile from the recorded-outcome gate) tells the
            /// two apart. With gating ON only paid (B) symbols are in scope; a TB — which by definition did not pay —
            /// is never displayed, and non-paying paid-class occurrences are then suppressed by the gate itself.
            /// Recorded-gate CANDIDACY is deliberately not routed through here — only paid-class symbols may claim a
            /// recorded win (see ResolveRecordedOverlayGate), so a TB cannot steal an equal-value paid symbol's win.
            /// Used by tile build and once-cell resolution so those two visibility decisions agree.
            /// </summary>
            public bool InScope(MultiplierParams p) => p.Paid || !GateOnRecordedWin;
        }

        /// <summary>
        /// Multiplier-recompute feature entry point for the details view. Resolves this game's history config
        /// (wwwroot\GameConfig\<GameName>\ <GameName>_reels.xml), computes the finalised multiplier amounts,
        /// runs the LOG-ONLY Phase 1 validation (cross-checks computed vs. recorded located-scatter wins and logs
        /// any divergence), and returns a context the render loop uses to overlay those amounts onto the outcome
        /// tiles. Wrapped so any failure is non-fatal to the history page: on error/disabled/no-config it returns
        /// null and the tiles render exactly as before.
        ///
        /// Gated by the "MultiplierRecompute.Enabled" appSetting (off unless explicitly set to true).
        /// With "MultiplierRecompute.GateOverlayOnRecordedWin" off (default) every configured multiplier symbol is
        /// overlaid — paid (B) and statically-unpaid (TB) alike — told apart by the paid vs unpaid render style;
        /// with it on, only occurrences the recorded located-scatter outcome confirms paid are overlaid.
        /// </summary>
        private MultiplierOverlayContext PrepareMultiplierData(ISlotRoundReader slotRoundReader)
        {
            try
            {
                bool.TryParse(ConfigurationManager.AppSettings["MultiplierRecompute.Enabled"], out bool enabled);
                if (!enabled)
                {
                    return null;
                }

                string gameName = slotRoundReader.GetGameName();
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
                string configPath = Path.Combine(gameConfigRoot, gameName, gameName + "_reels.xml");
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

                // Early return when the config carries no multiplier symbols. This happens when the
                // <GameName>_reels.xml has no GameHistoryConfig element (or an empty multiplierGroups):
                // there is nothing to overlay, so the tiles must fall back to the original plain-symbol
                // rendering. Returning null here makes that fallback explicit at the source, and also
                // skips the log-only validator (which would otherwise WARN about every recorded
                // located-scatter win having no computed match for a game that was never configured).
                if (mapping == null || mapping.Mappings.Count == 0)
                {
                    if (sLog.IsDebugEnabled)
                    {
                        sLog.DebugFormat("Config for game '{0}' at {1} has no multiplier symbols (missing GameHistoryConfig/multiplierGroups); rendering plain symbols.", gameName, configPath);
                    }
                    return null;
                }

                // maps multiplier name to finalised amount
                IReadOnlyDictionary<string, decimal> computed = new WonAmountsComputer(parser).ComputeScatterAmounts(slotRoundReader);

                if (sLog.IsDebugEnabled)
                {
                    foreach (var kv in computed)
                    {
                        sLog.DebugFormat("Computed multiplier {0} -> {1} for game '{2}'.", kv.Key, kv.Value, gameName);
                    }
                }

                // Phase 1 validation stays log-only; it never alters what the loop below renders.
                new MultiplierComputationValidator().ValidateRound(slotRoundReader, mapping, computed);

                // Web.Config GameHistory Settings
                bool.TryParse(ConfigurationManager.AppSettings["MultiplierRecompute.GateOverlayOnRecordedWin"], out bool gateOnRecordedWin);

                return new MultiplierOverlayContext
                {
                    GateOnRecordedWin = gateOnRecordedWin,
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
        /// (<root></root><GameName></GameName><GameName></GameName>_reels.xml). Order of preference:
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
        /// A single grid position (reel/column index, floor/row index) within one spin. Used to pin a
        /// "once" placement overlay to exactly one cell.
        /// </summary>
        private struct GridCell : IEquatable<GridCell>
        {
            public int Reel { get; }
            public int Floor { get; }
            public GridCell(int reel, int floor) { Reel = reel; Floor = floor; }

            public bool Equals(GridCell other) => Reel == other.Reel && Floor == other.Floor;
            public override bool Equals(object obj) => obj is GridCell other && Equals(other);
            public override int GetHashCode() => (Reel * 397) ^ Floor;
        }

        /// <summary>
        /// Per-spin outcome of matching computed multiplier amounts against the recorded located-scatter wins.
        /// Resolved every round (see <see cref="ResolveRecordedOverlayGate"/>), independently of the Web.config flag.
        /// A NON-null (possibly empty) instance means "the recorded outcome was read": a listed occurrence paid this
        /// spin, and one NOT listed did not. What "did not pay" then looks like depends on
        /// <see cref="MultiplierOverlayContext.GateOnRecordedWin"/>: off (default) it is drawn with the unpaid render
        /// style; on it is suppressed to the plain symbol image. A null gate (returned on a read failure) means "could
        /// not be determined" and callers FAIL OPEN — every occurrence is treated as paid (paid style, never
        /// suppressed) rather than dimming or hiding a possibly-real win.
        ///  - <see cref="MatchedCells"/>: "all" placement occurrences whose computed amount matched a recorded win.
        ///  - <see cref="MatchedOnceGroups"/>: "once" placement groups that recorded a matching win this spin; the
        ///    single displayed cell is still chosen by <see cref="ResolveOnceOverlayCells"/>.
        /// </summary>
        private sealed class RecordedOverlayGate
        {
            public HashSet<GridCell> MatchedCells { get; } = new HashSet<GridCell>();
            public HashSet<string> MatchedOnceGroups { get; } = new HashSet<string>();
        }

        /// <summary>
        /// Builds the per-spin recorded-outcome gate: decides which PAID-class multiplier occurrences actually paid by
        /// matching each occurrence's computed amount against the spin's recorded located-scatter wins as a MULTISET
        /// — the very reconcile the Phase 1 validator does for logging, here promoted to a display decision. An
        /// occurrence whose amount finds an as-yet-unclaimed recorded win is "matched"; the rest did not pay. The grid
        /// is walked in render order so duplicate amounts are consumed the same way the tiles are drawn (a tie between
        /// two equal-amount cells resolves to the earlier one, matching how the loop paints). Only paid-class symbols
        /// are candidates: a statically-unpaid (TB) symbol shares an equal-value paid symbol's amount and must not be
        /// able to claim its win. Recorded zeros are not match targets (they are non-paying located scatters).
        ///
        /// Computed for every round. The result drives the paid vs unpaid render style always, and additionally
        /// suppresses non-payers when "MultiplierRecompute.GateOverlayOnRecordedWin" is on. Returns null if the
        /// recorded outcome cannot be read for this spin, so the caller FAILS OPEN (treats occurrences as paid)
        /// rather than dimming or hiding a win we merely failed to parse. An empty (non-null) gate is different: it
        /// means the spin genuinely recorded no paying located scatter, so nothing is matched.
        /// </summary>
        private static RecordedOverlayGate ResolveRecordedOverlayGate(
            SlotSymbolTableViewModel spin, string spinDetails, MultiplierOverlayContext ctx, ISlotRoundReader slotRoundReader)
        {
            if (ctx == null || spin?.Reels == null) return null;
            try
            {
                // Recorded located-scatter wins for this spin, read through the single reader. The reader already
                // drops non-paying zero markers, so these are all real amounts and valid match targets.
                var pool = slotRoundReader.GetOneSpinScatterWins(spinDetails);

                var gate = new RecordedOverlayGate();
                HashSet<string> onceSeen = null;

                int reelIdx = 0;
                foreach (var reelItem in spin.Reels)
                {
                    int floorIdx = 0;
                    if (reelItem?.Floors != null)
                    {
                        foreach (var floorItem in reelItem.Floors)
                        {
                            string symbolName = floorItem?.SymbolName;
                            // Only PAID-class symbols may claim a recorded located-scatter win. A statically
                            // unpaid (TB) symbol has the same computed amount as its equal-value paid sibling, so
                            // letting it match would let it steal that sibling's win (and mis-style both). A TB is
                            // always "did not pay" by virtue of its own paid="false" attribute, so it never needs a
                            // gate match to reach the unpaid style.
                            if (!string.IsNullOrEmpty(symbolName)
                                && ctx.Mapping.TryGet(symbolName, out var p)
                                && p.Paid
                                && ctx.Computed.TryGetValue(symbolName, out var amount))
                            {
                                if (p.Placement == MultiplierOverlayPlacement.OnceOnLastOccurrence)
                                {
                                    // One overlay per group per spin: consume the recorded win once for the group,
                                    // not once per in-group tile, mirroring the validator's once-group dedup.
                                    string groupKey = p.GroupName ?? symbolName;
                                    if (onceSeen == null) onceSeen = new HashSet<string>();
                                    if (onceSeen.Add(groupKey))
                                    {
                                        int gi = pool.IndexOf(amount);
                                        if (gi >= 0) { pool.RemoveAt(gi); gate.MatchedOnceGroups.Add(groupKey); }
                                    }
                                }
                                else
                                {
                                    int gi = pool.IndexOf(amount);
                                    if (gi >= 0) { pool.RemoveAt(gi); gate.MatchedCells.Add(new GridCell(reelIdx, floorIdx)); }
                                }
                            }
                            floorIdx++;
                        }
                    }
                    reelIdx++;
                }
                return gate;
            }
            catch (Exception ex)
            {
                // A single unparseable spin must not dim or hide overlays for the round; fail open (null gate =>
                // callers treat every occurrence as paid: paid style, never suppressed).
                sLog.WarnFormat("Recorded-outcome overlay gate failed for a spin (treating occurrences as paid): {0}", ex);
                return null;
            }
        }

        /// <summary>
        /// For each "once" placement group present in this spin, resolves the single cell that should carry the
        /// overlay: the LAST in-group occurrence in render order (reels left-to-right, floors top-to-bottom), keyed
        /// by group name. Only symbols that are in scope (see <see cref="MultiplierOverlayContext.InScope"/>) and have a
        /// computed amount are considered, so a group whose win did not resolve this spin contributes nothing.
        /// Returns an empty dictionary when there is no context or no "once" group occurs — the common path.
        /// The iteration order here mirrors the tile render loop so the chosen cell matches what is drawn.
        /// </summary>
        private static Dictionary<string, GridCell> ResolveOnceOverlayCells(SlotSymbolTableViewModel spin, MultiplierOverlayContext ctx)
        {
            var winners = new Dictionary<string, GridCell>();
            if (ctx == null || spin?.Reels == null) return winners;

            int reelIdx = 0;
            foreach (var reelItem in spin.Reels)
            {
                int floorIdx = 0;
                if (reelItem?.Floors != null)
                {
                    foreach (var floorItem in reelItem.Floors)
                    {
                        string symbolName = floorItem?.SymbolName;
                        if (!string.IsNullOrEmpty(symbolName)
                            && ctx.Mapping.TryGet(symbolName, out var p)
                            && p.Placement == MultiplierOverlayPlacement.OnceOnLastOccurrence
                            && ctx.InScope(p)
                            && ctx.Computed.ContainsKey(symbolName))
                        {
                            // Last assignment wins => the last in-group occurrence in render order.
                            winners[p.GroupName ?? symbolName] = new GridCell(reelIdx, floorIdx);
                        }
                        floorIdx++;
                    }
                }
                reelIdx++;
            }
            return winners;
        }

        /// <summary>
        /// Builds the HTML for a single outcome tile. For a configured multiplier symbol that is in scope
        /// (see <see cref="MultiplierOverlayContext.InScope"/>) and has a computed amount, the finalised amount is
        /// overlaid on top of the symbol artwork; otherwise the plain symbol image is returned unchanged.
        /// For a "once" placement group the overlay is drawn on a single cell per spin (see
        /// <see cref="ResolveOnceOverlayCells"/>); other in-group occurrences render plain.
        /// The overlay is styled by whether this occurrence paid this spin (from <paramref name="recordedGate"/>):
        /// a payer takes the group's paid style, a non-payer (a TB, or a paid-class symbol the recorded outcome did
        /// not confirm) takes the unpaid style. When <see cref="MultiplierOverlayContext.GateOnRecordedWin"/> is on,
        /// a non-payer is instead suppressed to the plain image, so only the paid style is ever drawn.
        /// <paramref name="symbolUrl"/> must already be resolved via Url.Content.
        /// <paramref name="reelIndex"/>/<paramref name="floorIndex"/> locate this tile in the spin grid.
        /// </summary>
        private static string BuildMultiplierTile(
            string symbolUrl,
            string symbolName,
            MultiplierOverlayContext ctx,
            int reelIndex,
            int floorIndex,
            Dictionary<string, GridCell> onceOverlayCells,
            RecordedOverlayGate recordedGate)
        {
            // Fallback in case the symbol is not in the mapping or has no computed amount: render the plain symbol image.
            decimal amount;
            if (ctx == null
                || string.IsNullOrEmpty(symbolName)
                || !ctx.Mapping.TryGet(symbolName, out MultiplierParams p)
                || !ctx.InScope(p)
                || !ctx.Computed.TryGetValue(symbolName, out amount))
            {
                return "<img src=\"" + symbolUrl + "\" >";
            }

            // "Once" placement: draw the overlay only on the resolved winning cell for this group; every other
            // in-group occurrence (e.g. the two trigger 'Wh' symbols) renders as the plain symbol image.
            if (p.Placement == MultiplierOverlayPlacement.OnceOnLastOccurrence)
            {
                if (onceOverlayCells == null
                    || !onceOverlayCells.TryGetValue(p.GroupName ?? symbolName, out var winner)
                    || winner.Reel != reelIndex
                    || winner.Floor != floorIndex)
                {
                    return "<img src=\"" + symbolUrl + "\" >";
                }
            }

            // Did THIS occurrence pay this spin? A statically-unpaid (TB) symbol never does. A paid-class symbol
            // does when the recorded located-scatter outcome confirms it (its cell, or its group for a "once"
            // placement, was matched by ResolveRecordedOverlayGate). A null gate means the outcome could not be
            // read -> fail open: treat as paid (paid look, and never suppressed) rather than dim/hide a possibly
            // -real win.
            bool paidThisSpin;
            if (recordedGate == null)
            {
                paidThisSpin = true;
            }
            else if (p.Placement == MultiplierOverlayPlacement.OnceOnLastOccurrence)
            {
                paidThisSpin = p.Paid && recordedGate.MatchedOnceGroups.Contains(p.GroupName ?? symbolName);
            }
            else
            {
                paidThisSpin = p.Paid && recordedGate.MatchedCells.Contains(new GridCell(reelIndex, floorIndex));
            }

            // Recorded-outcome gate (global "MultiplierRecompute.GateOverlayOnRecordedWin"). When ON, a non-paying
            // occurrence is suppressed entirely (plain image) and only payers render — so the unpaid style is never
            // reached in this mode. When OFF, nothing is suppressed here: both payers and non-payers render, and are
            // told apart below by the paid vs unpaid style.
            if (ctx.GateOnRecordedWin && !paidThisSpin)
            {
                return "<img src=\"" + symbolUrl + "\" >";
            }

            // Pick the render style by whether this occurrence paid. Both styles are fully resolved on the params
            // (unpaid falls back to paid unless the config supplied a distinct unpaid delta), so an un-styled config
            // yields the historical look for every tile.
            RenderStyle style = paidThisSpin ? p.PaidStyle : p.UnpaidStyle;

            string text = HttpUtility.HtmlEncode(FormatOverlayAmount(amount));
            var sb = new StringBuilder();
            sb.Append("<span style=\"position:relative; display:inline-block; line-height:0;\">");
            sb.Append("<img src=\"").Append(symbolUrl).Append("\" >");
            sb.Append("<span style=\"position:absolute; top:50%; left:50%; transform:translate(-50%,-50%); ");
            style.AppendCss(sb);
            sb.Append("white-space:nowrap; pointer-events:none;\">");
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
