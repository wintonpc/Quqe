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
      var bars = DataSet.LoadNinjaBars("TQQQ.txt");
      var nn = new NeuralNet(
        new[] { "Open0", "Open1", "Low1", "High1", "Close1" },
        new[] { 5 },
        new[] { "Bias" });

      Func<Bar[], double[]> cookInputs = bs => {
        var z = bs[1].Open;
        return new double[] { bs[0].Open / z, bs[1].Open / z, bs[1].Low / z, bs[1].High / z, bs[1].Close / z };
      };

      var iterations = 5000;
      nn.Anneal(iterations, time => Math.Exp(-(double)time / (double)iterations * 7), net =>
        -1 * Backtest(net, bars, 2, cookInputs).CumulativeProfitPercent);
      var ns = nn.ToString();
      File.WriteAllText("lastnn.txt", ns);
      Console.WriteLine(ns);
      Console.WriteLine("CumulativeProfitPercent = " + Backtest(nn, bars, 2, cookInputs).CumulativeProfitPercent);
      Console.ReadLine();
    }

    static BacktestResults Backtest(NeuralNet nn, List<Bar> bars, int barsToInclude, Func<Bar[], double[]> makeInputs)
    {
      double cpp = 1;
      for (int i = 0; i < bars.Count - barsToInclude + 1; i++)
      {
        var bs = bars.Skip(i).Take(barsToInclude).Reverse().ToArray();
        var inputs = makeInputs(bs);
        var outputs = nn.Propagate(inputs);
        var bias = outputs[0];
        bool goLong = bias >= 0.5;
        if (goLong)
          cpp *= bs[0].Close / bs[0].Open;
        else
          cpp *= bs[0].Open / bs[0].Close;
      }
      return new BacktestResults { CumulativeProfitPercent = cpp };
    }

    public class BacktestResults
    {
      public double CumulativeProfitPercent { get; set; }
    }
  }
}
