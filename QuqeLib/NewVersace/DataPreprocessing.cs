using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra.Double;
using Quqe.NewVersace;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public static class DataPreprocessing
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

    public static DataSet LoadTrainingSet(Database db, string predictedSymbol, DateTime startDate, DateTime endDate, Func<DataSeries<Bar>, double> idealSignalFunc)
    {
      var cleanSeries = GetCleanSeries(db, predictedSymbol, GetTickers(predictedSymbol));
      return LoadTrainingSetInternal(predictedSymbol, startDate, endDate, cleanSeries, idealSignalFunc);
    }

    static DataSet LoadTrainingSetInternal(string predictedSymbol, DateTime startDate, DateTime endDate, List<DataSeries<Bar>> cleanSeries, Func<DataSeries<Bar>, double> idealSignalFunc)
    {
      return LoadTrainingSet(cleanSeries, predictedSymbol, startDate, endDate, idealSignalFunc);
    }

    public static DataSet LoadTrainingSetFromDisk(string predictedSymbol, DateTime startDate, DateTime endDate, Func<DataSeries<Bar>, double> idealSignalFunc)
    {
      return LoadTrainingSet(GetCleanSeriesFromDisk(predictedSymbol, GetTickers(predictedSymbol)), predictedSymbol, startDate, endDate, idealSignalFunc);
    }

    static DataSet LoadTrainingSet(List<DataSeries<Bar>> cleanSeries, string predictedSymbol, DateTime startDate, DateTime endDate,
      Func<DataSeries<Bar>, double> idealSignalFunc)
    {
      var preprocessedInput = GetPreprocessedInput(predictedSymbol, cleanSeries);
      var predictedSeries = cleanSeries.First(x => x.Symbol == predictedSymbol);
      var output = GetIdealOutput(idealSignalFunc, predictedSeries);
      var inputs = preprocessedInput.Input.Columns().Take(preprocessedInput.Input.ColumnCount - 1).ColumnsToMatrix(); // trim to output

      return new DataSet(
        TrimToWindow(inputs, startDate, endDate, predictedSeries),
        TrimToWindow(output, startDate, endDate, predictedSeries),
        preprocessedInput.DatabaseAInputLength);
    }

    public static Tuple2<DataSet> LoadTrainingAndValidationSets(Database db, string predictedSymbol, DateTime startDate, DateTime endDate,
                                                                double validationPct, Func<DataSeries<Bar>, double> idealSignalFunc)
    {
      DateTime splitDate = startDate.AddDays((endDate - startDate).TotalDays * (1.0 - validationPct)).Date;
      var tickers = GetTickers(predictedSymbol);
      var cleanSeries = GetCleanSeries(db, predictedSymbol, tickers);
      return new Tuple2<DataSet>(
        LoadTrainingSetInternal(predictedSymbol, startDate, splitDate, cleanSeries, idealSignalFunc),
        LoadTrainingSetInternal(predictedSymbol, splitDate.AddDays(1), endDate, cleanSeries, idealSignalFunc));
    }

    static Mat TrimToWindow(Mat inputs, DateTime startDate, DateTime endDate, DataSeries<Bar> s)
    {
      var w = GetWindow(startDate, endDate, s);
      return inputs.Columns().Skip(w.Item1).Take(w.Item2 - w.Item1 + 1).ColumnsToMatrix();
    }

    static Vec TrimToWindow(Vec outputs, DateTime startDate, DateTime endDate, DataSeries<Bar> s)
    {
      var w = GetWindow(startDate, endDate, s);
      return outputs.SubVector(w.Item1, w.Item2 - w.Item1 + 1);
    }

    static Tuple2<int> GetWindow(DateTime startDate, DateTime endDate, DataSeries<Bar> s)
    {
      var sList = s.ToList();
      var start = sList.FindIndex(x => x.Timestamp.Date >= startDate.Date);
      var end = sList.FindLastIndex(x => x.Timestamp.Date <= endDate.Date);
      return new Tuple2<int>(start, end);
    }

    public static List<string> GetTickers(string predictedSymbol)
    {
      return Lists.Create(predictedSymbol, "^IXIC", "^GSPC", "^DJI", "^DJT", "^DJU", "^DJA", "^N225", "^BVSP",
                          "^GDAXI", "^FTSE", /*"^CJJ", "USDCHF"*/ "^TYX", "^TNX", "^FVX", "^IRX", /*"EUROD"*/ "^XAU");
    }

    static PreprocessedData GetPreprocessedInput(string predictedSymbol, List<DataSeries<Bar>> clean)
    {
      var aOnly = new List<DataSeries<Value>>();
      var bOnly = new List<DataSeries<Value>>();

      /////////////////////
      // helper functions
      Func<string, DataSeries<Bar>> get = ticker => clean.First(x => x.Symbol == ticker);

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

      #region

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

      #endregion

      var allInputSeries = aOnly.Concat(bOnly).Select(s => s.NormalizeUnit()).ToList();
      if (!allInputSeries.All(s => s.All(x => !double.IsNaN(x.Val))))
        throw new Exception("Some input values are NaN!");

      var unalignedInputs = allInputSeries.SeriesToMatrix();
      return new PreprocessedData(predicted, allInputSeries, unalignedInputs, aOnly.Count);
    }

    static Vec GetIdealOutput(Func<DataSeries<Bar>, double> idealSignal, DataSeries<Bar> predicted)
    {
      var outputSignal = predicted.MapElements<Value>((s, _) => idealSignal(s));
      return new DenseVector(outputSignal.Skip(1).Select(x => x.Val).ToArray());
    }

    static List<DataSeries<Bar>> GetCleanSeries(Database db, string predictedSymbol, List<string> tickers)
    {
      var allSeries = tickers.Select(symbol => new DataSeries<Bar>(symbol, db.QueryAll<DbBar>(x => x.Symbol == symbol, "Timestamp").Select(DbBarToBar))).ToList();
      return CleanSeries(predictedSymbol, allSeries);
    }

    static List<DataSeries<Bar>> GetCleanSeriesFromDisk(string predictedSymbol, List<string> tickers)
    {
      var allSeries = tickers.Select(t => DataImport.LoadVersace(t)).ToList();
      return CleanSeries(predictedSymbol, allSeries);
    }

    static List<DataSeries<Bar>> CleanSeries(string predictedSymbol, List<DataSeries<Bar>> allSeries)
    {
      var predictedSeries = allSeries.First(s => s.Symbol == predictedSymbol);
      return allSeries.Select(s => s == predictedSeries ? s : CleanWithRespectTo(predictedSeries, s)).ToList();
    }


    static Bar DbBarToBar(DbBar x)
    {
      return new Bar(x.Timestamp, x.Open, x.Low, x.High, x.Close, x.Volume);
    }

    static DataSeries<Bar> CleanWithRespectTo(DataSeries<Bar> wrt, DataSeries<Bar> s)
    {
      // fill in missing trainingSet in supplemental instruments
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

  public class PreprocessedData
  {
    public readonly DataSeries<Bar> PredictedSeries;
    public readonly List<DataSeries<Value>> AllInputSeries;
    public readonly Mat Input;
    public readonly int DatabaseAInputLength;

    public PreprocessedData(DataSeries<Bar> predictedSeries, List<DataSeries<Value>> allInputSeries, Mat input, int databaseAInputLength)
    {
      PredictedSeries = predictedSeries;
      AllInputSeries = allInputSeries;
      Input = input;
      DatabaseAInputLength = databaseAInputLength;
    }
  }
}