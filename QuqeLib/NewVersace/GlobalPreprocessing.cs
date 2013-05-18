using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra.Double;
using PCW;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public static class Preprocessing
  {
    public static Vec ComplementCode(Vec input)
    {
      return new DenseVector(input.Concat(input.Select(x => 1.0 - x)).ToArray());
    }

    public static Mat PrincipleComponents(Mat data)
    {
      var rows = data.Rows();
      var meanAdjustedRows = rows.Select(x => x.Subtract(x.Average())).ToList();
      var X = meanAdjustedRows.ColumnsToMatrix(); // we needed to transpose it anyway

      var svd = X.Svd(true);
      var V = svd.VT().Transpose();
      return V;
    }

    public static Vec NthPrincipleComponent(Mat principleComponents, int n, Vec x)
    {
      var pc = principleComponents.Column(n);
      return x.DotProduct(pc) * pc;
    }

    public static TrainingSeed MakeTrainingSeed(DateTime startDate, DateTime endDate)
    {
      var data = PreprocessEnhanced("DIA", startDate, endDate, GetIdealSignalFunc(PredictionType.NextClose));
      return new TrainingSeed(data.Input, data.Output, data.DatabaseAInputLength);
    }

    public static List<string> GetTickers(string predictedSymbol)
    {
      return List.Create(predictedSymbol, "^IXIC", "^GSPC", "^DJI", "^DJT", "^DJU", "^DJA", "^N225", "^BVSP",
        "^GDAXI", "^FTSE", /*"^CJJ", "USDCHF"*/ "^TYX", "^TNX", "^FVX", "^IRX", /*"EUROD"*/ "^XAU");
    }

    static PreprocessedData PreprocessEnhanced(string predictedSymbol, DateTime startDate, DateTime endDate, Func<DataSeries<Bar>, double> idealSignal)
    {
      var clean = GetCleanSeries(predictedSymbol, GetTickers(predictedSymbol));
      var aOnly = new List<DataSeries<Value>>();
      var bOnly = new List<DataSeries<Value>>();

      if (idealSignal != null)
      {
        // increase endDate by one trading day, because on the true endDate,
        // we need to look one day ahead to know the correct prediction.
        // if the trueEnd date is the last day in the DataSeries (can't look ahead), we'll have to chop it off in the output.
        var ds = clean.First(); // any will do, since we already cleaned them to have the exact same timestamp sequence
        int i = 1;
        while (i < ds.Length && ds[i].Timestamp < endDate.AddDays(1))
          i++;
        endDate = ds[i].Timestamp;
      }

      /////////////////////
      // helper functions
      Func<string, DataSeries<Bar>> get = ticker => clean.First(x => x.Symbol == ticker)
        .From(startDate).To(endDate);

      Action<string, string, Func<Bar, Value>> addSmaNorm = (ticker, tag, getValue) =>
        aOnly.Add(get(ticker).NormalizeSma10(getValue).SetTag(tag));

      Action<string> addSmaNormOHLC = ticker => {
        addSmaNorm(ticker, "Open", x => x.Open);
        addSmaNorm(ticker, "High", x => x.High);
        addSmaNorm(ticker, "Low", x => x.Low);
        addSmaNorm(ticker, "Close", x => x.Close);
      };

      //////////////////////////////////////////
      // apply indicators and SMA normalizations
      var predicted = get(predictedSymbol);

      // % ROC Close
      bOnly.Add(predicted.MapElements<Value>((s, v) => {
        if (s.Pos == 0)
          return 0;
        else
          return (s[0].Close - s[1].Close) / s[1].Close * 100;
      }).SetTag("% ROC Close"));

      // % Diff Open-Close
      bOnly.Add(predicted.MapElements<Value>((s, v) => (s[0].Open - s[0].Close) / s[0].Open * 100).SetTag("% Diff Open-Close"));

      // % Diff High-Low
      bOnly.Add(predicted.MapElements<Value>((s, v) => (s[0].High - s[0].Low) / s[0].Low * 100).SetTag("% Diff High-Low"));

      // my own LinReg stuff
      {
        var fast = predicted.Closes().LinReg(2, 1);
        var slow = predicted.Closes().LinReg(7, 1);
        bOnly.Add(fast.ZipElements<Value, Value>(slow, (f, s, _) => f[0] - s[0]).SetTag("Fast/Slow differential"));
        bOnly.Add(predicted.Closes().RSquared(10).SetTag("Fast/Slow RSquared"));
        bOnly.Add(predicted.Closes().LinRegSlope(4).SetTag("Fast/Slow LinRegSlope"));
      }

      addSmaNormOHLC(predictedSymbol);
      addSmaNorm(predictedSymbol, "Volume", x => x.Volume);

      bOnly.Add(predicted.ChaikinVolatility(10).SetTag("ChaikinVolatility(10)"));
      bOnly.Add(predicted.Closes().MACD(10, 21).SetTag("MACD(10, 21)"));
      bOnly.Add(predicted.Closes().Momentum(10).SetTag("Momentum(10)"));
      bOnly.Add(predicted.VersaceRSI(10).SetTag("VersaceRSI(10)"));

      addSmaNormOHLC("^IXIC");
      addSmaNormOHLC("^GSPC");
      addSmaNormOHLC("^DJI");
      addSmaNormOHLC("^DJT");
      addSmaNormOHLC("^DJU");
      addSmaNormOHLC("^DJA");
      addSmaNormOHLC("^N225");
      addSmaNormOHLC("^BVSP");
      addSmaNormOHLC("^GDAXI");
      addSmaNormOHLC("^FTSE");
      // MISSING: dollar/yen
      // MISSING: dollar/swiss frank
      addSmaNormOHLC("^TYX");
      addSmaNormOHLC("^TNX");
      addSmaNormOHLC("^FVX");
      addSmaNormOHLC("^IRX");
      // MISSING: eurobond

      addSmaNormOHLC("^XAU");
      //addSmaNorm("^XAU", "Volume", x => x.Volume); // XAU volume is no longer reported

      // % Diff. between Normalized DJIA and Normalized T Bond
      bOnly.Add(get("^DJI").Closes().NormalizeUnit().ZipElements<Value, Value>(get("^TYX").Closes().NormalizeUnit(), (dj, tb, _) => dj[0] - tb[0])
        .SetTag("% Diff. between Normalized DJIA and Normalized T Bond"));

      var allInputSeries = aOnly.Concat(bOnly).Select(s => s.NormalizeUnit()).ToList();
      if (!allInputSeries.All(s => s.All(x => !double.IsNaN(x.Val))))
        throw new Exception("Some input values are NaN!");

      var unalignedInputs = allInputSeries.SeriesToMatrix();
      if (idealSignal == null)
        return new PreprocessedData(predicted, null, unalignedInputs, null, aOnly.Count);

      var unalignedOutput = List.Create(predicted.MapElements<Value>((s, _) => idealSignal(s))).SeriesToMatrix();

      var data = unalignedInputs.Columns().Take(unalignedInputs.ColumnCount - 1).ColumnsToMatrix();
      var output = unalignedOutput.Columns().Skip(1).ColumnsToMatrix();
      return new PreprocessedData(predicted, allInputSeries, data, new DenseVector(output.Row(0).ToArray()), aOnly.Count);
    }

    public static Func<DataSeries<Bar>, double> GetIdealSignalFunc(PredictionType pt)
    {
      if (pt == PredictionType.NextClose)
        return s => {
          if (s.Pos == 0) return 0;
          var ideal = Math.Sign(s[0].Close - s[1].Close);
          if (ideal == 0) return 1; // we never predict "no change", so if there actually was no change, consider it a buy
          return ideal;
        };
      else
        throw new Exception("Unexpected PredictionType: " + pt);
    }

    static List<DataSeries<Bar>> GetCleanSeries(string predictedSymbol, List<string> tickers)
    {
      var allSeries = tickers.Select(t => DataImport.LoadVersace(t)).ToList();
      var predictedSeries = allSeries.First(s => s.Symbol == predictedSymbol);
      return allSeries.Select(s => s == predictedSeries ? s : CleanWithRespectTo(predictedSeries, s)).ToList();
    }

    static DataSeries<Bar> CleanWithRespectTo(DataSeries<Bar> wrt, DataSeries<Bar> s)
    {
      // fill in missing data in supplemental instruments
      var q = (from w in wrt
               join x in s on w.Timestamp equals x.Timestamp into joined
               from j in joined.DefaultIfEmpty()
               select new {
                 Timestamp = w.Timestamp,
                 X = j
               }).ToList();
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
    }
  }
}
