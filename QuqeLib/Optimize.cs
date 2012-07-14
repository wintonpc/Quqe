using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using System.IO;
using System.Text.RegularExpressions;

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

  public class OptimizerReport
  {
    public List<StrategyParameter> StrategyParams;
    public string GenomeName;
  }

  public delegate OptimizerReport OptimizeKernelFunc(List<StrategyParameter> sParams);

  public class EvolutionParams
  {
    public int GenerationSurvivorCount;
    public int RandomAliensPerGeneration;
    public double MutationRate;
    public double MaxMutation;
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
      if (!Directory.Exists(GenomesDir))
        Directory.CreateDirectory(GenomesDir);

      int lastGenomeNumber = Directory.EnumerateFiles(GenomesDir).Select(f => int.Parse(Regex.Replace(f, @"([^\.])+\.txt$", m => m.Groups[1].Value))).Max();
      var number = lastGenomeNumber.ToString("D6");
      var name = number;
      var fn = Path.Combine(GenomesDir, name + ".txt");

      File.WriteAllText(fn, Genes.Join("\t"));
      return name;
    }

    public static Genome Load(string name)
    {
      return new Genome { Genes = File.ReadAllText(Path.Combine(GenomesDir, name + ".txt")).Split('\t').Select(s => double.Parse(s)).ToList() };
    }
  }

  public static class Optimizer
  {
    public static Genome Evolve(EvolutionParams eParams, Genome seed, Func<Genome, double> fitnessFunc)
    {
      var population = new List<Genome>();
      population.Add(seed);
      population.AddRange(List.Repeat(eParams.GenerationSurvivorCount - 1, () => MakeRandomGenome(seed.Size)));

      foreach (var g in population)
        g.Fitness = fitnessFunc(g);

      for (int gen = 0; gen < eParams.NumGenerations; gen++)
      {
        var minFitnessSquared = Math.Pow(population.Min(g => g.Fitness).Value, 2);
        var maxFitnessSquared = Math.Pow(population.Max(g => g.Fitness).Value, 2);
        Func<double, int> numOffspring = fitness => (int)(fitness * fitness / (maxFitnessSquared - minFitnessSquared) * eParams.MaxOffspring);

        // add children
        foreach (var g in population.ToList())
          population.AddRange(List.Repeat(numOffspring(g.Fitness.Value), () => Breed(g, population.RandomItem(), eParams)));

        // add random aliens
        population.AddRange(List.Repeat(eParams.RandomAliensPerGeneration, () => MakeRandomGenome(seed.Size)));

        // update fitness for those that don't have it
        foreach (var g in population.Where(g => g.Fitness == null).ToList())
          g.Fitness = fitnessFunc(g);

        // kill off the unfit
        population = population.OrderByDescending(g => g.Fitness).Take(eParams.GenerationSurvivorCount).ToList();
      }
      return population.First();
    }

    static Genome Breed(Genome a, Genome b, EvolutionParams eParams)
    {
      var c = new Genome();
      for (int i = 0; i < a.Size; i++)
      {
        var p = Random.Next(2);
        var gene = p == 0 ? a.Genes[i] : b.Genes[i];
        if (WithProb(eParams.MutationRate))
          gene += RandomDouble(-eParams.MaxMutation, eParams.MaxMutation);
        c.Genes[i] = Clip(GeneMin, GeneMax, gene);
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

    public static List<OptimizerReport> OptimizeStrategyParameters(IEnumerable<OptimizerParameter> oParams, OptimizeKernelFunc optimizeKernel)
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
