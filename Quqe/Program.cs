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
      var allBars = DataSet.LoadNinjaBars("TQQQ.txt");
      var nn = new NeuralNet(
        new[] { "Open0", "Open1", "Low1", "High1", "Close1" },
        new[] { 5 },
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

      var iterations = 3000;
      nn.Anneal(iterations, time => Math.Exp(-(double)time / (double)iterations * 7),
        net => -1 * Backtest(net, trainingSet, inputBarCount, cookInputs).ProfitFactor);

      nn.GradientlyDescend(0.8, 0.00001, MakeExamples(trainingSet, 5).Select(bars => new Example {
        Inputs = cookInputs(bars),
        BestOutputs = calcOutputs(bars)
      }).ToList(), () => {
        Console.WriteLine(nn.ToString());
        Console.WriteLine("ProfitFactor: " + Backtest(nn, trainingSet, inputBarCount, cookInputs).ProfitFactor);
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
  }
}
