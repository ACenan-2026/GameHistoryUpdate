using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using log4net;

namespace GameHistory.Helpers
{
  public static class DateTimeExtensions
  {
    private static ILog sLog = LogManager.GetLogger(typeof(DateTimeExtensions));
    /// <summary>
    /// Convert ISO 8601 formatted date time string to .NET DateTime type. Method assumes that string is ISO 8601 formatted
    /// and UTC is the timezone being used when conversion has been done.
    /// </summary>
    /// <param name="isoDateTime">ISO 8601 formatted date and time string.</param>
    /// <returns>DateTime structure.</returns>
    public static DateTime ConvertISO8601ToDateTime(this string isoDateTime)
    {
      if (sLog.IsDebugEnabled)
      {
        sLog.DebugFormat("ConvertISO8601ToDateTime with isoDateTime: {0} invoked.", isoDateTime);
      }
      try
      {
        DateTime result;
        // The Sortable ("s") Format Specifier
        // The "s" standard format specifier represents a custom date and time format string that is defined by
        // the DateTimeFormatInfo.SortableDateTimePattern property. The pattern reflects a defined standard (ISO 8601), 
        // and the property is read-only. Therefore, it is always the same, regardless of the culture used or the 
        // format provider supplied. The custom format string is "yyyy'-'MM'-'dd'T'HH':'mm':'ss".
        // When this standard format specifier is used, the formatting or parsing operation always uses the invariant culture.
        result = DateTime.ParseExact(isoDateTime, "s", System.Globalization.CultureInfo.InvariantCulture);
        if (sLog.IsDebugEnabled)
        {
          sLog.DebugFormat("ConvertISO8601ToDateTime finished with {0}.", result);
        } 
        return result;
      }
      catch (FormatException e)
      {
        sLog.ErrorFormat("Error in ConvertISO8601ToDateTime with {0}. {1}", isoDateTime, e);
        //TODO: throw proper exception!
        throw;
      }
      catch (ArgumentNullException e)
      {
        sLog.ErrorFormat("Error in ConvertISO8601ToDateTime with {0}. {1}", isoDateTime, e);
        //TODO: throw proper exception!
        throw;
      }
      catch (ArgumentException e)
      {
        sLog.ErrorFormat("Error in ConvertISO8601ToDateTime with {0}. {1}", isoDateTime, e);
        //TODO: throw proper exception!
        throw;
      }
    }

    /// <summary>
    /// Converts provided DateTime to ISO8601 string.
    /// </summary>
    /// <param name="dateTime">DateTime to convert.</param>
    /// <returns>String presentation of the provided DateTime.</returns>
    public static string ConvertDateTimeToISO8601(this DateTime dateTime)
    {
      if (sLog.IsDebugEnabled)
      {
        sLog.DebugFormat("ConvertDateTimeToISO8601 with dateTime: {0} invoked.", dateTime);
      }
      try
      {
        //string result = dateTime.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
        string iso8601Date = dateTime.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        //result += "." + dateTime.Millisecond;
        if (sLog.IsDebugEnabled)
        {
          sLog.DebugFormat("ConvertDateTimeToISO8601 finished with {0}.", iso8601Date);
        }
        return iso8601Date;
      }
      catch (ArgumentOutOfRangeException aoore)
      {
        sLog.ErrorFormat("Error in ConvertDateTimeToISO8601 with {0}. {1}", dateTime, aoore);
      }

      return null;
    }

    /// <summary>
    /// Checks provided string if matches ISO8601 format.
    /// </summary>
    /// <param name="dateTime">DateTime to check.</param>
    /// <returns></returns>
    public static bool IsISO8601(this string isoDateTime)
    {
      if (sLog.IsDebugEnabled)
      {
        sLog.DebugFormat("IsISO8601 with isoDateTime: {0} invoked.", isoDateTime);
      }
      bool result = false;
      try
      {
         //DateTime result = DateTime.ParseExact(isoDateTime, "s", System.Globalization.CultureInfo.InvariantCulture);
         DateTime dt;
         result = DateTime.TryParseExact(isoDateTime, "s", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt);      
      }
      catch (Exception e)
      {
        sLog.ErrorFormat("Error in IsISO8601 with isoDateTime: {0}. {1}", isoDateTime, e);
        result = false;
      }
      if (sLog.IsDebugEnabled)
      {
        sLog.DebugFormat("IsISO8601 finished with: {0}.", result);
      }
      return result;
    }

  }
}