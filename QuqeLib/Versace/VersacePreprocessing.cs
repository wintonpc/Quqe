using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra.Double;
using PCW;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public static partial class Versace
  {
    public static void LoadPreprocessedValues()
    {
      Func<DataSeries<Bar>, double> idealSignal = GetIdealSignalFunc(Settings.PredictionType);

      PreprocessedData trainingData = GetPreprocessedValues(Settings.PreprocessingType, Settings.PredictedSymbol, Settings.TrainingStart, Settings.TrainingEnd, true, idealSignal);
      TrainingInput = trainingData.Inputs;
      TrainingOutput = trainingData.Outputs;

      if (Settings.UseValidationSet)
      {
        PreprocessedData validationData = GetPreprocessedValues(Settings.PreprocessingType, Settings.PredictedSymbol, Settings.ValidationStart, Settings.ValidationEnd, true, idealSignal);
        ValidationInput = validationData.Inputs;
        ValidationOutput = validationData.Outputs;
      }

      PreprocessedData testingData = GetPreprocessedValues(Settings.PreprocessingType, Settings.PredictedSymbol, Settings.TestingStart, Settings.TestingEnd, true, idealSignal);
      TestingInput = testingData.Inputs;
      TestingOutput = testingData.Outputs;
    }

    static Func<DataSeries<Bar>, double> GetIdealSignalFunc(PredictionType pt)
    {
      if (pt == PredictionType.NextClose)
        return s => s.Pos == 0 ? 0 : Math.Sign(s[0].Close - s[1].Close);
      else
        throw new Exception("Unexpected PredictionType: " + pt);
    }

    public static PreprocessedData GetPreprocessedValues(PreprocessingType preprocessType, string predictedSymbol, DateTime startDate, DateTime endDate, bool includeOutputs, Func<DataSeries<Bar>, double> idealSignal = null)
    {
      if (preprocessType == PreprocessingType.Enhanced)
        return PreprocessEnhanced(predictedSymbol, startDate, endDate, includeOutputs, idealSignal);
      else
        throw new Exception("Unexpected PreprocessingType: " + preprocessType);
    }

    public static List<DataSeries<Bar>> GetCleanSeries(string predictedSymbol, List<string> tickers)
    {
      var raw = tickers.Select(t => DataImport.LoadVersace(t)).ToList();
      var dia = raw.First(s => s.Symbol == predictedSymbol);
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

    static PreprocessedData PreprocessEnhanced(string predictedSymbol, DateTime startDate, DateTime endDate, bool includeOutputs, Func<DataSeries<Bar>, double> idealSignal)
    {
      var clean = GetCleanSeries(predictedSymbol, GetTickers(predictedSymbol));
      var aOnly = new List<DataSeries<Value>>();
      var bOnly = new List<DataSeries<Value>>();

      if (includeOutputs)
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

      Action<string, Func<Bar, Value>> addSmaNorm = (ticker, getValue) =>
        aOnly.Add(get(ticker).NormalizeSma10(getValue));

      Action<string> addSmaNormOHLC = (ticker) => {
        addSmaNorm(ticker, x => x.Open);
        addSmaNorm(ticker, x => x.High);
        addSmaNorm(ticker, x => x.Low);
        addSmaNorm(ticker, x => x.Close);
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
      }));

      // % Diff Open-Close
      bOnly.Add(predicted.MapElements<Value>((s, v) => (s[0].Open - s[0].Close) / s[0].Open * 100));

      // % Diff High-Low
      bOnly.Add(predicted.MapElements<Value>((s, v) => (s[0].High - s[0].Low) / s[0].Low * 100));

      // my own LinReg stuff
      {
        var fast = predicted.Closes().LinReg(2, 1);
        var slow = predicted.Closes().LinReg(7, 1);
        bOnly.Add(fast.ZipElements<Value, Value>(slow, (f, s, _) => f[0] - s[0]));
        bOnly.Add(predicted.Closes().RSquared(10));
        bOnly.Add(predicted.Closes().LinRegSlope(4));
      }

      addSmaNormOHLC(predictedSymbol);
      addSmaNorm(predictedSymbol, x => x.Volume);

      bOnly.Add(predicted.ChaikinVolatility(10));
      bOnly.Add(predicted.Closes().MACD(10, 21));
      bOnly.Add(predicted.Closes().Momentum(10));
      bOnly.Add(predicted.VersaceRSI(10));

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
      bOnly.Add(get("^DJI").Closes().NormalizeUnit().ZipElements<Value, Value>(get("^TYX").Closes().NormalizeUnit(), (dj, tb, _) => dj[0] - tb[0]));

      DatabaseAInputLength[PreprocessingType.Enhanced] = aOnly.Count;

      var unalignedData = aOnly.Concat(bOnly).Select(s => s.NormalizeUnit()).SeriesToMatrix();
      if (!includeOutputs)
      {
        return new PreprocessedData {
          Predicted = predicted,
          Inputs = unalignedData,
          Outputs = null
        };
      }
      //var unalignedOutput = SeriesToMatrix(List.Create(predicted.MapElements<Value>((s, _) => s.Pos == 0 ? 0 : Math.Sign(s[0].Close - s[1].Close))));
      var unalignedOutput = List.Create(predicted.MapElements<Value>((s, _) => idealSignal(s))).SeriesToMatrix();

      var data = unalignedData.Columns().Take(unalignedData.ColumnCount - 1).ColumnsToMatrix();
      var output = unalignedOutput.Columns().Skip(1).ColumnsToMatrix();
      return new PreprocessedData {
        Predicted = predicted,
        Inputs = data,
        Outputs = new DenseVector(output.Row(0).ToArray())
      };
    }

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
  }
}
