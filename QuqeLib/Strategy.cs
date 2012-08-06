using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Diagnostics;
using System.Threading.Tasks;

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
      if (strategyName == "BuySell")
        return new BuySellStrategy(sParams);
      if (strategyName == "Combo")
        return new ComboStrategy(sParams);
      else
        throw new Exception("didn't expect " + strategyName);
    }

    public static IEnumerable<StrategyOptimizerReport> Optimize(string strategyName, DataSeries<Bar> bars, IEnumerable<OptimizerParameter> oParams, OptimizationType oType, EvolutionParams eParams = null)
    {
      var reports = Optimizer.OptimizeNeuralIndicator(oParams, oType, eParams, sParams => {
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

    protected static List<DataSeries<T>> TrimInputs<T>(IEnumerable<DataSeries<T>> inputs)
      where T: DataSeriesElement
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

  public class UpdatingBuySellStrategy : Strategy
  {
    public override string Name { get { return "UpdatingBuySell"; } }

    int MinTrainingSize = 15;
    int MaxTrainingSize = 39;
    //int MinTrainingSize = 30;
    //int MaxTrainingSize = 30;
    int WindowStepSize = 4;

    public UpdatingBuySellStrategy(IEnumerable<StrategyParameter> sParams)
      : base(sParams)
    {
    }

    class Trial
    {
      public int WindowSize;
      public double PredictionError;
    }

    public override DataSeries<Value> MakeSignal(Genome notUsed)
    {
      List<Value> signalElements = new List<Value>();
      for (int i = 0; i <= MaxTrainingSize; i++)
        signalElements.Add(new Value(Bars[i].Timestamp, 0));

      Optimizer.ShowTrace = false;

      int bestTrainingSize = 0;

      var sizeSma = Optimizer.MakeSMA(5);

      for (int i = MaxTrainingSize + 1; i < Bars.Length; i++)
      {
        Trace.WriteLine("UpdateBuySell [ " + i + " / " + Bars.Length + " ]    bestTrainingSize = " + bestTrainingSize);

        // optimize and evaluate various windows sizes for the last known bar
        var trials = new List<Trial>();
        Parallel.For(0, (MaxTrainingSize - MinTrainingSize) / WindowStepSize + 1, j => {
          //for (int size = MinTrainingSize; size<=MaxTrainingSize; size += WindowStepSize)
          //{

          var size = MinTrainingSize + j * WindowStepSize;

          // optimize a neural net for each training size with i-2 as the last training bar
          var trainingSet = new DataSeries<Bar>(Bars.Symbol, Bars.Skip(i - size - 1).Take(size));
          var oStrat = new BuySellStrategy(Parameters);
          oStrat.ApplyToBars(trainingSet);
          var genome = Optimizer.OptimizeNeuralGenome(oStrat, OptimizationType.Anneal);

          // evaluate performance of each neural net in predicting bar i-1
          var validationSet = new DataSeries<Bar>(Bars.Symbol, Bars.Skip(i - size - 1).Take(size + 1));
          var vStrat = new BuySellStrategy(Parameters);
          vStrat.ApplyToBars(validationSet);
          var vSignalAll = vStrat.MakeSignal(genome);
          double correctPct = (double)validationSet.From(vSignalAll.First().Timestamp)
            .ZipElements<Value, Value>(vSignalAll, (v, s, x) =>
            ((s[0] > 0) == v[0].IsGreen) ? 1 : 0).Sum(x => x.Val) / validationSet.Length;
          double vSignal = vSignalAll.Last().Val;

          lock (trials)
          {
            trials.Add(new Trial {
              WindowSize = size,
              PredictionError = Math.Pow(vSignal - (validationSet.Last().IsGreen ? 1 : -1), 2)
            });
          }
        });

        // bestTrainingSize = training size of best performing neural net
        bestTrainingSize = trials.OrderBy(t => t.PredictionError).First().WindowSize;

        //bestTrainingSize = 30;

        int sizeToUse = (int)sizeSma(bestTrainingSize);

        // let bestNet = optimize a neural net on bars i-bestTrainingSize..i-1
        var currentTrainingSet = new DataSeries<Bar>(Bars.Symbol, Bars.Skip(i - sizeToUse).Take(sizeToUse));
        var bestStrat = new BuySellStrategy(Parameters);
        bestStrat.ApplyToBars(currentTrainingSet);
        var bestGenome = Optimizer.OptimizeNeuralGenome(bestStrat, OptimizationType.Anneal);

        var currentValidationSet = new DataSeries<Bar>(Bars.Symbol, Bars.Skip(i - sizeToUse).Take(sizeToUse + 1));
        var finalStrat = new BuySellStrategy(Parameters);
        finalStrat.ApplyToBars(currentValidationSet);
        var sig = finalStrat.MakeSignal(bestGenome).Last();
        signalElements.Add(new Value(Bars[i].Timestamp, sig.Val));

        Trace.WriteLine("got it right? " + (Bars[i].IsGreen == (sig.Val > 0)));
      }
      Optimizer.ShowTrace = true;

      return new DataSeries<Value>(Bars.Symbol, signalElements);
    }

    public override BacktestReport Backtest(Genome g, Account account)
    {
      return GenericBacktest(g, account, MaxTrainingSize + 1, null);
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
}
