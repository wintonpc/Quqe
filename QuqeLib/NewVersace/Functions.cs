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

  public static class Functions
  {
    static MixtureInfo MakeMixture(Generation gen, ProtoChromosome chromDesc, int rnnPerMixture, int rbfPerMixture)
    {
      var rnnChromosomes = List.Repeat(rnnPerMixture, _ => RandomChromosome(NetworkType.Rnn, chromDesc));
      var rbfChromosomes = List.Repeat(rbfPerMixture, _ => RandomChromosome(NetworkType.Rbf, chromDesc));
      var mixture = new Mixture(gen, new Mixture[0]);
      return new MixtureInfo(mixture.Id, rnnChromosomes.Concat(rbfChromosomes).ToArray());
    }

    public static Run Evolve(Database db, IGenTrainer trainer, int mixturesPerGeneration, int numGenerations, int rnnPerMixture, int rbfPerMixture, ProtoChromosome chromDesc)
    {
      var run = new Run(db, chromDesc);

      var gen0 = new Generation(run, 0);
      var pop0 = List.Repeat(mixturesPerGeneration, _ => MakeMixture(gen0, chromDesc, rnnPerMixture, rbfPerMixture)).ToArray();

      List.Iterate(numGenerations, pop0, (i, pop) => {
        var genNum = i + 1;
        var gen = new Generation(run, genNum);
        var mixtures = trainer.Train(gen, pop,
          progress => Trace.WriteLine(string.Format("Generation {0}: Trained {1} of {2}", genNum, progress.Completed, progress.Total)));
        return Mutate(CrossOver(Select(Evaluate(mixtures))));
      });
      return run;
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

    static MixtureInfo[] CrossOver(IEnumerable<MixtureEval> ms)
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

    static Chromosome RandomChromosome(NetworkType networkType, ProtoChromosome chromDesc)
    {
      return new Chromosome(networkType, chromDesc.ProtoGenes.Select(gd => new Gene(gd.Name, RandomGeneValue(gd))));
    }

    static double RandomGeneValue(ProtoGene gd)
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
