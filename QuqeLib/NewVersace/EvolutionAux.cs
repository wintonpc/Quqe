using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quqe
{
  public class MixtureInfo
  {
    public readonly ObjectId MixtureId;
    public readonly Chromosome[] Chromosomes;
    [BsonIgnore]
    public readonly Mixture[] Parents;

    public MixtureInfo(ObjectId mixtureId, IEnumerable<Chromosome> chromosomes)
    {
      MixtureId = mixtureId;
      Chromosomes = chromosomes.ToArray();
    }

    public MixtureInfo(IEnumerable<Mixture> parents, IEnumerable<Chromosome> chromosomes)
    {
      Parents = parents.ToArray();
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
}
