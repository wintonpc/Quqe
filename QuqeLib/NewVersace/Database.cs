using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using System.Windows;
using System.Linq.Expressions;

namespace Quqe
{
  public class Database
  {
    Dictionary<Type, Dictionary<ObjectId, object>> Lookup = new Dictionary<Type, Dictionary<ObjectId, object>>();

    readonly MongoDatabase MDB;

    public Database(MongoDatabase db)
    {
      MDB = db;
    }

    public IEnumerable<T> QueryAll<T>(Expression<Func<T, bool>> predicate)
    {
      var query = new QueryBuilder<T>().Where(predicate);
      var coll = MDB.GetCollection(typeof(T).Name);
      return coll.FindAs<T>(query);
    }

    public T Get<T>(ObjectId id) where T: MongoTopLevelObject
    {
      return Get<T>(id, GetTypeLookup<T>());
    }

    public void Set<T>(T value, Func<T, ObjectId> getId) where T : MongoTopLevelObject
    {
      StoreInMongo<T>(value);
      GetTypeLookup<T>().Add(getId(value), value);
    }

    T Get<T>(ObjectId id, Dictionary<ObjectId, object> typeLookup) where T : MongoTopLevelObject
    {
      object value;
      if (!typeLookup.TryGetValue(id, out value))
      {
        value = GetFromMongo<T>(id);
        typeLookup.Add(id, value);
      }
      return (T)value;
    }

    Dictionary<ObjectId, object> GetTypeLookup<T>()
    {
      Dictionary<ObjectId, object> lookup;
      if (!Lookup.TryGetValue(typeof(T), out lookup))
      {
        lookup = new Dictionary<ObjectId, object>();
        Lookup.Add(typeof(T), lookup);
      }
      return lookup;
    }

    T GetFromMongo<T>(ObjectId id) where T : MongoTopLevelObject
    {
      var coll = MDB.GetCollection(typeof(T).Name);
      var obj = coll.FindOneAs<T>(Query.EQ("_id", id));
      obj.Database = this;
      return obj;
    }

    void StoreInMongo<T>(T value) where T : MongoTopLevelObject
    {
      var coll = MDB.GetCollection(typeof(T).Name);
      coll.Insert(value);
      value.Database = this;
    }
  }
}
