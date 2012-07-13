using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Quqe
{
  class Program
  {
    static void Main(string[] args)
    {
      //DoSimpler();
      var allBars = DataSet.LoadNinjaBars("TQQQ.txt");
      var nn = new NeuralNet(
        new[] { "Open0", "Open1", "Low1", "High1", "Close1" },
        new int[] { },
        new[] { "Bias" });

      int inputBarCount = 2;

      double trainingPercentage = 0.8;
      var traningSetCount = (int)(allBars.Count * trainingPercentage);
      var trainingSet = allBars.Take(traningSetCount).ToList();
      var validationSet = allBars.Skip(traningSetCount).ToList();

      Func<IEnumerable<Bar>, double[]> cookInputs = bars => {
        var bs = bars.Reverse().ToArray();
        var z = bs[1].Open;
        return new double[] { bs[0].Open / z, bs[1].Open / z, bs[1].Low / z, bs[1].High / z, bs[1].Close / z };
      };

      Func<IEnumerable<Bar>, double[]> calcOutputs = bars => {
        var bs = bars.Reverse().ToArray();
        return new double[] { bs[0].Close > bs[0].Open ? 1 : 0 };
      };

      var iterations = 2000;
      nn.Anneal(iterations, time => Math.Exp(-(double)time / (double)iterations * 7),
        net => -1 * Backtest(net, trainingSet, inputBarCount, cookInputs).ProfitFactor);

      double bestTest = 0;
      double bestValidation = 0;
      nn.GradientlyDescend(1, 0.00001, MakeExamples(trainingSet, 10).Select(bars => new Example {
        Inputs = cookInputs(bars),
        BestOutputs = calcOutputs(bars)
      }).ToList(), () => {
        //Console.WriteLine(nn.ToString());
        var test = Backtest(nn, trainingSet, inputBarCount, cookInputs).ProfitFactor;
        var validation = Backtest(nn, validationSet, inputBarCount, cookInputs).ProfitFactor;
        bestTest = Math.Max(bestTest, test);
        bestValidation = Math.Max(bestValidation, validation);
        Console.WriteLine(string.Format("ProfitFactor: {0:N9}\t{1:N9}\t{2:N9}\t{3:N9}", test, validation, bestTest, bestValidation));
        Console.ReadLine();
      });

      var ns = nn.ToString();
      File.WriteAllText("lastnn.txt", ns);
      Console.WriteLine(ns);

      Console.WriteLine();
      Console.WriteLine("-- Training Set --");
      var backtestInfo = Backtest(nn, trainingSet, inputBarCount, cookInputs);
      Console.WriteLine("ProfitFactor: " + backtestInfo.ProfitFactor);
      Console.WriteLine("WinningPercentage: " + backtestInfo.WinningPercentage.ToString("N1") + "%");

      Console.WriteLine();
      Console.WriteLine("-- Validation Set --");
      backtestInfo = Backtest(nn, validationSet, inputBarCount, cookInputs);
      Console.WriteLine("ProfitFactor: " + backtestInfo.ProfitFactor);
      Console.WriteLine("WinningPercentage: " + backtestInfo.WinningPercentage.ToString("N1") + "%");
      Console.ReadLine();
    }

    static List<List<Bar>> MakeExamples(List<Bar> allBars, int barsPerExample)
    {
      List<List<Bar>> results = new List<List<Bar>>();
      for (int i = 0; i < allBars.Count - barsPerExample + 1; i++)
        results.Add(allBars.Skip(i).Take(barsPerExample).ToList());
      return results;
    }

    static BacktestResults Backtest(NeuralNet nn, List<Bar> bars, int barsToInclude, Func<IEnumerable<Bar>, double[]> makeInputs)
    {
      double profitFactor = 1;
      int totalGuesses = bars.Count - barsToInclude + 1;
      int winningGuesses = 0;
      for (int i = 0; i < bars.Count - barsToInclude + 1; i++)
      {
        var window = bars.Skip(i).Take(barsToInclude);
        var inputs = makeInputs(window);
        var outputs = nn.Propagate(inputs);
        var bias = outputs[0];
        bool goLong = bias >= 0.5;
        var bs = window.Reverse().ToArray();
        if (goLong)
        {
          profitFactor *= bs[0].Close / bs[0].Open;
          if (bs[0].Close > bs[0].Open)
            winningGuesses++;
        }
        else
        {
          profitFactor *= bs[0].Open / bs[0].Close;
          if (bs[0].Open > bs[0].Close)
            winningGuesses++;
        }
      }
      return new BacktestResults {
        ProfitFactor = profitFactor,
        WinningPercentage = (double)winningGuesses / totalGuesses * 100
      };
    }

    public class BacktestResults
    {
      public double ProfitFactor { get; set; }
      public double WinningPercentage { get; set; }
    }

    static void DoSimple()
    {
      var nn = new NeuralNet(
        new[] { "c1", "c2", "c3", "c4", "c5" },
        new[] { 10 },
        new[] { "is0", "is1", "is2", "is3", "is4", "is5", "is6", "is7", "is8", "is9" });

      var inputSet = new[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" }.ToList();

      Func<string, double[]> cook = s => s.PadRight(5).Select(c => (double)(int)c).ToArray();
      Func<string, double[]> makeCorrectOuputs = s => {
        var outputs = new double[10];
        outputs[inputSet.IndexOf(s)] = 1;
        return outputs;
      };

      //nn.Anneal(50000, null, net => {
      //  double cost = 0;
      //  foreach (var s in inputSet)
      //  {
      //    var inputs = cook(s);
      //    var correctOutputs = makeCorrectOuputs(s);
      //    var experimentalOutputs = net.Propagate(inputs);
      //    for (int i = 0; i < correctOutputs.Length; i++)
      //      cost += Math.Pow(correctOutputs[i] - experimentalOutputs[i], 2);
      //  }
      //  return -cost;
      //});

      //Console.ReadLine();

      nn.SetWeights(1);
      nn.GradientlyDescend(20, 0.0000001, inputSet.Select(s => {
        return new Example {
          Inputs = cook(s),
          BestOutputs = makeCorrectOuputs(s)
        };
      }).ToList(), () => {
        Console.WriteLine(string.Join("\t", nn.Propagate(cook("five")).Select(d => d.ToString("N4")).ToArray()));
      });
    }

    static double OutputError(double[] experimental, double[] correct)
    {
      double error = 0;
      for (int i = 0; i < experimental.Length; i++)
        error += Math.Pow(experimental[i] - correct[i], 2);
      return error;
    }

    static double TotalOutputError(NeuralNet nn, IEnumerable<double[]> inputSet, IEnumerable<double[]> outputSet)
    {
      return inputSet.Zip(outputSet, (i, o) => OutputError(nn.Propagate(i), o)).Sum();
    }

    static void DoSimpler()
    {
      var nn = new NeuralNet(
        new[] { "c1", "c2" },
        new int[] { },
        new[] { "isBoy", "isGirl" });

      var inputSet = new[] { "xx", "xy", "yx" }.ToList();

      Func<string, double[]> cook = s => s.Select(c => c == 'x' ? 0.0 : 1.0).ToArray();
      Func<string, double[]> makeCorrectOuputs = s => s.Contains('y') ? new double[] { 1, 0 } : new double[] { 0, 1 };

      nn.Anneal(100000, null, net => TotalOutputError(net, inputSet.Select(cook), inputSet.Select(makeCorrectOuputs)));

      nn.GradientlyDescend(1, 0.0000001, inputSet.Select(s => {
        return new Example {
          Inputs = cook(s),
          BestOutputs = makeCorrectOuputs(s)
        };
      }).ToList(), () => {
        Console.WriteLine(string.Join("\t", nn.Propagate(cook("xx")).Select(d => d.ToString("N4")).ToArray()));
      });
    }
  }
}
