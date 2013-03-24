using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
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

  public class Chromosome
  {
    [BsonRepresentation(BsonType.String)]
    public NetworkType NetworkType { get; private set; }
    public Gene[] Genes { get; private set; }

    public Chromosome(NetworkType networkType, IEnumerable<Gene> genes)
    {
      NetworkType = networkType;
      Genes = genes.ToArray();
    }
  }

  public class Run : MongoTopLevelObject
  {
    public ObjectId Id { get; private set; }
    public ProtoChromosome ProtoChromosome { get; private set; }
    public Generation[] Generations { get { return Database.QueryAll<Generation>(x => x.RunId == this.Id); } }

    public Run(Database db, ProtoChromosome protoChrom)
      : base(db)
    {
      ProtoChromosome = protoChrom;
      db.Set(this, x => x.Id);
    }
  }

  public class Generation : MongoTopLevelObject
  {
    public ObjectId Id { get; private set; }
    public ObjectId RunId { get; private set; }
    public Run Run { get { return Database.Get<Run>(RunId); } }

    public int Order { get; private set; }
    public Mixture[] Mixtures { get { return Database.QueryAll<Mixture>(x => x.GenerationId == this.Id); } }
    public GenEval Evaluated { get { return Database.QueryAll<GenEval>(x => x.GenerationId == this.Id).Single(); } }

    public Generation(Run run, int order)
      : base(run.Database)
    {
      RunId = run.Id;
      Order = order;
      Database.Set(this, x => x.Id);
    }
  }

  public class Mixture : MongoTopLevelObject
  {
    public ObjectId Id { get; private set; }
    public ObjectId GenerationId { get; private set; }
    public Generation Generation { get { return Database.Get<Generation>(GenerationId); } }

    public ObjectId[] Parents;
    public Expert[] Experts
    {
      get
      {
        var rnnExperts = Database.QueryAll<RnnTrainRec>(x => x.MixtureId == this.Id);
        var rbfExperts = Database.QueryAll<RbfTrainRec>(x => x.MixtureId == this.Id);
        return rnnExperts.Concat<Expert>(rbfExperts).ToArray();
      }
    }
    public MixtureEval Evaluated { get { return Database.QueryAll<MixtureEval>(x => x.MixtureId == this.Id).Single(); } }

    public Mixture(Generation gen, IEnumerable<Mixture> parents)
      : base(gen.Database)
    {
      GenerationId = gen.Id;
      Parents = parents.Select(x => x.Id).ToArray();
      Database.Set(this, x => x.Id);
    }
  }

  public class GenEval : MongoTopLevelObject
  {
    public ObjectId Id { get; private set; }
    public ObjectId GenerationId { get; private set; }
    public Generation Generation { get { return Database.Get<Generation>(GenerationId); } }
    public double Fitness { get; private set; }

    public GenEval(Generation gen, double fitness)
      : base(gen.Database)
    {
      Fitness = fitness;
      Database.Set(this, x => x.Id);
    }
  }

  public class MixtureEval : MongoTopLevelObject
  {
    public ObjectId Id { get; private set; }
    public ObjectId MixtureId { get; private set; }
    public Mixture Mixture { get { return Database.Get<Mixture>(MixtureId); } }
    public double Fitness { get; private set; }

    public MixtureEval(Mixture mixture, double fitness)
      : base(mixture.Database)
    {
      MixtureId = mixture.Id;
      Fitness = fitness;
      Database.Set(this, x => x.Id);
    }
  }

  public abstract class Expert : MongoTopLevelObject
  {
    public ObjectId Id { get; private set; }
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
