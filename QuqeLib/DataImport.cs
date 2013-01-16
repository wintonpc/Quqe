using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Quqe
{
  public static class DataImport
  {
    public static DataSeries<Bar> Load(string symbol)
    {
      return new DataSeries<Bar>(symbol, DataImport.LoadNinjaBars(@"c:\users\wintonpc\git\Quqe\Share\" + symbol + ".txt"));
    }

    public static DataSeries<Bar> LoadVersace(string symbol)
    {
      return new DataSeries<Bar>(symbol, DataImport.LoadNinjaBars(@"c:\users\wintonpc\git\Quqe\Share\VersaceData\" + symbol + ".txt"));
    }

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

    static List<DataSeries<Bar>> Series = new List<DataSeries<Bar>>();
    public static DataSeries<Bar> Get(string symbol)
    {
      lock (Series)
      {
        var s = Series.FirstOrDefault(x => x.Symbol == symbol);
        if (s == null)
        {
          s = Load(symbol);
          Series.Add(s);
        }
        return s;
      }
    }
  }
}
