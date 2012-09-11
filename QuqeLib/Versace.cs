using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Diagnostics;

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

    public static List<DataSeries<Bar>> GetCleanSeries()
    {
      var raw = Tickers.Select(t => Data.LoadVersace(t)).ToList();
      var dia = raw.First(s => s.Symbol == "DIA");
      var supp = raw.Where(s => s != dia).ToList();

      // fill in missing data in supplemental instruments
      var supp2 = supp.Select(s => {
        var q = (from d in dia
                 join x in s on d.Timestamp equals x.Timestamp into joined
                 from j in joined.DefaultIfEmpty()
                 select new { Timestamp = d.Timestamp, X = j }).ToList();
        List<Bar> newElements = new List<Bar>();
        for (int i = 0; i < q.Count; i++)
        {
          if (q[i].X != null)
            newElements.Add(q[i].X);
          else
          {
            var prev = newElements.Last();
            newElements.Add(new Bar(q[i].Timestamp, prev.Open, prev.Low, prev.High, prev.Close, prev.Volume));
          }
        }
        return new DataSeries<Bar>(s.Symbol, newElements);
      }).ToList();

      return supp2.Concat(List.Create(dia)).ToList();
    }

    static List<DataSeries<Value>> _NormalizedValueSeries;
    public static List<DataSeries<Value>> NormalizedValueSeries
    {
      get
      {
        if (_NormalizedValueSeries == null)
          LoadNormalizedValues();
        return _NormalizedValueSeries;
      }
    }

    private static void LoadNormalizedValues()
    {
      var clean = GetCleanSeries();
      var vs = new List<DataSeries<Value>>();

      Func<string, DataSeries<Bar>> get = ticker => clean.First(x => x.Symbol == ticker);

      Action<string, Func<Bar, Value>> addSmaNorm = (ticker, getValue) =>
        vs.Add(get(ticker).NormalizeSma10(getValue));

      Action<string> addSmaNormOHLC = (ticker) => {
        addSmaNorm(ticker, x => x.Open);
        addSmaNorm(ticker, x => x.High);
        addSmaNorm(ticker, x => x.Low);
        addSmaNorm(ticker, x => x.Close);
      };

      // % ROC Close
      vs.Add(get("DIA").MapElements<Value>((s, v) => {
        if (s.Pos == 0)
          return 0;
        else
          return (s[0].Close - s[1].Close) / s[1].Close * 100;
      }));

      // % Diff Open-Close
      vs.Add(get("DIA").MapElements<Value>((s, v) => (s[0].Open - s[0].Close) / s[0].Open * 100));

      // % Diff High-Low
      vs.Add(get("DIA").MapElements<Value>((s, v) => (s[0].High - s[0].Low) / s[0].Low * 100));

      addSmaNormOHLC("DIA");
      addSmaNorm("DIA", x => x.Volume);

      vs.Add(get("DIA").ChaikinVolatility(10));
      vs.Add(get("DIA").Closes().MACD(10, 21));
      vs.Add(get("DIA").Closes().Momentum(10));
      vs.Add(get("DIA").VersaceRSI(10));

      addSmaNormOHLC("^IXIC");
      addSmaNormOHLC("^GSPC");
      addSmaNormOHLC("^DJI");
      addSmaNormOHLC("^DJT");
      addSmaNormOHLC("^DJU");
      addSmaNormOHLC("^DJA");
      addSmaNormOHLC("^N225");
      addSmaNormOHLC("^BVSP");
      addSmaNormOHLC("^GDAX");
      addSmaNormOHLC("^FTSE");
      // MISSING: dollar/yen
      // MISSING: dollar/swiss frank
      addSmaNormOHLC("^TYX");
      addSmaNormOHLC("^TNX");
      addSmaNormOHLC("^FVX");
      addSmaNormOHLC("^IRX");
      // MISSING: eurobond

      addSmaNormOHLC("^XAU");
      addSmaNorm("^XAU", x => x.Volume);

      // % Diff. between Normalized DJIA and Normalized T Bond
      vs.Add(get("^DJI").Closes().NormalizeUnit().ZipElements<Value, Value>(get("^TYX").Closes().NormalizeUnit(), (dj, tb, _) => dj[0] - tb[0]));

      _NormalizedValueSeries = vs.Select(s => s.NormalizeUnit()).ToList();
      var pcs = PrincipleComponents(_NormalizedValueSeries);
    }

    public static List<DataSeries<Value>> ComplementCode(List<DataSeries<Value>> series)
    {
      return series.Concat(series.Select(x => x.MapElements<Value>((s, v) => 1.0 - s[0]))).ToList();
    }

    public static List<Vector> PrincipleComponents(List<DataSeries<Value>> series)
    {
      var m = series.Count;
      var n = series.First().Length;

      var meanAdjusted = series.Select(x => {
        var mean = x.Select(v => v.Val).Average();
        return x.MapElements<Value>((s, _) => s[0] - mean);
      }).ToList();

      Matrix X = new DenseMatrix(m, n);
      for (int i = 0; i < m; i++)
        for (int j = 0; j < n; j++)
          X[i, j] = meanAdjusted[i][j];

      var svd = X.Transpose().Svd(true);
      var V = svd.VT().Transpose();
      return List.Repeat(V.ColumnCount, j => (Vector)V.Column(j));
    }

    public static Vector NthPrincipleComponent(List<Vector> pcs, int n, Vector x)
    {
      var pc = pcs[n];
      return (Vector)(x.DotProduct(pc) * pc);
    }

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
            return string.Format("{0:yyyyMMdd};{1};{2};{3};{4};{5}",
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
