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
                
                Console.WriteLine("Anujan - GameHistoryDetailsResponse data is : ");
                    Console.WriteLine(data);

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
                                    html += "<img src=\"" + Url.Content(floorItem.SymbolName.ToSlotSymbolUrl(data.GameHistoryDetailsMember.GameHistoryGameInfoSlotModel.GameName, platformType) + "\" >");
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
