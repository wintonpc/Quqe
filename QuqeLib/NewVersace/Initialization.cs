using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCW;

namespace Quqe
{
  public class Initialization
  {
    public static ProtoChromosome MakeProtoChromosome()
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
        ProtoGene.Create("RnnTrainingEpochs", 20, 1000, GeneType.Discrete),
        ProtoGene.Create("RnnLayer1NodeCount", 3, 40, GeneType.Discrete),
        ProtoGene.Create("RnnLayer2NodeCount", 3, 20, GeneType.Discrete),

        // RBF params
        ProtoGene.Create("RbfNetTolerance", 0, 1, GeneType.Continuous),
        ProtoGene.Create("RbfGaussianSpread", 0.1, 10, GeneType.Continuous),
      });
    }

    public static Generation MakeInitialGeneration(Run run, RunSetupInfo setup, IGenTrainer trainer)
    {
      var gen = new Generation(run, -1);

      Func<int, NetworkType, Chromosome[]> makeChromosomes = (n, type) =>
        List.Repeat(n, _ => RandomChromosome(type, setup.ProtoChromosome)).ToArray();

      Func<Chromosome[]> makeMixtureChromosomes = () =>
        makeChromosomes(setup.RnnPerMixture, NetworkType.Rnn).Concat(
        makeChromosomes(setup.RbfPerMixture, NetworkType.Rbf)).ToArray();

      Func<Mixture> makeMixture = () => new Mixture(gen, new Mixture[0]);

      Func<Mixture, MixtureInfo> makeMixtureInfo = m => new MixtureInfo(m.Id, makeMixtureChromosomes());

      var pop = List.Repeat(setup.MixturesPerGeneration, _ => makeMixture()).Select(makeMixtureInfo);

      trainer.Train(gen, pop, _ => { });

      return gen;
    }

    public static Chromosome RandomChromosome(NetworkType networkType, ProtoChromosome protoChrom)
    {
      return new Chromosome(networkType, protoChrom.Genes.Select(gd => new Gene(gd.Name, Functions.RandomGeneValue(gd))));
    }
  }
}
