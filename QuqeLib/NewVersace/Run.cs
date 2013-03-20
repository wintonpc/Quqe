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
  public abstract class MongoObject
  {
    static MongoObject()
    {
      BsonClassMap.RegisterConventions(new ConventionProfile().SetMemberFinderConvention(new MyMemberFinderConvention()), _ => true);
    }

    internal Database Database;

    protected MongoObject(Database db)
    {
      Database = db;
    }
  }

  public class MyMemberFinderConvention : IMemberFinderConvention
  {
    public IEnumerable<MemberInfo> FindMembers(Type type)
    {
      return type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy).Where(x => !x.Name.EndsWith("Ref"))
        .Concat<MemberInfo>(type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(x => x.Name.EndsWith("Id")))
        .Concat<MemberInfo>(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy));
    }
  }

  public enum GeneType { Boolean, Discrete, Continuous }

  public class GeneDesc : MongoObject
  {
    [BsonId]
    public ObjectId Id { get; private set; }

    public readonly string Name;
    public readonly double MinValue;
    public readonly double MaxValue;
    public readonly double Granularity;
    public readonly bool IsBoolean;

    GeneDesc(Database db, string name, double minValue, double maxValue, double granularity, bool isBoolean) : base(db)
    {
      Name = name;
      MinValue = minValue;
      MaxValue = maxValue;
      Granularity = granularity;
      IsBoolean = isBoolean;
    }

    public static GeneDesc Create(Database db, string name, double minValue, double maxValue, double granularity, bool isBoolean)
    {
      return new GeneDesc(db, name, minValue, maxValue, granularity, isBoolean);
    }

    public static GeneDesc CreateBoolean(Database db, string name)
    {
      return new GeneDesc(db, name, 0, 1, 1, true);
    }

    public static GeneDesc Create(Database db, string name, double minValue, double maxValue, GeneType type)
    {
      switch (type)
      {
        case GeneType.Discrete: return new GeneDesc(db, name, minValue, maxValue, 1, false);
        case GeneType.Continuous: return new GeneDesc(db, name, minValue, maxValue, 0.00001, false);
        default: throw new Exception("Unexpected type: " + type);
      }
    }
  }

  public class Gene : MongoObject
  {
    public ObjectId GeneDescId { get; private set; }
    public GeneDesc GeneDesc
    {
      get { return Database.Get<GeneDesc>(GeneDescId); }
      set { Database.Set(value, x => x.Id); GeneDescId = value.Id; }
    }

    public readonly double Value;

    public Gene(GeneDesc desc, double value) : base(desc.Database)
    {
      GeneDesc = desc;
      Value = value;
    }
  }

  public class Chromosome : MongoObject
  {
    public readonly Gene[] Genes;

    //public Chromosome
  }

  public abstract class TrainRec : MongoObject
  {
    protected TrainRec(Database db) : base(db) { }
  }

  public class RbfTrainRec : TrainRec
  {
    public readonly MRadialBasis[] Bases;
    public readonly double OutputBias;
    public readonly double Spread;
    public readonly bool IsDegenerate;
  }

  public class MRadialBasis : MongoObject
  {
    public readonly Vec Center;
    public readonly double Weight;
  }
  
  public class RnnTrainRec : TrainRec
  {
    public readonly Vec InitialWeights;
    public readonly MRnnSpec RnnSpec;
  }

  public class MRnnSpec : MongoObject
  {
    public readonly int NumInputs;
    public readonly MLayerSpec[] Layers;
  }

  public class MLayerSpec : MongoObject
  {
    public readonly int NodeCount;
    public readonly bool IsRecurrent;
    public readonly ActivationType ActivationType;
  }

  public class Generation : MongoObject
  {
    public readonly int Number;
    public readonly Chromosome[] Chromosomes;
    public readonly TrainRec[] TrainRecs;
  }

  public class Run : MongoObject
  {
    public readonly Object[] GeneDescriptionIds;
    public GeneDesc[] GeneDescriptions { get; set; } // TODO: implement

  }
}
