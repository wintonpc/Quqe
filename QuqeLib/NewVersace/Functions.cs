using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PCW;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quqe
{
  public static class Functions
  {
    public static Run Evolve(Database db, IGenTrainer trainer, int numGenerations, RunSetupInfo v)
    {
      var run = new Run(db, v.ProtoChromosome);
      var initialGen = Initialization.MakeInitialGeneration(run, v, trainer);

      List.Iterate(numGenerations, initialGen, (i, gen) =>
        Train(trainer, run, i, Mutate(Combine(v.MixturesPerGeneration, Select(v.SelectionSize, Evaluate(gen.Mixtures))))));
      return run;
    }

    static Generation Train(IGenTrainer trainer, Run run, int generationNum, MixtureInfo[] pop)
    {
      var gen = new Generation(run, generationNum);
      trainer.Train(gen, pop.Select(mi => new MixtureInfo(new Mixture(gen, mi.Parents).Id, mi.Chromosomes)),
        progress => Trace.WriteLine(string.Format("Generation {0}: Trained {1} of {2}",
          generationNum, progress.Completed, progress.Total)));
      return gen;
    }

    static MixtureEval[] Evaluate(IEnumerable<Mixture> mixtures)
    {
      return mixtures.Select(EvaluateMixture).ToArray();
    }

    static MixtureEval EvaluateMixture(Mixture m)
    {
      var fitness = MixtureFitness(m);
      return new MixtureEval(m, fitness);
    }

    static MixtureEval[] Select(int selectionSize, IEnumerable<MixtureEval> ms)
    {
      return ms.OrderByDescending(x => x.Fitness).Take(selectionSize).ToArray();
    }

    static MixtureInfo[] Combine(int outputSize, IList<MixtureEval> ms)
    {
      return List.Repeat((int)Math.Ceiling(outputSize / 2.0), _ => CombineTwoMixtures(ms)).SelectMany(x => x).Take(outputSize).ToArray();
    }

    static Tuple2<MixtureInfo> CombineTwoMixtures(IList<MixtureEval> ms)
    {
      var parents = SelectTwoAccordingToQuality(ms, x => x.Fitness);

      Func<MixtureEval, Chromosome[]> chromosomesOf = me => me.Mixture.Experts.Select(x => x.Chromosome).ToArray();

      Func<IEnumerable<Chromosome>, MixtureInfo> chromosomesToMixture = chromosomes => 
        new MixtureInfo(parents.Select(p => p.Mixture), chromosomes);

      return CrossOver(chromosomesOf(parents.Item1), chromosomesOf(parents.Item2), CrossOverChromosomes, chromosomesToMixture);
    }

    public static Tuple2<Chromosome> CrossOverChromosomes(Chromosome a, Chromosome b)
    {
      Debug.Assert(a.NetworkType == b.NetworkType);

      Func<Gene, Gene, Tuple2<Gene>> crossGenes = (x, y) => {
        Debug.Assert(x.Name == y.Name);
        return QuqeUtil.Random.Next(2) == 0 ? Tuple2.Create(x, y) : Tuple2.Create(y, x);
      };

      return CrossOver(a.Genes, b.Genes, crossGenes, genes => new Chromosome(a.NetworkType, genes));
    }

    public static Tuple2<TResult> CrossOver<T, TResult>(T[] a, T[] b, Func<T, T, Tuple2<T>> crossItems, Func<T[], TResult> makeResult)
    {
      var zipped = a.Zip(b, crossItems).ToArray();
      var newA = zipped.Select(x => x.Item1).ToArray();
      var newB = zipped.Select(x => x.Item2).ToArray();
      return new Tuple2<TResult>(makeResult(newA), makeResult(newB));
    }

    public static Tuple2<T> SelectTwoAccordingToQuality<T>(IList<T> items, Func<T, double> quality)
    {
      var possibleFirsts = items;
      var first = SelectOneAccordingToQuality(possibleFirsts, quality);
      var possibleSeconds = items.Except(List.Create(first)).ToList();
      var second = SelectOneAccordingToQuality(possibleSeconds, quality);
      return Tuple2.Create(first, second);
    }

    public static T SelectOneAccordingToQuality<T>(IList<T> items, Func<T, double> quality)
    {
      var qualitySum = items.Sum(quality);
      var spot = QuqeUtil.Random.NextDouble() * qualitySum;

      var a = 0.0;
      foreach (var item in items)
      {
        var b = a + quality(item);
        if (a <= spot && spot < b)
          return item;
        a = b;
      }
      throw new Exception("Your algorithm didn't work");
    }

    static MixtureInfo[] Mutate(IEnumerable<MixtureInfo> ms)
    {
      throw new NotImplementedException();
    }

    static double MixtureFitness(Mixture m)
    {
      return QuqeUtil.Random.NextDouble();
    }

    public static double RandomGeneValue(ProtoGene gd)
    {
      return Quantize(RandomDouble(gd.MinValue, gd.MaxValue), gd.MinValue, gd.Granularity);
    }

    public static double Quantize(double v, double min, double step)
    {
      return Math.Round((v - min) / step) * step + min;
    }

    public static double RandomDouble(double min, double max)
    {
      return QuqeUtil.Random.NextDouble() * (max - min) + min;
    }
  }

  public class Tuple2<T> : IEnumerable<T>
  {
    public readonly T Item1;
    public readonly T Item2;

    public Tuple2(T item1, T item2)
    {
      Item1 = item1;
      Item2 = item2;
    }

    public IEnumerator<T> GetEnumerator()
    {
      return List.Create(Item1, Item2).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }

  public static class Tuple2
  {
    public static Tuple2<T> Create<T>(T item1, T item2) { return new Tuple2<T>(item1, item2); }
  }
}
