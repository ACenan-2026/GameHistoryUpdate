using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Security.Policy;

namespace GameHistory.Helpers
{
  public static class GameExtensions
  {
    public static string ToSlotSymbolUrl(this string symbolName, string content, string platform)
    {
        return String.Format("~/Content/Images/{0}/{1}/{2}.png", content, platform, symbolName);
    }

    public static string ToCardSymbolUrl(this string symbolName)
    {
      return String.Format("~/Content/Images/Cards/{0}.png",symbolName);
    }
  }
}