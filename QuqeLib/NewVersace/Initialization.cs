using System;
using System.Linq;

namespace Quqe
{
  public class Initialization
  {
    public static ProtoChromosome MakeProtoChromosome()
    {
      return new ProtoChromosome(new[] {
        // input data window
        ProtoGene.Create("TrainingOffsetPct", 0, 1, GeneType.Continuous),
        ProtoGene.Create("TrainingSizePct", 0.03, 1, GeneType.Continuous),
        
        // transformations
        ProtoGene.Create("DatabaseType", 0, 1, GeneType.Discrete),
        ProtoGene.CreateBoolean("UseComplementCoding"),
        ProtoGene.CreateBoolean("UsePCA"),
        ProtoGene.Create("PrincipalComponent", 0, 100, GeneType.Discrete),

        // RNN params
        ProtoGene.Create("RnnTrainingEpochs", 20, 1000, GeneType.Discrete),
        ProtoGene.Create("RnnLayer1NodeCount", 3, 200, GeneType.Discrete),
        ProtoGene.Create("RnnLayer2NodeCount", 3, 200, GeneType.Discrete),

        // RBF params
        ProtoGene.Create("RbfNetTolerance", 0, 1, GeneType.Continuous),
        ProtoGene.Create("RbfGaussianSpread", 0.1, 10, GeneType.Continuous),
      });
    }

    public static ProtoChromosome MakeFastProtoChromosome()
    {
      return new ProtoChromosome(new[] {
        // input data window
        ProtoGene.Create("TrainingOffsetPct", 0, 1, GeneType.Continuous),
        ProtoGene.Create("TrainingSizePct", 0, 1, GeneType.Continuous),
        
        // transformations
        ProtoGene.Create("DatabaseType", 0, 1, GeneType.Discrete),
        ProtoGene.CreateBoolean("UseComplementCoding"),
        ProtoGene.CreateBoolean("UsePCA"),
        ProtoGene.Create("PrincipalComponent", 0, 100, GeneType.Discrete),

        // RNN params
        ProtoGene.Create("RnnTrainingEpochs", 20, 100, GeneType.Discrete),
        ProtoGene.Create("RnnLayer1NodeCount", 3, 40, GeneType.Discrete),
        ProtoGene.Create("RnnLayer2NodeCount", 3, 10, GeneType.Discrete),

        // RBF params
        ProtoGene.Create("RbfNetTolerance", 0, 1, GeneType.Continuous),
        ProtoGene.Create("RbfGaussianSpread", 0.1, 10, GeneType.Continuous),
      });
    }

    public static ProtoChromosome MakeFastestProtoChromosome()
    {
      return new ProtoChromosome(new[] {
        // input data window
        ProtoGene.Create("TrainingOffsetPct", 0, 1, GeneType.Continuous),
        ProtoGene.Create("TrainingSizePct", 0, 1, GeneType.Continuous),
        
        // transformations
        ProtoGene.Create("DatabaseType", 0, 1, GeneType.Discrete),
        ProtoGene.CreateBoolean("UseComplementCoding"),
        ProtoGene.CreateBoolean("UsePCA"),
        ProtoGene.Create("PrincipalComponent", 0, 100, GeneType.Discrete),

        // RNN params
        ProtoGene.Create("RnnTrainingEpochs", 3, 3, GeneType.Discrete),
        ProtoGene.Create("RnnLayer1NodeCount", 3, 40, GeneType.Discrete),
        ProtoGene.Create("RnnLayer2NodeCount", 3, 10, GeneType.Discrete),

        // RBF params
        ProtoGene.Create("RbfNetTolerance", 0.99, 1, GeneType.Continuous),
        ProtoGene.Create("RbfGaussianSpread", 0.1, 10, GeneType.Continuous),
      });
    }

    public static Generation MakeInitialGeneration(DataSet seed, Run run, IGenTrainer trainer)
    {
      var gen = new Generation(run, 0);

      Func<int, NetworkType, int, Chromosome[]> makeChromosomes = (n, type, orderOffset) =>
        Lists.Repeat(n, i => MakeRandomChromosome(type, run.ProtoRun.ProtoChromosome, orderOffset + i)).ToArray();

      Func<Chromosome[]> makeMixtureChromosomes = () =>
        makeChromosomes(run.ProtoRun.RnnPerMixture, NetworkType.Rnn, 0).Concat(
        makeChromosomes(run.ProtoRun.RbfPerMixture, NetworkType.Rbf, run.ProtoRun.RnnPerMixture)).ToArray();

      Func<Mixture> makeMixture = () => new Mixture(gen, new Mixture[0]);

      Func<Mixture, MixtureInfo> makeMixtureInfo = m => new MixtureInfo(m.Id, makeMixtureChromosomes());

      var pop = Lists.Repeat(run.ProtoRun.MixturesPerGeneration, _ => makeMixture()).Select(makeMixtureInfo);

      trainer.Train(seed, gen, pop, progress => Console.WriteLine("Initialized {0} of {1}", progress.Completed, progress.Total));

      return gen;
    }

    public static Chromosome MakeRandomChromosome(NetworkType networkType, ProtoChromosome protoChrom, int order)
    {
      return new Chromosome(networkType, protoChrom.Genes.Select(gd => new Gene(gd.Name, Functions.RandomGeneValue(gd))), order);
    }
  }
}
