using MongoDB.Bson;
using PCW;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quqe
{
  public class MixtureInfo
  {
    public readonly ObjectId MixtureId;
    public readonly Chromosome[] Chromosomes;

    public MixtureInfo(ObjectId mixtureId, IEnumerable<Chromosome> chromosomes)
    {
      MixtureId = mixtureId;
      Chromosomes = chromosomes.ToArray();
    }
  }

  public class RunSetupInfo
  {
    public readonly ProtoChromosome ProtoChromosome;
    public readonly int MixturesPerGeneration;
    public readonly int RnnPerMixture;
    public readonly int RbfPerMixture;
    public readonly int SelectionSize;

    public RunSetupInfo(ProtoChromosome protoChrom, int mixturesPerGen, int rnnPerMixture, int rbfPerMixture, int selectionSize)
    {
      ProtoChromosome = protoChrom;
      MixturesPerGeneration = mixturesPerGen;
      RnnPerMixture = rnnPerMixture;
      RbfPerMixture = rbfPerMixture;
      SelectionSize = selectionSize;
    }
  }

  public static class Functions
  {
    public static Run Evolve(Database db, IGenTrainer trainer, int numGenerations, RunSetupInfo v)
    {
      var run = new Run(db, v.ProtoChromosome);
      var initialGen = Initialization.MakeInitialGeneration(run, v, trainer);

      List.Iterate(numGenerations, initialGen, (i, gen) => 
        Train(trainer, run, i, newGen => Mutate(Combine(v.MixturesPerGeneration, newGen, Select(v.SelectionSize, Evaluate(gen.Mixtures))))));
      return run;
    }

    static Generation Train(IGenTrainer trainer, Run run, int generationNum, Func<Generation, MixtureInfo[]> getPopulation)
    {
      var gen = new Generation(run, generationNum);
      trainer.Train(gen, getPopulation(gen),
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

    static MixtureInfo[] Combine(int outputSize, Generation gen, IEnumerable<MixtureEval> ms)
    {
      return List.Repeat((int)Math.Ceiling(outputSize / 2.0), _ => CombineTwo(ms, gen)).SelectMany(x => x).Take(outputSize).ToArray();
    }

    static MixtureInfo[] CombineTwo(IEnumerable<MixtureEval> ms, Generation gen)
    {
      var parents = SelectTwoAccordingToQuality(ms, x => x.Fitness);

      Func<MixtureEval, Chromosome[]> chromosomesFor = me => me.Mixture.Experts.Select(x => x.Chromosome).ToArray();

      Func<IEnumerable<Chromosome>, MixtureInfo> chromosomesToMixture = chromosomes => {
        var mixture = new Mixture(gen, List.Create(parents[0].Mixture, parents[1].Mixture));
        return new MixtureInfo(mixture.Id, chromosomes);
      };

      var zipped = chromosomesFor(parents[0]).Zip(chromosomesFor(parents[1]), (a, b) => CrossOver(gen.Run, a, b));
      var unzipped = Unzip(zipped, z => z[0], z => z[1]);
      return unzipped.Select(chromosomesToMixture).ToArray();
    }

    public static Chromosome[] CrossOver(Run run, Chromosome a, Chromosome b)
    {
      Debug.Assert(a.NetworkType == b.NetworkType);
      var zipped = a.Genes.Zip(b.Genes, (x, y) => {
        Debug.Assert(x.Name == y.Name);
        return QuqeUtil.Random.Next(2) == 0 ? List.Create(x, y) : List.Create(y, x);
      });
      var unzipped = Unzip(zipped, z => z[0], z => z[1]);
      return unzipped.Select(genes => new Chromosome(a.NetworkType, genes)).ToArray();
    }

    public static IEnumerable<IEnumerable<TUnzipped>> Unzip<T, TUnzipped>(IEnumerable<T> items, Func<T, TUnzipped> a, Func<T, TUnzipped> b)
    {
      return List.Create(items.Select(a), items.Select(b));
    }

    public static T[] SelectTwoAccordingToQuality<T>(IEnumerable<T> items, Func<T, double> quality)
    {
      var possibleFirsts = items.ToList();
      var first = SelectOneAccordingToQuality(possibleFirsts, quality);
      var possibleSeconds = items.Except(List.Create(first)).ToList();
      var second = SelectOneAccordingToQuality(possibleSeconds, quality);
      return new[] { first, second };
    }

    public static T SelectOneAccordingToQuality<T>(IEnumerable<T> items, Func<T, double> quality)
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
      return 0;
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
}
