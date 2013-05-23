using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra.Double;
using PCW;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

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
      return value.As<T>();
    }
  }

  public class StrategyOptimizerReport
  {
    public string StrategyName;
    public List<StrategyParameter> StrategyParams;
    public string GenomeName;
    public double Fitness;

    public static readonly string StrategyDir = @"d:\Users\Wintonpc\git\Quqe\Share\Strategies";
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

    static double Clip(double min, double max, double x)
    {
      return Math.Max(min, Math.Min(max, x));
    }

    static double GeneMin { get { return -GeneMagnitude; } }
    static double GeneMax { get { return GeneMagnitude; } }
    const double GeneMagnitude = 1;

    public static double RandomDouble(double min, double max)
    {
      return QuqeUtil.Random.NextDouble() * (max - min) + min;
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

  public class TrainResult<TParams, TInit>
  {
    public TParams Params;
    public TInit TrainingInit;
    public double Cost;
    public List<double> CostHistory;
  }

  public class RnnTrainResult
  {
    public RNNSpec RNNSpec;
    public double Cost;
    public List<double> CostHistory;
    public List<Vec> WeightHistory;
  }
}
