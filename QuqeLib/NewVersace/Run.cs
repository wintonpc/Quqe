using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public enum GeneType { Boolean, Discrete, Continuous }
  public enum NetworkType { Rnn, Rbf }

  public class ProtoGene
  {
    public string Name { get; private set; }
    public double MinValue { get; private set; }
    public double MaxValue { get; private set; }
    public double Granularity { get; private set; }
    public bool IsBoolean { get; private set; }

    ProtoGene(string name, double minValue, double maxValue, double granularity, bool isBoolean)
    {
      Name = name;
      MinValue = minValue;
      MaxValue = maxValue;
      Granularity = granularity;
      IsBoolean = isBoolean;
    }

    public static ProtoGene Create(string name, double minValue, double maxValue, double granularity, bool isBoolean)
    {
      return new ProtoGene(name, minValue, maxValue, granularity, isBoolean);
    }

    public static ProtoGene CreateBoolean(string name)
    {
      return new ProtoGene(name, 0, 1, 1, true);
    }

    public static ProtoGene Create(string name, double minValue, double maxValue, GeneType type)
    {
      switch (type)
      {
        case GeneType.Discrete: return new ProtoGene(name, minValue, maxValue, 1, false);
        case GeneType.Continuous: return new ProtoGene(name, minValue, maxValue, 0.00001, false);
        default: throw new Exception("Unexpected type: " + type);
      }
    }
  }

  public class ProtoChromosome
  {
    public ProtoGene[] Genes { get; private set; }
    public ProtoChromosome(IEnumerable<ProtoGene> protoGenes) { Genes = protoGenes.ToArray(); }
  }

  public class Gene
  {
    public string Name { get; private set; }
    public double Value { get; private set; }

    public Gene(string name, double value)
    {
      Name = name;
      Value = value;
    }

    public ProtoGene GetProto(Run run)
    {
      return run.ProtoChromosome.Genes.First(x => x.Name == this.Name);
    }
  }

  public enum DatabaseType { A, B }

  public class Chromosome
  {
    [BsonRepresentation(BsonType.String)]
    public NetworkType NetworkType { get; private set; }
    public Gene[] Genes { get; private set; }
    public int OrderInMixture { get; private set; }

    public Chromosome(NetworkType networkType, IEnumerable<Gene> genes, int order)
    {
      NetworkType = networkType;
      Genes = genes.ToArray();
      OrderInMixture = order;
    }

    // input data window
    public double TrainingOffsetPct { get { return GetGeneValue<double>("TrainingOffsetPct"); } }
    public double TrainingSizePct { get { return GetGeneValue<double>("TrainingSizePct"); } }

    // transformations
    public DatabaseType DatabaseType { get { return GetGeneValue<DatabaseType>("DatabaseType"); } }
    public bool UseComplementCoding { get { return GetGeneValue<bool>("UseComplementCoding"); } }
    public bool UsePCA { get { return GetGeneValue<bool>("UsePCA"); } }
    public int PrincipalComponent { get { return GetGeneValue<int>("PrincipalComponent"); } }

    // RNN params
    public int RnnTrainingEpochs { get { return GetGeneValue<int>("RnnTrainingEpochs"); } }
    public int RnnLayer1NodeCount { get { return GetGeneValue<int>("RnnLayer1NodeCount"); } }
    public int RnnLayer2NodeCount { get { return GetGeneValue<int>("RnnLayer2NodeCount"); } }

    // RBF params
    public double RbfNetTolerance { get { return GetGeneValue<double>("RbfNetTolerance"); } }
    public double RbfGaussianSpread { get { return GetGeneValue<double>("RbfGaussianSpread"); } }

    T GetGeneValue<T>(string name) where T : struct
    {
      return Genes.First(g => g.Name == name).Value.As<T>();
    }

    //static T DoubleToValue(

    public override string ToString()
    {
      return NetworkType + "-" + OrderInMixture + "-" + Genes.Join("-", g => g.Name + ":" + g.Value);
    }
  }

  public class ProtoRun : MongoTopLevelObject
  {
    public string Name { get; private set; }
    public int NumGenerations { get; private set; }
    public ProtoChromosome ProtoChromosome { get; private set; }
    public int MixturesPerGeneration { get; private set; }
    public int RnnPerMixture { get; private set; }
    public int RbfPerMixture { get; private set; }
    public int SelectionSize { get; private set; }
    public double MutationRate { get; private set; }

    public ProtoRun(Database db, string name, int numGenerations, ProtoChromosome protoChrom,
      int mixturesPerGen, int rnnPerMixture, int rbfPerMixture, int selectionSize, double mutationRate)
      : base(db)
    {
      Name = name;
      NumGenerations = numGenerations;
      ProtoChromosome = protoChrom; MixturesPerGeneration = mixturesPerGen;
      RnnPerMixture = rnnPerMixture;
      RbfPerMixture = rbfPerMixture;
      SelectionSize = selectionSize;
      MutationRate = mutationRate;

      db.Store(this);
    }
  }

  public class Run : MongoTopLevelObject
  {
    public ProtoChromosome ProtoChromosome { get; private set; }
    public ProtoRun ProtoRun { get; private set; }
    public Generation[] Generations { get { return Database.QueryAll<Generation>(x => x.RunId == this.Id, "Order"); } }

    public Run(ProtoRun protoRun, ProtoChromosome protoChrom)
      : base(protoRun.Database)
    {
      ProtoChromosome = protoChrom;
      ProtoRun = protoRun;
      Database.Store(this);
    }
  }

  public class Generation : MongoTopLevelObject
  {
    public ObjectId RunId { get; private set; }
    public Run Run { get { return Database.Get<Run>(RunId); } }

    public int Order { get; private set; }
    public Mixture[] Mixtures { get { return Database.QueryAll<Mixture>(x => x.GenerationId == this.Id); } }
    public GenEval Evaluated { get { return Database.QueryOne<GenEval>(x => x.GenerationId == this.Id); } }

    public Generation(Run run, int order)
      : base(run.Database)
    {
      RunId = run.Id;
      Order = order;
      Database.Store(this);
    }
  }

  public class Mixture : MongoTopLevelObject
  {
    public ObjectId GenerationId { get; private set; }
    public Generation Generation { get { return Database.Get<Generation>(GenerationId); } }

    public Chromosome[] Chromosomes { get { return Experts.Select(x => x.Chromosome).ToArray(); } }

    public ObjectId[] Parents;
    public Expert[] Experts
    {
      get
      {
        var rnnExperts = Database.QueryAll<RnnTrainRec>(x => x.MixtureId == this.Id);
        var rbfExperts = Database.QueryAll<RbfTrainRec>(x => x.MixtureId == this.Id);
        return rnnExperts.Concat<Expert>(rbfExperts).OrderBy(x => x.Chromosome.OrderInMixture).ToArray();
      }
    }
    public MixtureEval Evaluated { get { return Database.QueryOne<MixtureEval>(x => x.MixtureId == this.Id); } }

    public Mixture(Generation gen, IEnumerable<Mixture> parents)
      : base(gen.Database)
    {
      GenerationId = gen.Id;
      Parents = parents.Select(x => x.Id).ToArray();
      Database.Store(this);
    }
  }

  public class GenEval : MongoTopLevelObject
  {
    public ObjectId GenerationId { get; private set; }
    public Generation Generation { get { return Database.Get<Generation>(GenerationId); } }
    public double Fitness { get; private set; }

    public GenEval(Generation gen, double fitness)
      : base(gen.Database)
    {
      GenerationId = gen.Id;
      Fitness = fitness;

      Database.Store(this);
    }
  }

  public class MixtureEval : MongoTopLevelObject
  {
    public ObjectId MixtureId { get; private set; }
    public Mixture Mixture { get { return Database.Get<Mixture>(MixtureId); } }
    public double Fitness { get; private set; }

    public MixtureEval(Mixture mixture, double fitness)
      : base(mixture.Database)
    {
      MixtureId = mixture.Id;
      Fitness = fitness;
      Database.Store(this);
    }
  }

  public abstract class Expert : MongoTopLevelObject
  {
    public ObjectId MixtureId { get; private set; }
    public Mixture Mixture { get { return Database.Get<Mixture>(MixtureId); } }
    public Chromosome Chromosome { get; private set; }

    protected Expert(Database db, ObjectId mixtureId, Chromosome chromosome)
      : base(db)
    {
      MixtureId = mixtureId;
      Chromosome = chromosome;
    }
  }
}
