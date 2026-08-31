using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using GameHistory.Helpers;
using System.Net;
using log4net;

namespace GameHistory
{
  // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
  // visit http://go.microsoft.com/?LinkId=9394801
  public class MvcApplication : System.Web.HttpApplication
  {
    private static ILog sLog = LogManager.GetLogger(typeof(MvcApplication));
    protected void Application_Start()
    {
      log4net.Config.XmlConfigurator.Configure();
      sLog.Debug("Application_Start invoked.");
      AreaRegistration.RegisterAllAreas();

      WebApiConfig.Register(GlobalConfiguration.Configuration);
      FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
      RouteConfig.RegisterRoutes(RouteTable.Routes);

      if (PageConfiguration.IgnoreServerCertificates != null)
      {
        sLog.Debug("IgnoreServerCertificates exists");
        bool ignoreServerCertificates = false;

        Boolean.TryParse(PageConfiguration.IgnoreServerCertificates, out ignoreServerCertificates);

        if (ignoreServerCertificates)
        {
          sLog.Debug("ignoreServerCertificates is set.");
          ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
        }
      }
    }
  }
}