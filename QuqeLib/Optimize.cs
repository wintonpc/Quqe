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
    public List<StrategyParameter> StrategyParams;
    public string GenomeName;
    public double GenomeFitness;

    static readonly string StrategyDir = "Strategies";
    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.AppendLine("Genome : " + GenomeName);
      sb.AppendLine("Fitness: " + GenomeFitness);
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
        r.GenomeName = nextLine()[1];
        r.GenomeFitness = double.Parse(nextLine()[1]);

        r.StrategyParams = new List<StrategyParameter>();
        string[] lin;
        while ((lin = nextLine()) != null)
          r.StrategyParams.Add(new StrategyParameter(lin[0], double.Parse(lin[1])));
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

  public static class Optimizer
  {
    public static IEnumerable<StrategyOptimizerReport> OptimizeNeuralIndicator(IEnumerable<OptimizerParameter> oParams, EvolutionParams eParams,
      Func<IEnumerable<StrategyParameter>, List<DataSeries<Value>>> cookInputs,
      Func<Genome, IEnumerable<DataSeries<Value>>, DataSeries<Value>> makeSignal,
      DataSeries<Bar> bars, DataSeries<Value> idealSignal)
    {
      return Optimizer.OptimizeStrategyParameters(oParams, sParams => {
        var inputs = cookInputs(sParams);

        var bestGenome = Optimizer.Evolve(eParams, Optimizer.MakeRandomGenome(WardNet.GenomeSize(inputs.Count)),
          g => -1 * makeSignal(g, inputs).Variance(idealSignal));

        var genomeName = bestGenome.Save();
        return new StrategyOptimizerReport {
          StrategyParams = sParams,
          GenomeName = genomeName,
          GenomeFitness = bestGenome.Fitness.Value
        };
      }).OrderByDescending(x => x.GenomeFitness);
    }

    public static Genome Evolve(EvolutionParams eParams, Genome seed, Func<Genome, double> fitnessFunc)
    {
      var population = new List<Genome>();
      population.Add(seed);
      population.AddRange(List.Repeat(eParams.GenerationSurvivorCount - 1, () => MakeRandomGenome(seed.Size)));

      Action updateFitness = () => {
        var unmeasured = population.Where(g => g.Fitness == null).ToList();
        //Trace.WriteLine("Calculating fitness for " + unmeasured.Count + " individuals");
        Parallel.ForEach(unmeasured, g => { g.Fitness = fitnessFunc(g); });
        //foreach (var g in unmeasured)
        //  g.Fitness = fitnessFunc(g);
      };

      updateFitness();

      double maxMutation = 0;

      for (int gen = 0; gen < eParams.NumGenerations; gen++)
      {
        var maxFitness = population.Max(g => g.Fitness).Value;
        var averageFitness = population.Average(g => g.Fitness).Value;
        Func<double, int> numOffspring = fitness => (int)Math.Max(0, (fitness - averageFitness) / (maxFitness - averageFitness) * eParams.MaxOffspring);

        int numAliens = eParams.RandomAliensPerGeneration;
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
        maxMutation = eParams.MaxMutationTimesVariance / variance;
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
      for (int i = 0; i < a.Size; i++)
      {
        var p = Random.Next(2);
        var gene = p == 0 ? a.Genes[i] : b.Genes[i];
        if (WithProb(eParams.MutationRate))
          gene += RandomDouble(-maxMutation, maxMutation);
        c.Genes.Add(Clip(GeneMin, GeneMax, gene));
      }
      return c;
    }

    static double Clip(double min, double max, double x)
    {
      return Math.Max(min, Math.Min(max, x));
    }

    static Random Random = new Random();

    const double GeneMin = -1;
    const double GeneMax = 1;

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

    public static List<StrategyOptimizerReport> OptimizeStrategyParameters(IEnumerable<OptimizerParameter> oParams, OptimizeKernelFunc optimizeKernel)
    {
      return CrossProd(oParams.Select(op => Range(op.Low, op.High, op.Granularity).ToArray()).ToList())
        .Select(sParamValues => oParams.Select(sp => sp.Name).Zip(sParamValues, (n, v) => new StrategyParameter(n, v)))
        .Select(sParams => optimizeKernel(sParams.ToList()))
        .ToList();
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
