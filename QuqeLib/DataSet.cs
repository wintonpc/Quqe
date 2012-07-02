using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Quqe
{
  public class Bar
  {
    public readonly DateTime Timestamp;
    public readonly double Open;
    public readonly double Low;
    public readonly double High;
    public readonly double Close;
    public readonly long Volume;

    public Bar(DateTime timestamp, double open, double low, double high, double close, long volume)
    {
      Timestamp = timestamp;
      Open = open;
      Low = low;
      High = high;
      Close = close;
      Volume = volume;
    }
  }

  public static class DataSet
  {
    public static List<Bar> LoadNinjaBars(string fn)
    {
      return File.ReadAllLines(fn).Select(line => {
        var toks = line.Trim().Split(';');
        return new Bar(
          DateTime.ParseExact(toks[0], "yyyyMMdd", null),
          double.Parse(toks[1]),
          double.Parse(toks[3]),
          double.Parse(toks[2]),
          double.Parse(toks[4]),
          long.Parse(toks[5]));
      }).ToList();
    }
  }
}
