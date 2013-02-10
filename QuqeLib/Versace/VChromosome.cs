using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCW;

namespace Quqe
{
  public class VChromosome
  {
    static readonly Random Random = new Random();

    public readonly List<VGene> Genes;

    public static VChromosome CreateRandom()
    {
      return new VChromosome(name => Versace.Settings.ProtoChromosome.First(x => x.Name == name).CloneAndRandomize());
    }

    VChromosome(List<VGene> genes)
    {
      Genes = genes;
    }

    VChromosome(Func<string, VGene> makeGene)
    {
      Genes = new List<VGene> {
        makeGene("ElmanTrainingEpochs"),
        makeGene("DatabaseType"),
        makeGene("TrainingOffsetPct"),
        makeGene("TrainingSizePct"),
        makeGene("UseComplementCoding"),
        makeGene("UsePrincipalComponentAnalysis"),
        makeGene("PrincipalComponent"),
        makeGene("RbfNetTolerance"),
        makeGene("RbfGaussianSpread"),
        makeGene("ElmanHidden1NodeCount"),
        makeGene("ElmanHidden2NodeCount")
      };
    }

    public int ElmanTrainingEpochs { get { return GetGeneValue<int>("ElmanTrainingEpochs"); } }
    public DatabaseType DatabaseType { get { return GetGeneValue<int>("DatabaseType") == 0 ? DatabaseType.A : DatabaseType.B; } }
    public double TrainingOffsetPct { get { return GetGeneValue<double>("TrainingOffsetPct"); } }
    public double TrainingSizePct { get { return GetGeneValue<double>("TrainingSizePct"); } }
    public bool UseComplementCoding { get { return GetGeneValue<int>("UseComplementCoding") == 1; } }
    public bool UsePrincipalComponentAnalysis { get { return GetGeneValue<int>("UsePrincipalComponentAnalysis") == 1; } }
    public int PrincipalComponent { get { return GetGeneValue<int>("PrincipalComponent"); } }
    public double RbfNetTolerance { get { return GetGeneValue<double>("RbfNetTolerance"); } }
    public double RbfGaussianSpread { get { return GetGeneValue<double>("RbfGaussianSpread"); } }
    public int ElmanHidden1NodeCount { get { return GetGeneValue<int>("ElmanHidden1NodeCount"); } }
    public int ElmanHidden2NodeCount { get { return GetGeneValue<int>("ElmanHidden2NodeCount"); } }

    TValue GetGeneValue<TValue>(string name) where TValue : struct
    {
      return ((VGene<TValue>)Genes.First(g => g.Name == name)).Value;
    }

    public List<VChromosome> Crossover(VChromosome other)
    {
      var children = Crossover(Genes, other.Genes);
      return List.Create(
        new VChromosome(children[0]),
        new VChromosome(children[1]));
    }

    static List<List<VGene>> Crossover(IEnumerable<VGene> x, IEnumerable<VGene> y)
    {
      var a = x.ToList();
      var b = y.ToList();
      for (int i = 0; i < a.Count; i++)
        if (Optimizer.WithProb(0.5))
        {
          var t = a[i];
          a[i] = b[i];
          b[i] = t;
        }
      return List.Create(a, b);
    }

    public VChromosome Mutate()
    {
      return new VChromosome(Mutate(Genes));
    }

    static List<VGene> Mutate(IEnumerable<VGene> genes)
    {
      return genes.Select(g => Random.NextDouble() < Versace.Settings.MutationRate ? g.Mutate(Versace.Settings.MutationDamping) : g).ToList();
    }
  }
}
