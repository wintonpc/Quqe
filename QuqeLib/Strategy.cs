using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Diagnostics;

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
      if (strategyName == "Midpoint")
        return new MidpointStrategy(sParams);
      if (strategyName == "BuySell")
        return new BuySellStrategy(sParams);
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

    static void PrintStrategyOptimizerReports(IEnumerable<StrategyOptimizerReport> reports)
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

    public BuySellStrategy(IEnumerable<StrategyParameter> sParams)
      :base(sParams)
    {
      NumInputs = 2;
      InputNames = List.Create("Open0", "Close1");
    }

    public override void ApplyToBars(DataSeries<Bar> bars)
    {
      base.ApplyToBars(bars);

      Inputs = List.Create(
        bars.MapElements<Value>((s, v) => Normalize(s[0].Open, bars)),
        bars.MapElements<Value>((s, v) => Normalize(s[1].Close, bars))
        );
      Debug.Assert(NumInputs == Inputs.Count);
      IdealSignal = bars.MapElements<Value>((s, v) => s[0].IsGreen ? 1 : -1);
    }

    public override int GenomeSize
    {
      get { return new WardNet(NumInputs).ToGenome().Genes.Count; }
    }

    public override DataSeries<Value> MakeSignal(Genome g)
    {
      return Bars.NeuralNet(new WardNet(NumInputs, g), Inputs);
    }

    public override double CalculateError(Genome g)
    {
      return Error(MakeSignal(g), IdealSignal);
    }

    static double Error(DataSeries<Value> a, DataSeries<Value> b)
    {
      return a.Variance(b);
    }

    public override double Normalize(double value, DataSeries<Bar> ds)
    {
      //return value / ds[Lookback].Close - 1;
      return value / ds[Lookback].Close;
    }

    public override double Denormalize(double value, DataSeries<Bar> ds)
    {
      //return (value + 1) * ds[Lookback].Close;
      return value * ds[Lookback].Close;
    }

    protected override NeuralNet MakeNeuralNet(Genome g)
    {
      return new WardNet(NumInputs, g);
    }

    public override BacktestReport Backtest(Genome g, Account account)
    {
      var signal = MakeSignal(g);
      var helper = BacktestHelper.Start(Bars, account);
      DataSeries.Walk(Bars, signal, pos => {
        if (pos < Lookback)
          return;

        var shouldBuy = signal[0] >= 0;
        var size = (long)((account.BuyingPower - account.Padding) / Bars[0].Open);
        if (size > 0)
        {
          if (shouldBuy)
            account.EnterLong(Bars.Symbol, size, new ExitOnSessionClose(), Bars.FromHere());
          else
            account.EnterShort(Bars.Symbol, size, new ExitOnSessionClose(), Bars.FromHere());
        }
      });
      return helper.Stop();
    }
  }

  public class MidpointStrategy : Strategy
  {
    public override string Name { get { return "Midpoint"; } }

    public readonly int NumHidden;
    public readonly int Lookback;

    public MidpointStrategy(IEnumerable<StrategyParameter> sParams)
      : base(sParams)
    {
      NumHidden = sParams.Get<int>("NumHidden");
      Lookback = sParams.Get<int>("Lookback");
      NumInputs = Lookback - 1;
      InputNames = List.Repeat(NumInputs, n => "mid" + (n + 1));
    }

    protected override NeuralNet MakeNeuralNet(Genome g)
    {
      return new NeuralNet(NumInputs, NumHidden, g);
    }

    public override void ApplyToBars(DataSeries<Bar> bars)
    {
      base.ApplyToBars(bars);

      Inputs = List.Repeat(Lookback - 1, n => Bars.MapElements<Value>((s, v) => Normalize(s[n + 1].Midpoint, s)));
      IdealSignal = Bars.MapElements<Value>((s, v) => s[0].Midpoint);
    }

    public override int GenomeSize
    {
      get { return new NeuralNet(NumInputs, NumHidden).ToGenome().Genes.Count; }
    }

    public override DataSeries<Value> MakeSignal(Genome g)
    {
      var output = Bars.NeuralNet(new NeuralNet(NumInputs, NumHidden, g), Inputs);
      var denormalized = Bars.ZipElements<Value, Value>(output, (bars, s, v) => {
        return s.Pos < Lookback ? 0 : Denormalize(s[0], bars);
      });
      return denormalized;
    }

    public override double CalculateError(Genome g)
    {
      var signal = MakeSignal(g);
      return Error(signal, IdealSignal) * Bars.ZipElements<Value, Value>(signal, (s, m, v) =>
          (s[0].WaxBottom < m[0] && m[0] < s[0].WaxTop) ? 0 : 1).Sum(x => x.Val);
    }

    static double Error(DataSeries<Value> a, DataSeries<Value> b)
    {
      return a.Variance(b);
    }

    public override double Normalize(double value, DataSeries<Bar> ds)
    {
      return value / ds[Lookback].Midpoint - 1;
    }

    public override double Denormalize(double value, DataSeries<Bar> ds)
    {
      return (value + 1) * ds[Lookback].Midpoint;
    }

    public override BacktestReport Backtest(Genome g, Account account)
    {
      throw new NotImplementedException();
    }
  }

  public class MidFit : Strategy
  {
    public override string Name { get { return "MidFit"; } }

    readonly int LineSamples;
    readonly int QuadSamples;

    public MidFit(IEnumerable<StrategyParameter> sParams)
      : base(sParams)
    {
      LineSamples = sParams.Get<int>("LineSamples");
      QuadSamples = sParams.Get<int>("QuadSamples");
    }

    class FitResult
    {
      public double NextY;
      public double Error;
    }

    public override DataSeries<Value> MakeSignal(Genome g)
    {
      var s = Bars;
      var newElements = new List<Value>();
      var maxSamplesNeeded = List.Create(LineSamples, QuadSamples).Max();
      DataSeries.Walk(s, pos => {
        if (pos < maxSamplesNeeded)
        {
          newElements.Add(new Value(s[0].Timestamp, s[0].Midpoint));
          return;
        }

        var lineFit = FitLine(List.Repeat(LineSamples, n => s[LineSamples - n].Midpoint));
        var quadFit = FitQuad(List.Repeat(QuadSamples, n => s[QuadSamples - n].Midpoint));

        var bestFit = List.Create(lineFit, quadFit)
          .OrderBy(x => x.Error).First();
        newElements.Add(new Value(s[0].Timestamp, bestFit.NextY));
      });

      return new DataSeries<Value>(Bars.Symbol, newElements);
    }

    static FitResult FitLine(List<double> ys)
    {
      var A = new DenseMatrix(ys.Count, 2);
      for (int i = 0; i < ys.Count; i++)
      {
        A[i, 0] = i;
        A[i, 1] = 1;
      }
      var B = new DenseMatrix(ys.Count, 1);
      for (int i = 0; i < ys.Count; i++)
        B[i, 0] = ys[i];

      var At = A.Transpose();

      var coefs = (At * A).Inverse() * At * B;

      var a = coefs[0, 0];
      var b = coefs[1, 0];

      var fit = List.Repeat(ys.Count, x => a * x + b);
      return new FitResult { Error = Error(ys, fit), NextY = a * ys.Count + b };
    }

    static FitResult FitQuad(List<double> ys)
    {
      var A = new DenseMatrix(ys.Count, 3);
      for (int i = 0; i < ys.Count; i++)
      {
        A[i, 0] = i * i;
        A[i, 1] = i;
        A[i, 2] = 1;
      }
      var B = new DenseMatrix(ys.Count, 1);
      for (int i = 0; i < ys.Count; i++)
        B[i, 0] = ys[i];

      var At = A.Transpose();

      var coefs = (At * A).Inverse() * At * B;

      var a = coefs[0, 0];
      var b = coefs[1, 0];
      var c = coefs[2, 0];

      var fit = List.Repeat(ys.Count, x => a * x * x + b * x + c);
      var nextX = ys.Count;
      return new FitResult { Error = Error(ys, fit), NextY = a * nextX * nextX + b * nextX + c };
    }

    static double Error(List<double> y1, List<double> y2)
    {
      return y1.Zip(y2, (q, r) => Math.Pow(q - r, 2)).Sum();
    }

    public override double Normalize(double value, DataSeries<Bar> ds)
    {
      throw new NotImplementedException();
    }

    public override double Denormalize(double value, DataSeries<Bar> ds)
    {
      throw new NotImplementedException();
    }

    public override BacktestReport Backtest(Genome g, Account account)
    {
      throw new NotImplementedException();
    }

    #region Not Implemented

    protected override NeuralNet MakeNeuralNet(Genome g)
    {
      throw new NotImplementedException();
    }

    public override int GenomeSize
    {
      get { throw new NotImplementedException(); }
    }

    public override double CalculateError(Genome g)
    {
      throw new NotImplementedException();
    }

    #endregion
  }
}
