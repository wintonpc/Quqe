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

    public RunSetupInfo(ProtoChromosome protoChrom, int mixturesPerGen, int rnnPerMixture, int rbfPerMixture)
    {
      ProtoChromosome = protoChrom;
      MixturesPerGeneration = mixturesPerGen;
      RnnPerMixture = rnnPerMixture;
      RbfPerMixture = rbfPerMixture;
    }
  }

  public static class Functions
  {
    public static Run Evolve(Database db, IGenTrainer trainer, int numGenerations, RunSetupInfo runSetup)
    {
      var run = new Run(db, runSetup.ProtoChromosome);
      var initialGen = Initialization.MakeInitialGeneration(run, runSetup, trainer);

      List.Iterate(numGenerations, initialGen, (i, gen) => 
        Train(trainer, run, i, Mutate(Combine(Select(Evaluate(gen.Mixtures))))));
      return run;
    }

    static Generation Train(IGenTrainer trainer, Run run, int generationNum, MixtureInfo[] pop)
    {
      var gen = new Generation(run, generationNum);
      trainer.Train(gen, pop,
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

    static MixtureEval[] Select(IEnumerable<MixtureEval> ms)
    {
      throw new NotImplementedException();
    }

    static MixtureInfo[] Combine(IEnumerable<MixtureEval> ms)
    {
      throw new NotImplementedException();
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
