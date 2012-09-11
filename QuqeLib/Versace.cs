using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;

namespace Quqe
{
  public static class Versace
  {
    public static List<string> Tickers = List.Create(
      "DIA", "^IXIC", "^GSPC", "^DJI", "^DJT", "^DJU", "^DJA", "^N225", "^BVSP", "^GDAX", "^FTSE", /*"^CJJ", "USDCHF"*/
      "^TYX", "^TNX", "^FVX", "^IRX", /*"EUROD"*/ "^XAU"
      );

    static DateTime StartDate = DateTime.Parse("11/11/2001");
    static DateTime EndDate = DateTime.Parse("02/12/2003");

    public static void GetData()
    {
      var dir = @"c:\users\wintonpc\git\Quqe\Share\VersaceData";
      if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);

      foreach (var ticker in Tickers)
      {
        using (var c = new WebClient())
        {

          var fn = Path.Combine(dir, ticker + ".txt");


          if (ticker.StartsWith("^DJ")) // historical downloads of dow jones indices are not allowed
          {
            GetDowJonesData(c, ticker, fn);
          }
          else
          {
            var address = string.Format("http://ichart.finance.yahoo.com/table.csv?s={0}&a={1}&b={2}&c={3}&d={4}&e={5}&f={6}&g=d&ignore=.csv",
              ticker, StartDate.Month - 1, StartDate.Day, StartDate.Year, EndDate.Month - 1, EndDate.Day, EndDate.Year);
            c.DownloadFile(address, fn);
          }

          var fixedLines = File.ReadAllLines(fn).Skip(1).Reverse().Select(line => {
            var toks = line.Split(',');
            var timestamp = DateTime.ParseExact(toks[0], "yyyy-MM-dd", null);
            var open = double.Parse(toks[1]);
            var high = double.Parse(toks[2]);
            var low = double.Parse(toks[3]);
            var close = double.Parse(toks[4]);
            var volume = long.Parse(toks[5]);
            return string.Format("{0:yyyyMMdd},{1},{2},{3},{4},{5}",
              timestamp, open, high, low, close, volume);
          });
          File.WriteAllLines(fn, fixedLines);
        }
      }
    }

    private static void GetDowJonesData(WebClient c, string ticker, string fn)
    {
      List<string> lines = new List<string>();
      var address = string.Format("http://finance.yahoo.com/q/hp?s={0}&a={1}&b={2}&c={3}&d={4}&e={5}&f={6}&g=d",
        ticker, StartDate.Month - 1, StartDate.Day, StartDate.Year, EndDate.Month - 1, EndDate.Day, EndDate.Year);

      while (true)
      {
        var html = c.DownloadString(address);
        var trs = Regex.Matches(html, @"<tr[^>]*>(<td [^>]*tabledata[^>]*>[^<]+</td>)+</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
          .OfType<Match>().Select(m => m.Groups[0].Value).ToList();
        foreach (var tr in trs)
        {
          var fs = Regex.Matches(tr, @"<td [^>]*tabledata[^>]*>([^<]+)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline).OfType<Match>().Select(m => m.Groups[1].Value).ToList();
          if (fs.Count != 7)
            continue;
          var timestamp = DateTime.ParseExact(fs[0], "MMM d, yyyy", null);
          if (timestamp < StartDate) // yahoo enumerates data in reverse chronological order
            goto Done;
          lines.Add(string.Format("{0:yyyy-MM-dd},{1},{2},{3},{4},{5}",
            timestamp, double.Parse(fs[1]), double.Parse(fs[2]), double.Parse(fs[3]), double.Parse(fs[4]), long.Parse(fs[5], System.Globalization.NumberStyles.AllowThousands)));
        }

        var nextAddrMatch = Regex.Match(html, @"<a [^>]*href=""([^""]+)""[^>]*>Next</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!nextAddrMatch.Success)
          goto Done;
        address = "http://finance.yahoo.com" + HttpUtility.HtmlDecode(nextAddrMatch.Groups[1].Value);
      }
    Done: File.WriteAllLines(fn, lines);
    }
  }

  //public class CookieClient
  //{
  //  CookieContainer Cookies = new CookieContainer();
  //  string LastAddress;

  //  public string Get(string address)
  //  {
  //    var request = (HttpWebRequest)HttpWebRequest.Create(address);
  //    request.CookieContainer = Cookies;
  //    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
  //    request.Referer = LastAddress;
  //    request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.1 (KHTML, like Gecko) Chrome/21.0.1180.89 Safari/537.1";
  //    LastAddress = address;

  //    var response = request.GetResponse();
  //    using (var sr = new StreamReader(response.GetResponseStream()))
  //      return sr.ReadToEnd();
  //  }
  //}
}
