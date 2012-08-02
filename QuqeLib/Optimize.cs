using System;
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
      return (T)Convert.ChangeType(sParams.First(sp => sp.Name == name).Value, typeof(T));
    }
  }

  public class StrategyOptimizerReport
  {
    public string StrategyName;
    public List<StrategyParameter> StrategyParams;
    public string GenomeName;
    public double GenomeFitness;

    static readonly string StrategyDir = "Strategies";
    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.AppendLine("Strategy : " + StrategyName);
      sb.AppendLine("Genome   : " + GenomeName);
      sb.AppendLine("Fitness  : " + GenomeFitness);
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
        r.GenomeFitness = double.Parse(nextLine()[1]);

        var sParams = new List<StrategyParameter>();
        string[] lin;
        while ((lin = nextLine()) != null)
          sParams.Add(new StrategyParameter(lin[0], double.Parse(lin[1])));
        r.StrategyParams = sParams;
        return r;
      }
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

  public class EvolutionParams
  {
    public int GenerationSurvivorCount;
    public int RandomAliensPerGeneration;
    public double MutationRate;
    //public double MaxMutation;
    public double MaxMutationTimesVariance;
    public int MaxOffspring;
    public int NumGenerations;
  }

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

  public enum OptimizationType { Genetic, Anneal }

  public static class Optimizer
  {
    //public static IEnumerable<StrategyOptimizerReport> OptimizeNeuralIndicator(IEnumerable<OptimizerParameter> oParams, EvolutionParams eParams,
    //  Func<IEnumerable<StrategyParameter>, List<DataSeries<Value>>> cookInputs,
    //  Func<IEnumerable<StrategyParameter>, Genome, IEnumerable<DataSeries<Value>>, DataSeries<Value>> makeSignal,
    //  Func<IEnumerable<StrategyParameter>, DataSeries<Value>> makeIdealSignal,
    //  OptimizationType oType, DataSeries<Bar> bars)
    //{
    //  return Optimizer.OptimizeStrategyParameters(oParams, sParams => {
    //    var inputs = cookInputs(sParams);

    //    var idealSignal = makeIdealSignal(sParams);

    //    Genome bestGenome;
    //    if (oType == OptimizationType.Genetic)
    //    {
    //      bestGenome = Optimizer.Evolve(eParams, Optimizer.MakeRandomGenome(WardNet.GenomeSize(inputs.Count)),
    //        g => -1 * makeSignal(sParams, g, inputs).Variance(idealSignal));
    //    }
    //    else
    //    {
    //      bestGenome = Optimizer.Anneal(Optimizer.MakeRandomGenome(WardNet.GenomeSize(inputs.Count)), g => {
    //        var signal = makeSignal(sParams, g, inputs);
    //        return signal.Variance(idealSignal) * Math.Pow(signal.Sign().Variance(idealSignal), 1);
    //      });
    //    }

    //    var genomeName = bestGenome.Save();
    //    return new StrategyOptimizerReport {
    //      StrategyParams = sParams,
    //      GenomeName = genomeName,
    //      GenomeFitness = bestGenome.Fitness ?? 0
    //    };
    //  }).OrderByDescending(x => x.GenomeFitness);
    //}

    public static bool ShowTrace = true;

    public static IEnumerable<StrategyOptimizerReport> OptimizeNeuralIndicator(IEnumerable<OptimizerParameter> oParams,
      OptimizationType oType, EvolutionParams eParams, Func<IEnumerable<StrategyParameter>, Strategy> makeStrat)
    {
      return Optimizer.OptimizeStrategyParameters(oParams, sParams => {

        var strat = makeStrat(sParams);

        Genome bestGenome;
        if (oType == OptimizationType.Genetic)
        {
          bestGenome = Optimizer.Evolve(eParams, Optimizer.MakeRandomGenome(strat.GenomeSize),
            g => -1 * strat.MakeSignal(g).Variance(strat.IdealSignal));
        }
        else
        {
          bestGenome = Optimizer.Anneal(Optimizer.MakeRandomGenome(strat.GenomeSize), g =>
          strat.CalculateError(g));
        }

        var genomeName = bestGenome.Save();
        return new StrategyOptimizerReport {
          StrategyName = strat.Name,
          StrategyParams = strat.Parameters,
          GenomeName = genomeName,
          GenomeFitness = bestGenome.Fitness ?? 0
        };
      }).OrderByDescending(x => x.GenomeFitness);
    }

    public static Genome OptimizeNeuralGenome(Strategy strat, OptimizationType oType, EvolutionParams eParams = null)
    {
      if (oType == OptimizationType.Genetic)
      {
        return Optimizer.Evolve(eParams, Optimizer.MakeRandomGenome(strat.GenomeSize),
          g => -1 * strat.MakeSignal(g).Variance(strat.IdealSignal));
      }
      else
      {
        return Optimizer.Anneal(Optimizer.MakeRandomGenome(strat.GenomeSize), g =>
        strat.CalculateError(g));
      }
    }

    public static void OptimizeSignalAccuracy(string signalName, IEnumerable<OptimizerParameter> oParams, DataSeries<Bar> bars,
      Func<IEnumerable<StrategyParameter>, DataSeries<Value>> makeSignal)
    {
      var reports = Optimizer.OptimizeStrategyParameters(oParams, sParams => {
        var sig = makeSignal(sParams);
        return new StrategyOptimizerReport {
          GenomeFitness = bars.SignalAccuracyPercent(sig),
          StrategyParams = sParams,
          StrategyName = signalName
        };
      }).OrderByDescending(x => x.GenomeFitness);

      Strategy.PrintStrategyOptimizerReports(reports);
    }

    public static Genome Evolve(EvolutionParams eParams, Genome seed, Func<Genome, double> fitnessFunc)
    {
      var population = new List<Genome>();
      population.Add(seed);
      population.AddRange(List.Repeat(eParams.GenerationSurvivorCount - 1, () => MakeRandomGenome(seed.Size)));

      Action updateFitness = () => {
        var unmeasured = population.Where(g => g.Fitness == null).ToList();
        //Trace.WriteLine("Calculating fitness for " + unmeasured.Count + " individuals");
        if (ParallelStrategies)
        {
          foreach (var g in unmeasured)
            g.Fitness = fitnessFunc(g);
        }
        else
          Parallel.ForEach(unmeasured, g => { g.Fitness = fitnessFunc(g); });
      };

      updateFitness();

      double maxMutation = 0;

      for (int gen = 0; gen < eParams.NumGenerations; gen++)
      {
        var maxFitness = population.Max(g => g.Fitness).Value;
        var averageFitness = population.Average(g => g.Fitness).Value;
        //Func<double, int> numOffspring = fitness => (int)Math.Max(0, (fitness - averageFitness) / (maxFitness - averageFitness) * eParams.MaxOffspring);
        int maxOffspring = (gen < eParams.NumGenerations) ? eParams.MaxOffspring : eParams.MaxOffspring * 4;
        int numAliens = (gen < eParams.NumGenerations) ? eParams.RandomAliensPerGeneration : eParams.RandomAliensPerGeneration / 4;

        var z = gen % 100;
        if (40 <= z && z < 45)
        {
          Trace.WriteLine("Radiation burst!");
          maxMutation = 0.4;  // radiation burst
          Trace.WriteLine("Alien invasion!");
          numAliens = 200;  // alien invasion
        }

        // asexual reproduction
        foreach (var g in population.ToList())
          population.AddRange(List.Repeat(eParams.MaxOffspring /*numOffspring(g.Fitness.Value)*/, () => Breed(g, g, eParams, maxMutation)));
        updateFitness();

        //if (90 <= z && z < 95)
        //{
        //  Trace.WriteLine("Alien invasion!");
        //  numAliens = 200;  // alien invasion
        //}

        // add random aliens
        population.AddRange(List.Repeat(numAliens, () => MakeRandomGenome(seed.Size)));
        updateFitness();

        //// sexual reproduction
        //foreach (var g in population.ToList())
        //  population.AddRange(List.Repeat(numOffspring(g.Fitness.Value), () => Breed(g, population.RandomItem(), eParams)));
        //updateFitness();

        // kill off the unfit
        population = population.OrderByDescending(g => g.Fitness).Take(eParams.GenerationSurvivorCount).ToList();
        var variance = Variance(population.Select(g => g.Fitness.Value));
        maxMutation = Math.Max(0.005, Math.Min(1, eParams.MaxMutationTimesVariance / variance));
        Trace.WriteLine("Gen " + gen + ", fittest: " + population.First().Fitness.Value.ToString("N6") + "   Variance: " + variance.ToString("N6") + "   MaxMutation: " + maxMutation.ToString("N6"));
      }
      return population.First();
    }

    static double Variance(IEnumerable<double> xs)
    {
      var u = xs.Average();
      return xs.Average(x => Math.Pow(x - u, 2));
    }

    static Genome Breed(Genome a, Genome b, EvolutionParams eParams, double maxMutation)
    {
      var c = new Genome { Genes = new List<double>() };
      var m = RandomDouble(0, maxMutation);
      for (int i = 0; i < a.Size; i++)
      {
        var p = Random.Next(2);
        var gene = p == 0 ? a.Genes[i] : b.Genes[i];
        if (WithProb(eParams.MutationRate))
          gene += RandomDouble(-m, m);
        c.Genes.Add(Clip(GeneMin, GeneMax, gene));
      }
      return c;
    }

    static Genome MutateGenome(Genome genome, double temperature)
    {
      return new Genome {
        Genes = genome.Genes.Select(g => Clip(GeneMin, GeneMax, g + RandomDouble(-temperature, temperature))).ToList()
      };
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

    public static Genome Anneal(Genome seed, Func<Genome, double> costFunc)
    {
      int iterations = 25000;
      Func<double, double> schedule = t => Math.Sqrt(Math.Exp(-11 * t));
      var escapeCostPremiumSma = MakeSMA(25);
      var costSma = MakeSMA(2000);
      var costStdDev = MakeStdDev(2000);

      var acceptCount = 0;
      var rejectCount = 0;
      double takeAnywayProbability = 0;
      var currentGenome = seed;
      var currentCost = costFunc(currentGenome);
      var bestGenome = currentGenome;
      var bestCost = currentCost;
      double averageEscapeCostPremium = 0;
      double bestCostStdDev;
      double bestCostAvg;
      double bestCostThresh = 0.00001;
      for (int i = 0; i < iterations /*|| averageEscapeCostPremium / bestCost > 0.0000005*/; i++)
      {
        var temperature = schedule((double)i / iterations);
        var nextGenome = MutateGenome(currentGenome, temperature * GeneMagnitude);
        var nextCost = costFunc(nextGenome);

        if (i % 50 == 0)
        {
          //Console.WriteLine(string.Format("{0} / {1}  Temp = {2:N2}  Accept rate = {3}  ( {4} / {5} )  Cost = {6:N4}",
          //  i, iterations, temperature, rejectCount == 0 ? "always" : ((double)acceptCount / (double)rejectCount).ToString("N3"),
          //  acceptCount, rejectCount, currentCost));
          if (ShowTrace)
            Console.WriteLine(string.Format("{0} / {1}  Cost = {2:N8}  ECP = {3}  T = {4:N4}  P = {5:N4}",
              i, iterations, currentCost, averageEscapeCostPremium, temperature, takeAnywayProbability));
          //acceptCount = 0;
          //rejectCount = 0;
        }

        Action takeNext = () => {
          currentGenome = nextGenome;
          currentCost = nextCost;
          if (currentCost < bestCost)
          {
            bestGenome = currentGenome;
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

        bestCostStdDev = costStdDev(currentCost);
        bestCostAvg = costSma(currentCost);
        //if (i > 2000 && bestCostStdDev / bestCostAvg < bestCostThresh)
        //  break;
      }

      return bestGenome;
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

    //public static List<StrategyOptimizerReport> OptimizeStrategyParameters(IEnumerable<OptimizerParameter> oParams, OptimizeKernelFunc optimizeKernel)
    //{
    //  return CrossProd(oParams.Select(op => Range(op.Low, op.High, op.Granularity).ToArray()).ToList())
    //    .Select(sParamValues => oParams.Select(sp => sp.Name).Zip(sParamValues, (n, v) => new StrategyParameter(n, v)))
    //    .Select(sParams => optimizeKernel(sParams.ToList()))
    //    .ToList();
    //}

    static bool ParallelStrategies;

    public static List<StrategyOptimizerReport> OptimizeStrategyParameters(IEnumerable<OptimizerParameter> oParams, OptimizeKernelFunc optimizeKernel)
    {
      var sParamsList = CrossProd(oParams.Select(op => Range(op.Low, op.High, op.Granularity).ToArray()).ToList())
        .Select(sParamValues => oParams.Select(sp => sp.Name).Zip(sParamValues, (n, v) => new StrategyParameter(n, v))).ToList();

      if (sParamsList.Count > 1)
      {
        Trace.WriteLine("Parallellizing strategies.");
        ParallelStrategies = true;
      }
      else
      {
        Trace.WriteLine("Parallellizing fitness calculation.");
        ParallelStrategies = false;
      }

      List<StrategyOptimizerReport> results;

      if (ParallelStrategies)
      {
        List<StrategyOptimizerReport> reports = new List<StrategyOptimizerReport>();
        Parallel.ForEach(sParamsList, sParams => {
          var rpt = optimizeKernel(sParams.ToList());
          lock (reports) { reports.Add(rpt); }
        });
        results = reports;
      }
      else
        results = sParamsList.Select(sParams => optimizeKernel(sParams.ToList())).ToList();

      return results.OrderByDescending(x => x.GenomeFitness).ToList();
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
}
