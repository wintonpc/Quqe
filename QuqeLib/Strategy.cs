using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;

namespace Quqe
{
  public abstract class Strategy
  {
    public List<StrategyParameter> Parameters;
    protected DataSeries<Bar> Bars { get; private set; }
    public Strategy(IEnumerable<StrategyParameter> sParams)
    {
      Parameters = sParams.ToList();
    }

    public abstract string Name { get; }
    public int NumInputs { get; protected set; }
    public List<String> InputNames { get; protected set; }
    public List<DataSeries<Value>> Inputs { get; protected set; }
    public DataSeries<Value> IdealSignal { get; protected set; }
    public abstract int GenomeSize { get; }

    public virtual void ApplyToBars(DataSeries<Bar> bars)
    {
      if (Bars != null)
        throw new Exception("Bars have already been applied.");
      Bars = bars;
    }
    public abstract DataSeries<Value> MakeSignal(Genome g);
    public abstract double CalculateError(Genome g);
    public abstract double Normalize(double value, DataSeries<Bar> ds);
    public abstract double Denormalize(double value, DataSeries<Bar> ds);
    protected abstract NeuralNet MakeNeuralNet(Genome g);
    public abstract BacktestReport Backtest(Genome g, Account account);

    public Dictionary<string, double> CalculateContributions(Genome g)
    {
      var net = MakeNeuralNet(g);

      var results = List.Repeat(NumInputs, n => {
        var ins = new double[NumInputs];
        ins[n] = 1;
        return new { InputName = InputNames[n], Contribution = Math.Abs(net.Propagate(ins)[0]) };
      });

      var contTotal = results.Sum(r => r.Contribution);

      var pctResults = results.Select(r => new { InputName = r.InputName, ContributionPct = r.Contribution / contTotal });

      var d = new Dictionary<string, double>();
      foreach (var z in pctResults)
        d[z.InputName] = z.ContributionPct;
      return d;
    }

    public static Strategy Make(string strategyName, IEnumerable<StrategyParameter> sParams)
    {
      var className = "Quqe." + strategyName + "Strategy";
      var type = typeof(Strategy).Assembly.GetType(className);
      var ctor = type.GetConstructor(new[] { typeof(IEnumerable<StrategyParameter>) });
      return (Strategy)ctor.Invoke(new object[] { sParams });
    }

    public static IEnumerable<StrategyOptimizerReport> Optimize(string strategyName, DataSeries<Bar> bars, IEnumerable<OptimizerParameter> oParams)
    {
      var reports = Optimizer.OptimizeNeuralStrategy(oParams, sParams => {
        var strat = Make(strategyName, sParams);
        strat.ApplyToBars(bars);
        return strat;
      });
      PrintStrategyOptimizerReports(reports);
      return reports;
    }

    protected BacktestReport GenericBacktest(Genome g, Account account, int lookback, double? maxLossPct)
    {
      var signal = MakeSignal(g);
      var bs = Bars.From(signal.First().Timestamp);
      return BacktestSignal(bs, signal, account, lookback, maxLossPct);
    }

    public static BacktestReport BacktestSignal(DataSeries<Bar> bars, DataSeries<Value> signal, Account account, int lookback, double? maxLossPct)
    {
      var bs = bars.From(signal.First().Timestamp).To(signal.Last().Timestamp);
      var helper = BacktestHelper.Start(bs, account);
      DataSeries.Walk(bs, signal, pos => {
        if (pos < lookback)
          return;

        var shouldBuy = signal[0] >= 0;
        Trace.WriteLine(bs[0].Timestamp.ToString("yyyy-MM-dd") + " signal: " + signal[0].Val);

        double? stopLimit = null;
        if (maxLossPct.HasValue)
        {
          if (shouldBuy)
            stopLimit = (1 - maxLossPct) * bs[0].Open;
          else
            stopLimit = (1 + maxLossPct) * bs[0].Open;
        }

        var size = (long)((account.BuyingPower - account.Padding) / bs[0].Open);
        if (size > 0)
        {
          if (shouldBuy)
            account.EnterLong(bs.Symbol, size, new ExitOnSessionClose(stopLimit), bs.FromHere());
          else
            account.EnterShort(bs.Symbol, size, new ExitOnSessionClose(stopLimit), bs.FromHere());
        }
      });
      return helper.Stop();
    }

    public static void WriteTrades(List<TradeRecord> trades, DateTime now, string genomeName)
    {
      var dirName = "Trades";
      if (!Directory.Exists(dirName))
        Directory.CreateDirectory(dirName);

      var fn = Path.Combine(dirName, string.Format("{0:yyyy-MM-dd-hh-mm-ss} {1}.csv", now, genomeName));

      using (var op = new StreamWriter(fn))
      {
        Action<IEnumerable<object>> writeRow = list => op.WriteLine(list.Join(","));

        writeRow(List.Create("Symbol", "Size", "EntryTime", "ExitTime", "Position", "Entry", "StopLimit", "Exit",
          "Profit", "Loss", "PercentProfit", "PercentLoss"));

        foreach (var t in trades)
          writeRow(List.Create<object>(t.Symbol, t.Size, t.EntryTime, t.ExitTime, t.PositionDirection, t.Entry, t.StopLimit, t.Exit,
            t.Profit, t.Loss, t.PercentProfit, t.PercentLoss));
      }
    }

