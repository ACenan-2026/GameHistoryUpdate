using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using log4net;

namespace GameHistory.Helpers
{
  public static class PageConfiguration
  {
    private static ILog sLog = LogManager.GetLogger(typeof(PageConfiguration));
    public static string RGSAgentUrl
    {
      get 
      { 
        string result = System.Web.Configuration.WebConfigurationManager.AppSettings["RGSAgentUrl"].ToString();
        if (sLog.IsDebugEnabled)
        {
          sLog.DebugFormat("Get RGSAgentUrl finished with {0}.", result);
        }
        return result;
      }
    }

    public static string RGSAgentUsername
    {
      get
      {
        string result = System.Web.Configuration.WebConfigurationManager.AppSettings["RGSAgentUsername"].ToString();
        if (sLog.IsDebugEnabled)
        {
          sLog.DebugFormat("Get RGSAgentUsername finished with {0}.", result);
        }
        return result;
      }
    }

    public static string RGSAgentPassword
    {
      get
      {
        string result = System.Web.Configuration.WebConfigurationManager.AppSettings["RGSAgentPassword"].ToString();
        if (sLog.IsDebugEnabled)
        {
          sLog.DebugFormat("Get RGSAgentPassword finished with {0}.", result);
        }
        return result;
      }
    }

    public static string IgnoreServerCertificates
    {
      get
      {
        string result = System.Web.Configuration.WebConfigurationManager.AppSettings["IgnoreServerCertificates"].ToString();
        if (sLog.IsDebugEnabled)
        {
          sLog.DebugFormat("Get IgnoreServerCertificates finished with {0}.", result);
        }
        return result;
      }
    }

    public static bool CheckSession
    {
      get
      {
        bool ret = false;
        string result = System.Web.Configuration.WebConfigurationManager.AppSettings["CheckSession"].ToString();
        if (bool.TryParse(result, out ret))
        {
          sLog.Debug("Value pardes");
        }
        if (sLog.IsDebugEnabled)
        {
          sLog.DebugFormat("Get CheckSession finished with {0}.", ret);
        }
        return ret;
      }
    }

    public static int NumberOfDaysOfHistory
    {
        get
        {
            int numberOfDaysOfHistory = Convert.ToInt16(System.Web.Configuration.WebConfigurationManager.AppSettings["NumberOfDaysOfHistory"]);

            return numberOfDaysOfHistory;
        }
    }

  }
}