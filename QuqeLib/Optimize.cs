﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Quqe
{
  public class OptimizerParameter
  {
    public readonly string Name;
    public readonly double Low;
    public readonly double High;
    public readonly double Granularity;
    public OptimizerParameter(string name, double low, double high, double granularity)
    {
      Name = name;
      Low = low;
      High = high;
      Granularity = granularity;
    }
  }

  public class StrategyParameter
  {
    public readonly string Name;
    public readonly double Value;
    public StrategyParameter(string name, double value)
    {
      Name = name;
      Value = value;
    }
  }

  public static class StrategyParameterHelpers
  {
    public static T Get<T>(this IEnumerable<StrategyParameter> sParams, string name)
    {
      var pair = sParams.FirstOrDefault(sp => sp.Name == name);
      double value = pair == null ? 0 : pair.Value;
      return (T)Convert.ChangeType(value, typeof(T));
    }
  }

  public class StrategyOptimizerReport
  {
    public string StrategyName;
    public List<StrategyParameter> StrategyParams;
    public string GenomeName;
    public double Fitness;

    public static readonly string StrategyDir = @"c:\Users\Wintonpc\git\Quqe\Share\Strategies";
    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.AppendLine("Strategy : " + StrategyName);
      sb.AppendLine("Genome   : " + (GenomeName ?? "(none)"));
      sb.AppendLine("Fitness  : " + Fitness);
      foreach (var sp in StrategyParams)
        sb.AppendLine(sp.Name + ": " + sp.Value);
      return sb.ToString();
    }

    public string Save(string prefix)
    {
      var fn = FileHelper.NextNumberedFilename(StrategyDir, prefix);
      File.WriteAllText(fn, this.ToString());
      return Path.GetFileNameWithoutExtension(fn);
    }

    public static StrategyOptimizerReport Load(string name)
    {
      using (var ip = new StreamReader(Path.Combine(StrategyDir, name + ".txt")))
      {
        Func<string[]> nextLine = () => {
          var line = ip.ReadLine();
          if (line == null) return null;
          return line.Split(new[] { ':', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        };
        var r = new StrategyOptimizerReport();
        r.StrategyName = nextLine()[1];
        r.GenomeName = nextLine()[1];
        r.Fitness = double.Parse(nextLine()[1]);

        var sParams = new List<StrategyParameter>();
        string[] lin;
        while ((lin = nextLine()) != null)
          sParams.Add(new StrategyParameter(lin[0], double.Parse(lin[1])));
        r.StrategyParams = sParams;
        return r;
      }
    }

    public static BasicStrategy CreateStrategy(string name)
    {
      var report = Load(name);
      return BasicStrategy.Make(report.StrategyName, report.StrategyParams);
    }
  }

  static class FileHelper
  {
    public static string NextNumberedFilename(string dir, string prefix)
    {
      if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);

      var nums = Directory.EnumerateFiles(dir).Select(f => int.Parse(Regex.Replace(Path.GetFileNameWithoutExtension(f), @"^[^\-]+\-", "")));
      var lastNum = nums.Any() ? nums.Max() : 0;
      var number = (lastNum + 1).ToString("D6");
      var name = number;
      return Path.Combine(dir, prefix + "-" + name + ".txt");
    }
  }

  public delegate StrategyOptimizerReport OptimizeKernelFunc(List<StrategyParameter> sParams);

  public class Genome
  {
    public List<double> Genes;
    public double? Fitness;
    public int Size { get { return Genes.Count; } }

    static string GenomesDir = "Genomes";
    public string Save()
    {
      var fn = FileHelper.NextNumberedFilename(GenomesDir, "Genome");
      File.WriteAllText(fn, Genes.Join("\t"));
      return Path.GetFileNameWithoutExtension(fn);
    }

    public static Genome Load(string name)
    {
      return new Genome { Genes = File.ReadAllText(Path.Combine(GenomesDir, name + ".txt")).Split('\t').Select(s => double.Parse(s)).ToList() };
    }
  }

  public static class Optimizer
  {
    public static bool ShowTrace = true;

    public static IEnumerable<StrategyOptimizerReport> OptimizeNeuralStrategy(IEnumerable<OptimizerParameter> oParams,
      Func<IEnumerable<StrategyParameter>, Strategy> makeStrat)
    {
      return Optimizer.OptimizeStrategyParameters(oParams, sParams => {

        var strat = makeStrat(sParams);
        var bestGenome = OptimizeNeuralGenome(strat);
        var genomeName = bestGenome.Save();
        return new StrategyOptimizerReport {
          StrategyName = strat.Name,
          StrategyParams = strat.Parameters,
          GenomeName = genomeName,
          Fitness = bestGenome.Fitness ?? 0
        };
      }).OrderByDescending(x => x.Fitness);
    }

    public static Genome OptimizeNeuralGenome(Strategy strat)
    {
      return Optimizer.Anneal(Optimizer.MakeRandomGenome(strat.GenomeSize), g => strat.CalculateError(g)).Params;
    }

    public static void OptimizeSignal(string signalName, IEnumerable<OptimizerParameter> oParams, DataSeries<Bar> bars,
      Func<IEnumerable<StrategyParameter>, DataSeries<Value>> makeSignal)
    {
      var reports = Optimizer.OptimizeStrategyParameters(oParams, sParams => {
        var sig = makeSignal(sParams);
        return new StrategyOptimizerReport {
          Fitness = bars.SignalAccuracyPercent(sig),
          StrategyParams = sParams,
          StrategyName = signalName
        };
      }).OrderByDescending(x => x.Fitness);

      Strategy.PrintStrategyOptimizerReports(reports);
    }

    class ValidationWindow
    {
      public DateTime PadFirst;
      public DateTime First;
      public DateTime Last;
    }

    public static StrategyOptimizerReport OptimizeDecisionTree(string name, IEnumerable<OptimizerParameter> oParams,
      int numAnnealingIterations, DateTime startDate, DataSeries<Bar> trainingBars, TimeSpan frontPadding,
      Func<IEnumerable<StrategyParameter>, double> getMinMajority,
      Func<IEnumerable<StrategyParameter>, DataSeries<Bar>, IEnumerable<DtExample>> makeExamples,
      bool silent = false)
    {
      var annealResult = Optimizer.Anneal(oParams, sParams => {
        var trainingSet = makeExamples(sParams, trainingBars.From(startDate.Subtract(frontPadding))).Where(x => x.Timestamp >= startDate).ToList();
        var dt = DecisionTree.Learn(trainingSet, Prediction.Green, getMinMajority(sParams));
        return -Quality(dt, trainingSet);
      }, numAnnealingIterations);

      return MakeReport(name, trainingBars, getMinMajority, makeExamples, silent, annealResult);
    }

    //public static StrategyOptimizerReport OptimizeDecisionTree(string name, IEnumerable<OptimizerParameter> oParams,
    //  int numAnnealingIterations, DateTime startDate, DataSeries<Bar> bars,
    //  TimeSpan validationWindowSize, TimeSpan frontPadding,
    //  Func<IEnumerable<StrategyParameter>, double> getMinMajority,
    //  Func<IEnumerable<StrategyParameter>, DataSeries<Bar>, IEnumerable<DtExample>> makeExamples,
    //  bool silent = false)
    //{
    //  Func<DateTime, DateTime, DateTime> maxDate = (a, b) => a > b ? a : b;

    //  List<ValidationWindow> validationWindows = new List<ValidationWindow>();
    //  for (DateTime windowStart = startDate;
    //    windowStart.Add(validationWindowSize) <= bars.Last().Timestamp;
    //    windowStart = windowStart.Add(validationWindowSize))
    //    validationWindows.Add(new ValidationWindow {
    //      First = windowStart,
    //      PadFirst = maxDate(bars.First().Timestamp, windowStart.Subtract(frontPadding)),
    //      Last = windowStart.Add(validationWindowSize).AddDays(-1)
    //    });
    //  var lastFirst = bars.Last().Timestamp.Subtract(validationWindowSize).AddDays(1);
    //  validationWindows.Add(new ValidationWindow {
    //    First = lastFirst,
    //    PadFirst = maxDate(bars.First().Timestamp, lastFirst.Subtract(frontPadding)),
    //    Last = bars.Last().Timestamp
    //  });

    //  var padDate = maxDate(bars.First().Timestamp, startDate.Subtract(frontPadding));

    //  var annealResult = Optimizer.Anneal(oParams, sParams => {
    //    double costSum = 0.0;
    //    Parallel.ForEach(validationWindows, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, vw => {
    //      //foreach (var vw in validationWindows)
    //      //{
    //      var teachingSet1 =
    //        makeExamples(sParams, bars.From(padDate).To(vw.First.AddDays(-1)))
    //          .Where(x => x.Timestamp >= startDate);

    //      var teachingSet2 =
    //        makeExamples(sParams, bars.From(maxDate(bars.First().Timestamp, vw.Last.AddDays(1).Subtract(frontPadding))))
    //                 .Where(x => x.Timestamp >= vw.Last.AddDays(1));

    //      var validationSet =
    //        makeExamples(sParams, bars.From(vw.PadFirst).To(vw.Last)).Where(x => x.Timestamp >= vw.First).ToList();

    //      var teachingSet = teachingSet1.Concat(teachingSet2);
    //      var dt = DecisionTree.Learn(teachingSet, Prediction.Green, getMinMajority(sParams));

    //      var quality = ValidationSetQuality(dt, validationSet);
    //      lock (validationWindows)
    //      {
    //        costSum += -quality;
    //      }
    //    });

    //    return costSum / validationWindows.Count;
    //  }, numAnnealingIterations);

    //  return MakeReport(name, bars, getMinMajority, makeExamples, silent, annealResult);
    //}

    static StrategyOptimizerReport MakeReport(string name, DataSeries<Bar> bars,
      Func<IEnumerable<StrategyParameter>, double> getMinMajority,
      Func<IEnumerable<StrategyParameter>, DataSeries<Bar>, IEnumerable<DtExample>> makeExamples,
      bool silent, AnnealResult<IEnumerable<StrategyParameter>> annealResult)
    {
      var report = new StrategyOptimizerReport {
        StrategyName = name,
        Fitness = -annealResult.Cost,
        StrategyParams = annealResult.Params.ToList()
      };
      if (!silent)
      {
        Strategy.PrintStrategyOptimizerReports(List.Create(report));
        DecisionTree.WriteDot("dt.dot",
          DecisionTree.Learn(makeExamples(annealResult.Params, bars),
          Prediction.Green, getMinMajority(annealResult.Params)));
        var p = Process.Start(@"C:\Program Files (x86)\Graphviz 2.28\bin\dot.exe", "-Tpng -o dt.png dt.dot");
        p.EnableRaisingEvents = true;
        p.WaitForExit();
        Process.Start("dt.png");
      }
      return report;
    }

    static double Quality(object dt, List<DtExample> set)
    {
      var numCorrect = 0;
      var numIncorrect = 0;
      var numUnsure = 0;
      foreach (var e in set)
      {
        var decision = DecisionTree.Decide(e.AttributesValues, dt);
        if (decision.Equals(e.Goal))
          numCorrect++;
        else if (decision is string && (string)decision == "Unsure")
          numUnsure++;
        else
          numIncorrect++;
      }

      var accuracy = (double)numCorrect / (numCorrect + numIncorrect);
      var confidence = (double)(numCorrect + numIncorrect) / set.Count;
      return accuracy * confidence;
    }

    static Genome MutateGenome(Genome genome, double temperature)
    {
      return new Genome {
        Genes = genome.Genes.Select(g => Clip(GeneMin, GeneMax, g + RandomDouble(-temperature, temperature))).ToList()
      };
    }

    static List<StrategyParameter> MutateSParams(IEnumerable<StrategyParameter> sParams, IEnumerable<OptimizerParameter> oParams, double temperature)
    {
      return sParams.Select(p => {
        var op = oParams.First(x => x.Name == p.Name);
        if (op.Low == op.High)
          return new StrategyParameter(op.Name, op.Low);
        else
        {
          var halfRange = (op.High - op.Low) / 2;
          return new StrategyParameter(p.Name, Quantize(Clip(op.Low, op.High, p.Value + temperature * RandomDouble(-halfRange, halfRange)), op.Low, op.Granularity));
        }
      }).ToList();
    }

    public static double Quantize(double v, double min, double step)
    {
      return Math.Round((v - min) / step) * step + min;
    }

    public static Func<double, double> MakeSMA(int period)
    {
      Queue<double> q = new Queue<double>();
      double avg = 0;
      return n => {
        if (q.Count < period)
        {
          avg = (avg * q.Count + n) / (q.Count + 1);
          q.Enqueue(n);
        }
        else
        {
          avg = (avg * q.Count - q.Peek() + n) / q.Count;
          q.Dequeue();
          q.Enqueue(n);
        }
        return avg;
      };
    }

    public static Func<double, double> MakeStdDev(int period)
    {
      Queue<double> q = new Queue<double>();
      return n => {
        if (q.Count < period)
          q.Enqueue(n);
        else
        {
          q.Dequeue();
          q.Enqueue(n);
        }
        return StdDev(q);
      };
    }

    static double StdDev(IEnumerable<double> xs)
    {
      var avg = xs.Average();
      return Math.Sqrt(xs.Select(x => Math.Pow(x - avg, 2)).Sum() / xs.Count());
    }

    public static AnnealResult<Genome> Anneal(Genome seed, Func<Genome, double> costFunc)
    {
      return Anneal(seed, MutateGenome, costFunc, 25000);
    }

    public static AnnealResult<IEnumerable<StrategyParameter>> Anneal(IEnumerable<OptimizerParameter> oParams, Func<IEnumerable<StrategyParameter>, double> costFunc, int iterations = 25000)
    {
      return Anneal(MakeSParamsSeed(oParams).ToList(), (sp, temp) => MutateSParams(sp, oParams, temp), costFunc, iterations);
    }

    static AnnealResult<TParams> Anneal<TParams>(TParams initialParams, Func<TParams, double, TParams> mutate, Func<TParams, double> costFunc, int iterations = 25000)
    {
      Func<double, double> schedule = t => Math.Sqrt(Math.Exp(-11 * t));
      var escapeCostPremiumSma = MakeSMA(25);
      var costSma = MakeSMA(2000);
      var costStdDev = MakeStdDev(2000);

      var acceptCount = 0;
      var rejectCount = 0;
      double takeAnywayProbability = 0;
      TParams currentParams = initialParams;
      var currentCost = costFunc(currentParams);
      var bestParams = currentParams;
      var bestCost = currentCost;
      double averageEscapeCostPremium = 0;
      for (int i = 0; i < iterations; i++)
      {
        var temperature = schedule((double)i / iterations);
        var nextParams = mutate(currentParams, temperature * GeneMagnitude);
        var nextCost = costFunc(nextParams);

        var divisor = Math.Max(1, iterations / 1000);
        if (i % divisor == 0)
        {
          if (ShowTrace)
            Trace.WriteLine(string.Format("{0} / {1}  Cost = {2:N8}  ECP = {3}  T = {4:N4}  P = {5:N4}",
              i + divisor, iterations, currentCost, averageEscapeCostPremium, temperature, takeAnywayProbability));
        }

        Action takeNext = () => {
          currentParams = nextParams;
          currentCost = nextCost;
          if (currentCost < bestCost)
          {
            bestParams = currentParams;
            bestCost = currentCost;
          }
          acceptCount++;
        };

        if (nextCost < currentCost)
          takeNext();
        else
        {
          var escapeCostPremium = (nextCost - currentCost);
          averageEscapeCostPremium = escapeCostPremiumSma(escapeCostPremium);
          takeAnywayProbability = temperature * Math.Exp(-(escapeCostPremium / averageEscapeCostPremium) + 1);
          bool takeItAnyway = WithProb(takeAnywayProbability);
          if (takeItAnyway)
            takeNext();
          else
            rejectCount++;
        }
      }

      if (ShowTrace)
        Trace.WriteLine("Best cost: " + bestCost);
      return new AnnealResult<TParams> {
        Params = bestParams,
        Cost = bestCost
      };
    }

    private static IEnumerable<StrategyParameter> MakeSParamsSeed(IEnumerable<OptimizerParameter> oParams)
    {
      return oParams.Select(op => new StrategyParameter(op.Name, Quantize((op.Low + op.High) / 2.0, op.Low, op.Granularity)));
    }

    static double Clip(double min, double max, double x)
    {
      return Math.Max(min, Math.Min(max, x));
    }

    static Random Random = new Random();

    static double GeneMin { get { return -GeneMagnitude; } }
    static double GeneMax { get { return GeneMagnitude; } }
    const double GeneMagnitude = 1;

    static double RandomDouble(double min, double max)
    {
      return Random.NextDouble() * (max - min) + min;
    }

    static bool WithProb(double probability)
    {
      return Random.NextDouble() < probability;
    }

    public static Genome MakeRandomGenome(int length)
    {
      return new Genome { Genes = List.Repeat(length, () => RandomDouble(GeneMin, GeneMax)) };
    }

    public static bool ParallelizeStrategyOptimization;

    public static List<StrategyOptimizerReport> OptimizeStrategyParameters(IEnumerable<OptimizerParameter> oParams, OptimizeKernelFunc optimizeKernel)
    {
      var sParamsList = CrossProd(oParams.Select(op => Range(op.Low, op.High, op.Granularity).ToArray()).ToList())
        .Select(sParamValues => oParams.Select(sp => sp.Name).Zip(sParamValues, (n, v) => new StrategyParameter(n, v)).ToList()).ToList();

      //if (sParamsList.Count > 1)
      //{
      //  Trace.WriteLine("Parallellizing strategies.");
      //  ParallelStrategies = true;
      //}
      //else
      //{
      //  Trace.WriteLine("Parallellizing fitness calculation.");
      //  ParallelStrategies = false;
      //}

      List<StrategyOptimizerReport> results;

      if (ParallelizeStrategyOptimization)
      {
        List<StrategyOptimizerReport> reports = new List<StrategyOptimizerReport>();
        var sw = new Stopwatch();
        sw.Start();
        var eta = Optimizer.MakeSMA(25);
        Parallel.ForEach(sParamsList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, sParams => {
          var rpt = optimizeKernel(sParams.ToList());
          lock (reports)
          {
            reports.Add(rpt);
            Trace.WriteLine(string.Format("Completed {0} of {1}    ETA: {2} min",
              reports.Count, sParamsList.Count, eta(sw.Elapsed.TotalMinutes / reports.Count * sParamsList.Count).ToString("N1")));
          }
        });
        results = reports;
      }
      else
      {
        var count = 0;
        results = sParamsList.Select(sParams => {
          count++;
          Trace.WriteLine(string.Format("Optimizing sParams {0} of {1}", count, sParamsList.Count));
          var result = optimizeKernel(sParams.ToList());
          return result;
        }).ToList();
      }

      return results.OrderByDescending(x => x.Fitness).ToList();
    }

    static IEnumerable<double> Range(double low, double high, double step)
    {
      var n = low;
      yield return n;
      while ((n += step) <= high)
        yield return n;
    }

    public static List<double[]> CrossProd(List<double[]> rest)
    {
      return CrossProd(new List<double[]>() { new double[0] }, rest.ToList());
    }

    static List<double[]> CrossProd(List<double[]> acc, List<double[]> rest)
    {
      if (!rest.Any())
        return acc;

      var newAcc = new List<double[]>();
      var next = rest.First();
      rest.Remove(next);

      foreach (var x in acc)
        foreach (var n in next)
        {
          var newx = new double[x.Length + 1];
          Array.Copy(x, 0, newx, 0, x.Length);
          newx[newx.Length - 1] = n;
          newAcc.Add(newx);
        }

      return CrossProd(newAcc, rest);
    }
  }

  public class AnnealResult<TParams>
  {
    public TParams Params;
    public double Cost;
  }
}