    protected static List<DataSeries<T>> TrimInputs<T>(IEnumerable<DataSeries<T>> inputs)
      where T : DataSeriesElement
    {
      var firstDate = inputs.Select(x => x.Elements.First().Timestamp).Max();
      return inputs.Select(x => x.From(firstDate)).ToList();
    }

    protected static List<DataSeries> TrimInputs(params DataSeries[] inputs)
    {
      var firstDate = inputs.Select(x => x.Elements.First().Timestamp).Max();
      return inputs.Select(x => x.FromDate(firstDate)).ToList();
    }

    public static void PrintStrategyOptimizerReports(IEnumerable<StrategyOptimizerReport> reports)
    {
      var best = reports.First();
      Trace.WriteLine("-- " + best.StrategyName + " --------------");

      Trace.WriteLine("=== BEST ===");
      Trace.WriteLine(best.ToString());
      Trace.WriteLine("(Saved as " + best.Save(best.StrategyName) + ")");
      Trace.WriteLine("");

      Trace.WriteLine("=== ALL ===");
      Action<IEnumerable<StrategyOptimizerReport>> printReports = list => {
        foreach (var r in list)
          Trace.WriteLine(r.ToString() + "\r\n");
      };

      printReports(reports);
      Trace.WriteLine("--------------------------------");
    }
  }

  public class BuySellStrategy : Strategy
  {
    public override string Name { get { return "BuySell"; } }

    readonly int Lookback = 2;
    readonly List<int> ActivationSpec;

    public BuySellStrategy(IEnumerable<StrategyParameter> sParams)
      : base(sParams)
    {
      InputNames = List.Create("Open0", "Close1");
      NumInputs = InputNames.Count;
      ActivationSpec = List.Create(sParams.Get<int>("Activation1"), sParams.Get<int>("Activation2"));
    }

    public override void ApplyToBars(DataSeries<Bar> bars)
    {
      var ha = bars.HeikenAshi().Delay(1).MapElements<Value>((s, v) => Math.Sign(s[0].Close - s[0].Open));
      base.ApplyToBars(bars.From(ha.First().Timestamp));

      Inputs = TrimInputs(List.Create(
        Bars.MapElements<Value>((s, v) => Normalize(s[0].Open, Bars)),
        Bars.MapElements<Value>((s, v) => Normalize(s[1].Close, Bars))
        ));
      Debug.Assert(NumInputs == Inputs.Count);
      IdealSignal = Bars.MapElements<Value>((s, v) => s[0].IsGreen ? 1 : -1);
    }

    public override int GenomeSize
    {
      get { return new WardNet(NumInputs).ToGenome().Genes.Count; }
    }

    public override DataSeries<Value> MakeSignal(Genome g)
    {
      return Bars.NeuralNet(new WardNet(NumInputs, g, ActivationSpec), Inputs);
    }

    public override double CalculateError(Genome g)
    {
      var signal = MakeSignal(g);
      return Error(signal, IdealSignal)/* * Error(signal.Sign(), IdealSignal)*/;
    }

    static double Error(DataSeries<Value> a, DataSeries<Value> b)
    {
      return a.Variance(b);
    }

    public override double Normalize(double value, DataSeries<Bar> ds)
    {
      return value / ds[Lookback].Close - 1;
      //return value / ds[Lookback].Close;
    }

    public override double Denormalize(double value, DataSeries<Bar> ds)
    {
      return (value + 1) * ds[Lookback].Close;
      //return value * ds[Lookback].Close;
    }

    protected override NeuralNet MakeNeuralNet(Genome g)
    {
      return new WardNet(NumInputs, g);
    }

    public override BacktestReport Backtest(Genome g, Account account)
    {
      return GenericBacktest(g, account, Lookback, null);
    }
  }

  public class ComboStrategy : Strategy
  {
    public override string Name { get { return "Combo"; } }

    readonly double? StopLimitPct;
    string TeachingEndDate = "12/31/2011";
    public ComboStrategy()
      : this(null, (double?)null)
    {
    }

    public ComboStrategy(string teachingEndDate, double? stopLimitPct)
      : base(new List<StrategyParameter>())
    {
      TeachingEndDate = teachingEndDate ?? TeachingEndDate;
      StopLimitPct = stopLimitPct;
    }

    public ComboStrategy(IEnumerable<StrategyParameter> sParams)
      : base(sParams)
    {
    }

