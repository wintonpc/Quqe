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
using System.Text.RegularExpressions;

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
        if (pos + 1 < lookback)
          return;

        var shouldBuy = signal[0] >= 0;
        //Trace.WriteLine(bs[0].Timestamp.ToString("yyyy-MM-dd") + " signal: " + signal[0].Val);

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

    public abstract DataSeries<Value> MakeSignal(DateTime trainingStart, DataSeries<Bar> trainingBars, DateTime validationStart, DataSeries<Bar> validationBars);

    public static BasicStrategy Make(string strategyName, IEnumerable<StrategyParameter> sParams)
    {
      var className = "Quqe." + strategyName + "Strategy";
      var type = typeof(Strategy).Assembly.GetType(className);
      var ctor = type.GetConstructor(new[] { typeof(IEnumerable<StrategyParameter>) });
      return (BasicStrategy)ctor.Invoke(new object[] { sParams });
    }
  }

  public abstract class DTStrategy : BasicStrategy
  {
    protected DTStrategy(IEnumerable<StrategyParameter> sParams) : base(sParams) { }
    public abstract IEnumerable<DtExample> MakeDtExamples(DateTime start, DataSeries<Bar> bars);
    public virtual object MakeDt(DateTime trainingStart, DataSeries<Bar> trainingBars)
    {
      var trainingExamples = MakeDtExamples(trainingBars.First().Timestamp, trainingBars).Where(x => x.Timestamp >= trainingStart);
      return DecisionTree.Learn(trainingExamples, Prediction.Green, 0);
    }
    public override DataSeries<Value> MakeSignal(DateTime trainingStart, DataSeries<Bar> trainingBars, DateTime validationStart, DataSeries<Bar> validationBars)
    {
      var dt = MakeDt(trainingStart, trainingBars);
      var validationExamples = MakeDtExamples(trainingBars.First().Timestamp, validationBars).Where(x => x.Timestamp >= validationStart);
      return Transforms.DecisionTreeSignal(dt, validationExamples);
    }

    protected static bool Between(double v, double low, double high)
    {
      return low <= v && v <= high;
    }
  }

  public class DTCandlesStrategy : DTStrategy
  {
    public DTCandlesStrategy(IEnumerable<StrategyParameter> sParams) : base(sParams) { }

    public enum Last2BarColor { Green, Red }
    public enum LastBarColor { Green, Red }
    public enum LastBarSize { Small, Medium, Large }
    public enum GapType { NoneLower, NoneUpper, Up, SuperUp, Down, SuperDown }
    public enum FastEmaSlope { Up, Down }
    public enum SlowEmaSlope { Up, Down }
    public enum Momentum { Positive, Negative }
    public enum Lrr2 { Buy, Sell }
    public enum RSquared { Linear, Nonlinear }
    public enum LinRegSlope { Positive, Negative }

    public override IEnumerable<DtExample> MakeDtExamples(DateTime start, DataSeries<Bar> bars)
    {
      return MakeExamples(SParams, bars).Where(x => x.Timestamp >= start);
    }

    public static IEnumerable<DtExample> MakeExamples(IEnumerable<StrategyParameter> sParams, DataSeries<Bar> carefulBars)
    {
      double smallMax = sParams.Get<double>("SmallMax");
      double mediumMax = sParams.Get<double>("MediumMax");
      double gapPadding = sParams.Get<double>("GapPadding");
      double superGapPadding = sParams.Get<double>("SuperGapPadding");

      int enableBarSizeAveraging = sParams.Get<int>("EnableBarSizeAveraging");
      double smallMaxPct = sParams.Get<double>("SmallMaxPct");
      double largeMinPct = sParams.Get<double>("LargeMinPct");
      int sizeAvgPeriod = sParams.Get<int>("SizeAvgPeriod");

      int SlowEmaPeriod = sParams.Get<int>("SlowEmaPeriod");
      int FastEmaPeriod = sParams.Get<int>("FastEmaPeriod");

      int enableMomentum = sParams.Get<int>("EnableMomentum");
      int momentumPeriod = sParams.Get<int>("MomentumPeriod");

      int enableRSquared = sParams.Get<int>("EnableRSquared");
      int rSquaredPeriod = sParams.Get<int>("RSquaredPeriod");
      double rSquaredThresh = sParams.Get<double>("RSquaredThresh");

      int enableLinRegSlope = sParams.Get<int>("EnableLinRegSlope");
      int linRegSlopePeriod = sParams.Get<int>("LinRegSlopePeriod");

      int enableLrr2 = sParams.Get<int>("EnableLrr2");

      if (carefulBars.Length < 3)
        return new List<DtExample>();

      List<DtExample> examples = new List<DtExample>();
      var fastEmaSlope = carefulBars.Closes().ZLEMA(FastEmaPeriod).Derivative().Delay(1);
      var slowEmaSlope = carefulBars.Closes().ZLEMA(SlowEmaPeriod).Derivative().Delay(1);
      var momo = carefulBars.Closes().Momentum(momentumPeriod).Delay(1);
      var rsquared = carefulBars.Closes().RSquared(rSquaredPeriod).Delay(1);
      var linRegSlope = carefulBars.Closes().LinRegSlope(linRegSlopePeriod).Delay(1);
      var lrr2 = carefulBars.LinRegRel2(); // already delayed!
      var bs = carefulBars.From(fastEmaSlope.First().Timestamp);
      DataSeries.Walk(
        List.Create<DataSeries>(bs, fastEmaSlope, slowEmaSlope, momo, rsquared, linRegSlope, lrr2), pos => {
          if (pos < 2)
            return;
          var a = new List<object>();
          a.Add(bs[1].IsGreen ? LastBarColor.Green : LastBarColor.Red);
          a.Add(bs[2].IsGreen ? Last2BarColor.Green : Last2BarColor.Red);
          if (enableBarSizeAveraging > 0)
          {
            var avgHeight = bs.BackBars(Math.Min(pos + 1, sizeAvgPeriod + 1)).Skip(1).Average(x => x.WaxHeight());
            var r = (bs[0].WaxHeight() - avgHeight) / avgHeight;
            a.Add(
              r < smallMaxPct ? LastBarSize.Small :
              r > largeMinPct ? LastBarSize.Large :
              LastBarSize.Medium);
          }
          else
          {
            a.Add(
              bs[1].WaxHeight() < smallMax ? LastBarSize.Small :
              bs[1].WaxHeight() < mediumMax ? LastBarSize.Medium :
              LastBarSize.Large);
          }
          a.Add(
            Between(bs[0].Open, bs[1].WaxBottom, bs[1].WaxMid()) ? GapType.NoneLower :
            Between(bs[0].Open, bs[1].WaxMid(), bs[1].WaxTop) ? GapType.NoneUpper :
            Between(bs[0].Open, bs[1].WaxTop + gapPadding, bs[1].High) ? GapType.Up :
            Between(bs[0].Open, bs[1].Low, bs[1].WaxBottom - gapPadding) ? GapType.Down :
            bs[0].Open > bs[1].High + superGapPadding ? GapType.SuperUp :
            bs[0].Open < bs[1].Low - superGapPadding ? GapType.SuperDown :
            GapType.NoneLower);
          a.Add(fastEmaSlope[0] >= 0 ? FastEmaSlope.Up : FastEmaSlope.Down);
          a.Add(slowEmaSlope[0] >= 0 ? SlowEmaSlope.Up : SlowEmaSlope.Down);
          if (enableMomentum > 0)
            a.Add(momo[0] >= 0 ? Momentum.Positive : Momentum.Negative);
          if (enableLrr2 > 0)
            a.Add(lrr2[0] >= 0 ? Lrr2.Buy : Lrr2.Sell);
          if (enableLinRegSlope > 0)
            a.Add(linRegSlope[0] >= 0 ? LinRegSlope.Positive : LinRegSlope.Negative);
          if (enableRSquared > 0)
            a.Add(rsquared[0] >= rSquaredThresh ? RSquared.Linear : RSquared.Nonlinear);
          examples.Add(new DtExample(bs[0].Timestamp, bs[0].IsGreen ? Prediction.Green : Prediction.Red, a));
        });
      return examples;
    }
  }

  public class DTBarSumStrategy : DTStrategy
  {
    public DTBarSumStrategy(IEnumerable<StrategyParameter> sParams) : base(sParams) { }

    public override IEnumerable<DtExample> MakeDtExamples(DateTime start, DataSeries<Bar> bars)
    {
      return MakeExamples(SParams, bars).Where(x => x.Timestamp >= start);
    }

    public enum GapType { NoneLower, NoneUpper, Up, SuperUp, Down, SuperDown }
    public enum BarSum { Neutral, Positive, Negative }
    public enum EmaSlope { Neutral, Up, Down }
    public enum OpenRel { Above, Below }

    public static IEnumerable<DtExample> MakeExamples(IEnumerable<StrategyParameter> sParams, DataSeries<Bar> carefulBars)
    {
      double gapPadding = sParams.Get<double>("GapPadding");
      double superGapPadding = sParams.Get<double>("SuperGapPadding");

      int barSumPeriod = sParams.Get<int>("BarSumPeriod");
      int barSumNormalizingPeriod = sParams.Get<int>("BarSumNormalizingPeriod");
      int barSumSmoothing = sParams.Get<int>("BarSumSmoothing");
      int barSumThresh = sParams.Get<int>("BarSumThresh");

      int emaPeriod = sParams.Get<int>("EmaPeriod");
      int emaThresh = sParams.Get<int>("EmaThresh");

      int linRegPeriod = sParams.Get<int>("LinRegPeriod");
      int linRegForecast = sParams.Get<int>("LinRegForecast");

      if (carefulBars.Length < 3)
        return new List<DtExample>();

      List<DtExample> examples = new List<DtExample>();
      var emaSlope = carefulBars.Closes().ZLEMA(emaPeriod).Derivative().Delay(1);
      var barSum = carefulBars.BarSum3(barSumPeriod, barSumNormalizingPeriod, barSumSmoothing).Delay(1);
      var linReg = carefulBars.Midpoint(b => b.WaxBottom, b => b.WaxTop).LinReg(linRegPeriod, linRegForecast).Delay(1);
      var bs = carefulBars.From(emaSlope.First().Timestamp);
      DataSeries.Walk(
        List.Create<DataSeries>(bs, emaSlope, barSum, linReg), pos => {
          if (pos < 2)
            return;
          var a = new List<object>();
          a.Add(
            Between(bs[0].Open, bs[1].WaxBottom, bs[1].WaxMid()) ? GapType.NoneLower :
            Between(bs[0].Open, bs[1].WaxMid(), bs[1].WaxTop) ? GapType.NoneUpper :
            Between(bs[0].Open, bs[1].WaxTop + gapPadding, bs[1].High) ? GapType.Up :
            Between(bs[0].Open, bs[1].Low, bs[1].WaxBottom - gapPadding) ? GapType.Down :
            bs[0].Open > bs[1].High + superGapPadding ? GapType.SuperUp :
            bs[0].Open < bs[1].Low - superGapPadding ? GapType.SuperDown :
            GapType.NoneLower);
          a.Add(
            barSum[0] < -barSumThresh ? BarSum.Negative :
            barSum[0] > barSumThresh ? BarSum.Positive :
            BarSum.Neutral);
          a.Add(
            emaSlope[0] < -emaThresh ? EmaSlope.Down :
            emaSlope[0] > emaThresh ? EmaSlope.Up :
            EmaSlope.Neutral);
          a.Add(linReg[0] > bs[0].Open ? OpenRel.Above : OpenRel.Below);
          examples.Add(new DtExample(bs[0].Timestamp, bs[0].IsGreen ? Prediction.Green : Prediction.Red, a));
        });
      return examples;
    }
  }

  public class FooStrategy : BasicStrategy
  {
    public FooStrategy(IEnumerable<StrategyParameter> sParams) : base(sParams) { }

    public override DataSeries<Value> MakeSignal(DateTime trainingStart, DataSeries<Bar> trainingBars, DateTime validationStart, DataSeries<Bar> validationBars)
    {
      var carefulBars = validationBars.From(validationStart);
      var midSlope = carefulBars.Midpoint(x => x.WaxBottom, x => x.WaxTop).LinRegSlope(2).Delay(1);
      var topForecast = carefulBars.MapElements<Value>((s, v) => s[0].WaxTop).LinReg(2, 1).Delay(1);
      var bottomForecast = carefulBars.MapElements<Value>((s, v) => s[0].WaxBottom).LinReg(2, 1).Delay(1);
      var bs = carefulBars.From(midSlope.First().Timestamp);

      var newElements = new List<Value>();
      DataSeries.Walk(bs, midSlope, topForecast, bottomForecast, pos => {
        //double sig;
        //if (midSlope[0] < 0)
        //{
        //  //if (bs[0].Open < bottomForecast[0])
        //  //  sig = 1;
        //  //else
        //    sig = -1;
        //}
        //else
        //{
        //  //if (bs[0].Open > topForecast[0])
        //  //  sig = -1;
        //  //else
        //    sig = 1;
        //}
        //newElements.Add(new Value(bs[0].Timestamp, sig));
        if (bs.Pos == 0)
          return;
        newElements.Add(new Value(bs[0].Timestamp, bs[1].IsGreen ? 1 : -1));
      });
      return new DataSeries<Value>(bs.Symbol, newElements);
    }
  }

  public class OTPDStrategy : BasicStrategy
  {
    public OTPDStrategy(IEnumerable<StrategyParameter> sParams) : base(sParams) { }

    public override DataSeries<Value> MakeSignal(DateTime trainingStart, DataSeries<Bar> trainingBars, DateTime validationStart, DataSeries<Bar> validationBars)
    {
      var tqqq = Data.Get("QQQ");
      var newElements = new List<Value>();
      using (var ip = new StreamReader("c.txt"))
      {
        string line;
        while ((line = ip.ReadLine()) != null)
        {
          var toks = Regex.Split(line, @"\s+");
          if (toks[0] == "")
            continue;
          var timestamp = DateTime.ParseExact(toks[0], "M/d/yyyy", null);
          var numShares = int.Parse(toks[1]);
          var profit = double.Parse(toks[2]);

          var profitPerShare = profit / numShares;
          var bar = tqqq.FirstOrDefault(x => x.Timestamp == timestamp);
          if (bar == null)
            continue;
          var exitPrice = bar.Open + profitPerShare;
          if (bar.IsGreen && profit > 0)
          {
            Trace.WriteLine(timestamp.ToString("MM/dd/yyyy") + " " + Math.Round(bar.WaxHeight(), 2).ToString("N2") + " "
              + Math.Round(profitPerShare, 2).ToString("N2") + " " + (profitPerShare / bar.WaxHeight()));
          }
        }
      }
      return new DataSeries<Value>(tqqq.Symbol, newElements);
    }
  }

  public class Trending1Strategy : BasicStrategy
  {
    public Trending1Strategy(IEnumerable<StrategyParameter> sParams) : base(sParams) { }

    public override DataSeries<Value> MakeSignal(DateTime trainingStart, DataSeries<Bar> trainingBars, DateTime validationStart, DataSeries<Bar> validationBars)
    {
      var wo = SParams.Get<double>("WO");
      var wl = SParams.Get<double>("WL");
      var wh = SParams.Get<double>("WH");
      var wc = SParams.Get<double>("WC");

      var fastRegPeriod = SParams.Get<int>("FastRegPeriod");
      var slowRegPeriod = SParams.Get<int>("SlowRegPeriod");

      var rSquaredPeriod = SParams.Get<int>("RSquaredPeriod");
      var rSquaredThresh = SParams.Get<double>("RSquaredThresh");
      var linRegSlopePeriod = SParams.Get<int>("LinRegSlopePeriod");

      var bars = validationBars.From(validationStart);
      var vs = bars.Weighted(wo, wl, wh, wc);
      var fastReg = vs.LinReg(fastRegPeriod, 1).Delay(1);
      var slowReg = vs.LinReg(slowRegPeriod, 1).Delay(1);
      var rSquared = vs.RSquared(rSquaredPeriod).Delay(1);
      var linRegSlope = vs.LinRegSlope(linRegSlopePeriod).Delay(1);
      var bs = bars.From(fastReg.First().Timestamp);

      var newElements = new List<Value>();
      DataSeries.Walk(bs, fastReg, slowReg, rSquared, linRegSlope, pos => {
        double sig;
        if (rSquared[0] > rSquaredThresh)
        {
          var slope = Math.Sign(linRegSlope[0]);
          sig = slope == 0 ? 1 : slope;
        }
        else
          sig = fastReg[0] >= slowReg[0] ? 1 : -1;
        newElements.Add(new Value(bs[0].Timestamp, sig));
      });

      return new DataSeries<Value>(bars.Symbol, newElements);
    }
  }

  /*

  public class DTLRR2Strategy : BasicStrategy
  {
    public DTLRR2Strategy(IEnumerable<StrategyParameter> sParams) : base(sParams) { }

    public override DataSeries<Value> MakeSignal(DataSeries<Bar> bars)
    {
      var examples = DtSignals.MakeExamples2(SParams, bars);
      var dt = DecisionTree.Learn(examples, Prediction.Green, 0);
      return Transforms.DecisionTreeSignal(dt, examples, 0);
    }
  }

  public class UDTLRR2Strategy : BasicStrategy
  {
    public UDTLRR2Strategy(IEnumerable<StrategyParameter> sParams) : base(sParams) { }

    public int WindowSize = 30;
    int WindowPadding = 25;
    public int ReteachInterval = 1;
    public int NumIterations = 5000;

    //public override DataSeries<Value> MakeSignal(DataSeries<Bar> bars)
    //{
    //  var oParams = List.Create(
    //    new OptimizerParameter("TOPeriod", 3, 15, 1),
    //    new OptimizerParameter("TOForecast", 0, 8, 1),
    //    new OptimizerParameter("TCPeriod", 3, 15, 1),
    //    new OptimizerParameter("TCForecast", 0, 8, 1),
    //    new OptimizerParameter("VOPeriod", 3, 12, 1),
    //    new OptimizerParameter("VOForecast", 0, 2, 1),
    //    new OptimizerParameter("VCPeriod", 3, 12, 1),
    //    new OptimizerParameter("VCForecast", 0, 2, 1),
    //    new OptimizerParameter("ATRPeriod", 7, 15, 1),
    //    new OptimizerParameter("ATRThresh", 1.0, 2.5, 0.1)
    //    );

    //  int maxBad = 2;
    //  int evalWindowSize = 6;

    //  var firstOffset = WindowPadding + WindowSize;
    //  List<StrategyParameter> currentSParams = null;
    //  object currentDt = null;
    //  List<Value> signalElements = new List<Value>();

    //  Func<int, bool> tooManyBad = i => {
    //    var numBad = 0;
    //    for (int j = i - evalWindowSize; j < i; j++)
    //      if ((signalElements[i - j - 1] >= 0) != bars[j].IsGreen)
    //        numBad++;
    //    return numBad > maxBad;
    //  };

    //  for (int i = firstOffset; i < bars.Length; i++)
    //  {
    //    if (currentSParams == null || (i >= evalWindowSize + firstOffset && tooManyBad(i)))
    //    {
    //      var report = Optimizer.OptimizeDecisionTree("DTLRR2", oParams, NumIterations,
    //        bars[i - WindowSize].Timestamp, bars.To(bars[i - 1].Timestamp),
    //        TimeSpan.FromDays(WindowPadding), sParams => 0.0, DtSignals.MakeExamples2, true);
    //      currentSParams = report.StrategyParams;

    //      currentDt = DecisionTree.Learn(
    //        DtSignals.MakeExamples2(currentSParams, bars.From(bars[i - WindowSize].Timestamp).To(bars[i - 1].Timestamp)),
    //        Prediction.Green, 0);
    //    }

    //    signalElements.Add(Transforms.DecisionTreeSignal(currentDt,
    //      DtSignals.MakeExamples2(currentSParams, new DataSeries<Bar>("DT", bars.Skip(i - 2).Take(3))), 0).First());
    //  }

    //  return new DataSeries<Value>(bars.Symbol, signalElements);
    //}

    public override DataSeries<Value> MakeSignal(DataSeries<Bar> bars)
    {
      var oParams = List.Create(
        new OptimizerParameter("TOPeriod", 3, 15, 1),
        new OptimizerParameter("TOForecast", 0, 8, 1),
        new OptimizerParameter("TCPeriod", 3, 15, 1),
        new OptimizerParameter("TCForecast", 0, 8, 1),
        new OptimizerParameter("VOPeriod", 3, 12, 1),
        new OptimizerParameter("VOForecast", 0, 2, 1),
        new OptimizerParameter("VCPeriod", 3, 12, 1),
        new OptimizerParameter("VCForecast", 0, 2, 1),
        new OptimizerParameter("ATRPeriod", 7, 15, 1),
        new OptimizerParameter("ATRThresh", 1.0, 2.5, 0.1)
        );

      Func<int, DateTime> offset = n => bars.First().Timestamp.AddDays(n);

      var newElements = new List<Value>();

      var numIterations = (bars.Length - WindowSize - WindowPadding) / ReteachInterval;

      Action<int> iterate = n => {
        var w = WindowPadding + WindowSize + n * ReteachInterval;
        var report = Optimizer.OptimizeDecisionTree("DTLRR2", oParams, NumIterations,
          bars[w - WindowSize].Timestamp, bars.To(bars[w - 1].Timestamp),
          TimeSpan.FromDays(WindowPadding), sParams => 0.0, DtSignals.MakeExamples2, true);

        var weekBars = new DataSeries<Bar>(bars.Symbol, bars.Skip(w - WindowPadding).Take(ReteachInterval + WindowPadding));
        var dt = DecisionTree.Learn(
          DtSignals.MakeExamples2(report.StrategyParams, bars.From(bars[w - WindowSize].Timestamp).To(bars[w - 1].Timestamp)),
          Prediction.Green, 0);
        var paddedSignal = Transforms.DecisionTreeSignal(dt, DtSignals.MakeExamples2(report.StrategyParams, weekBars), 0);
        var weekSignal = paddedSignal.Skip(paddedSignal.Length - ReteachInterval);
        lock (newElements)
        {
          newElements.AddRange(weekSignal);
          if (!Optimizer.ParallelizeStrategyOptimization)
            Trace.WriteLine(string.Format("Signal {0} / {1}", newElements.Count, numIterations * ReteachInterval));
        }
      };

      var oldTrace = Optimizer.ShowTrace;
      Optimizer.ShowTrace = false;
      if (Optimizer.ParallelizeStrategyOptimization)
      {
        for (int n = 0; n < numIterations; n++)
          iterate(n);
      }
      else
      {
        Parallel.For(0, numIterations, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, iterate);
      }

      Optimizer.ShowTrace = oldTrace;

      return new DataSeries<Value>(bars.Symbol, newElements.OrderBy(x => x.Timestamp));
    }
  }
   */
}
