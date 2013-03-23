using System.Reflection;
using MongoDB.Bson.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Conventions;

namespace Quqe
{
  public abstract class MongoTopLevelObject
  {
    static MongoTopLevelObject()
    {
      RegisterConventions();
    }

    public static void RegisterConventions()
    {
      BsonClassMap.RegisterConventions(new ConventionProfile().SetMemberFinderConvention(new MyMemberFinderConvention()), _ => true);
    }

    [BsonIgnore]
    internal Database Database;

    protected MongoTopLevelObject(Database db)
    {
      Database = db;
    }
  }

  public class MyMemberFinderConvention : IMemberFinderConvention
  {
    public IEnumerable<MemberInfo> FindMembers(Type type)
    {
      var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
      var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
        .Where(pi => pi.GetSetMethod(true) != null);
      return fields.Concat<MemberInfo>(props);
    }
  }
}