    public override DataSeries<Value> MakeSignal(Genome g)
    {
      return DtSignals.DtCombo(Data.Get("TQQQ").To(TeachingEndDate), Bars);
    }

    public override BacktestReport Backtest(Genome g, Account account)
    {
      return GenericBacktest(null, account, 1, StopLimitPct);
    }

    #region not implemented

    public override int GenomeSize
    {
      get { throw new NotImplementedException(); }
    }

    public override double CalculateError(Genome g)
    {
      throw new NotImplementedException();
    }

    public override double Normalize(double value, DataSeries<Bar> ds)
    {
      throw new NotImplementedException();
    }

    public override double Denormalize(double value, DataSeries<Bar> ds)
    {
      throw new NotImplementedException();
    }

    protected override NeuralNet MakeNeuralNet(Genome g)
    {
      throw new NotImplementedException();
    }

    #endregion
  }

  public abstract class BasicStrategy
  {
    public readonly List<StrategyParameter> SParams;

    protected BasicStrategy(IEnumerable<StrategyParameter> sParams)
    {
      SParams = sParams.ToList();
    }

    public abstract DataSeries<Value> MakeSignal(DataSeries<Bar> bars);

    public static BasicStrategy Make(string strategyName, IEnumerable<StrategyParameter> sParams)
    {
      var className = "Quqe." + strategyName + "Strategy";
      var type = typeof(Strategy).Assembly.GetType(className);
      var ctor = type.GetConstructor(new[] { typeof(IEnumerable<StrategyParameter>) });
      return (BasicStrategy)ctor.Invoke(new object[] { sParams });
    }
  }

  public class DTLRR2Strategy : BasicStrategy
  {
    public DTLRR2Strategy(IEnumerable<StrategyParameter> sParams) : base(sParams) { }

    public override DataSeries<Value> MakeSignal(DataSeries<Bar> bars)
    {
      return bars.DecisionTreeSignal(SParams, 0, DtSignals.MakeExamples2);
    }
  }

  public class UDTLRR2Strategy : BasicStrategy
  {
    public UDTLRR2Strategy(IEnumerable<StrategyParameter> sParams) : base(sParams) { }

    int WindowSize = 40;
    int WindowPadding = 25;
    int ReteachInterval = 8;
    int NumIterations = 15000;

    public override DataSeries<Value> MakeSignal(DataSeries<Bar> bars)
    {
      var oParams = List.Create(
        new OptimizerParameter("TOPeriod", 3, 12, 1),
        new OptimizerParameter("TOForecast", 0, 8, 1),
        new OptimizerParameter("TCPeriod", 3, 15, 1),
        new OptimizerParameter("TCForecast", 0, 8, 1),
        new OptimizerParameter("VOPeriod", 3, 10, 1),
        new OptimizerParameter("VOForecast", 0, 2, 1),
        new OptimizerParameter("VCPeriod", 3, 10, 1),
        new OptimizerParameter("VCForecast", 0, 2, 1),
        new OptimizerParameter("ATRPeriod", 8, 12, 1),
        new OptimizerParameter("ATRThresh", 1.0, 2.5, 0.1)
        );

      Func<int, DateTime> offset = n => bars.First().Timestamp.AddDays(n);

      var newElements = new List<Value>();

      Optimizer.ShowTrace = false;
      //for (int w = WindowSize; w < bars.Length - 1; w += ReteachInterval)
      //{
      Parallel.For(0, (bars.Length-WindowSize-WindowPadding) / ReteachInterval, n => {
        var w = WindowPadding + WindowSize + n * ReteachInterval;
        var report = Optimizer.OptimizeDecisionTree("DTLRR2", oParams, NumIterations,
          bars[w - WindowSize].Timestamp, bars.To(bars[w - 1].Timestamp),
          TimeSpan.FromDays(21), TimeSpan.FromDays(WindowPadding), sParams => 0.0, DtSignals.MakeExamples2, true);

        var weekBars = new DataSeries<Bar>(bars.Symbol, bars.Skip(w - WindowPadding).Take(ReteachInterval + WindowPadding));
        var paddedSignal = weekBars.DecisionTreeSignal(report.StrategyParams, 0, DtSignals.MakeExamples2);
        var weekSignal = paddedSignal.Skip(paddedSignal.Length - ReteachInterval);
        lock (newElements)
        {
          newElements.AddRange(weekSignal);
          Trace.WriteLine(string.Format("{0} / {1}", newElements.Count, bars.Length - WindowSize));
        }
      });

      Optimizer.ShowTrace = true;

      foreach (var x in newElements.OrderBy(x => x.Timestamp))
        Trace.WriteLine(x.Timestamp);

      return new DataSeries<Value>(bars.Symbol, newElements.OrderBy(x => x.Timestamp));
    }
  }
}
