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
  public static partial class Functions
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

    static MixtureEval[] Evaluate(Mixture[] mixtures)
    {
      return mixtures.Select(EvaluateMixture).ToArray();
    }

    static MixtureEval EvaluateMixture(Mixture m)
    {
      var fitness = MixtureFitness(m);
      return new MixtureEval(m, fitness);
    }

    static double MixtureFitness(Mixture m)
    {
      return QuqeUtil.Random.NextDouble();
    }

    static MixtureEval[] Select(int selectionSize, IEnumerable<MixtureEval> ms)
    {
      return ms.OrderByDescending(x => x.Fitness).Take(selectionSize).ToArray();
    }

    internal static MixtureInfo[] Combine(int outputSize, IList<MixtureEval> ms)
    {
      return List.Repeat((int)Math.Ceiling(outputSize / 2.0), _ => CombineTwoMixtures(ms)).SelectMany(x => x).Take(outputSize).ToArray();
    }

    internal static Tuple2<MixtureInfo> CombineTwoMixtures(IList<MixtureEval> ms)
    {
      var parents = SelectTwoAccordingToQuality(ms, x => x.Fitness);

      Func<MixtureEval, Chromosome[]> chromosomesOf = me => me.Mixture.Experts.Select(x => x.Chromosome).ToArray();

      Func<IEnumerable<Chromosome>, MixtureInfo> chromosomesToMixture = chromosomes => 
        new MixtureInfo(parents.Select(p => p.Mixture), chromosomes);

      return CrossOver(chromosomesOf(parents.Item1), chromosomesOf(parents.Item2), CrossOverChromosomes, chromosomesToMixture);
    }

    internal static Tuple2<Chromosome> CrossOverChromosomes(Chromosome a, Chromosome b)
    {
      Debug.Assert(a.NetworkType == b.NetworkType);

      Func<Gene, Gene, Tuple2<Gene>> crossGenes = (x, y) => {
        Debug.Assert(x.Name == y.Name);
        return QuqeUtil.Random.Next(2) == 0 ? Tuple2.Create(x, y) : Tuple2.Create(y, x);
      };

      return CrossOver(a.Genes, b.Genes, crossGenes, genes => new Chromosome(a.NetworkType, genes));
    }

    static MixtureInfo[] Mutate(IEnumerable<MixtureInfo> ms)
    {
      throw new NotImplementedException();
    }

    public static double RandomGeneValue(ProtoGene gd)
    {
      return Quantize(RandomDouble(gd.MinValue, gd.MaxValue), gd.MinValue, gd.Granularity);
    }
  }
}
