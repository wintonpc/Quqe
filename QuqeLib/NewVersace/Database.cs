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
    //Dictionary<Type, Dictionary<ObjectId, object>> Lookup = new Dictionary<Type, Dictionary<ObjectId, object>>();

    readonly MongoDatabase MDB;

    public Database(MongoDatabase db)
    {
      MDB = db;
    }

    public T[] QueryAll<T>(Expression<Func<T, bool>> predicate, Func<T, IComparable> orderKey = null) where T : MongoTopLevelObject
    {
      var query = new QueryBuilder<T>().Where(predicate);
      var coll = MDB.GetCollection(typeof(T).Name);
      var cursor = coll.FindAs<T>(query);
      var items = (orderKey != null ? (IEnumerable<T>)cursor.OrderBy(orderKey) : cursor).ToArray();
      foreach (var item in items)
        item.Database = this;
      return items;
    }

    public T QueryOne<T>(Expression<Func<T, bool>> predicate) where T : MongoTopLevelObject
    {
      return QueryAll(predicate).Single();
    }

    public T Get<T>(ObjectId id) where T: MongoTopLevelObject
    {
      //return Get<T>(id, GetTypeLookup<T>());
      return QueryOne<T>(x => x.Id == id);
    }

    public void Store<T>(T value) where T : MongoTopLevelObject
    {
      StoreInMongo(value);
      //GetTypeLookup<T>().Add(value.Id, value);
    }

    //T Get<T>(ObjectId id, Dictionary<ObjectId, object> typeLookup) where T : MongoTopLevelObject
    //{
    //  object value;
    //  if (!typeLookup.TryGetValue(id, out value))
    //  {
    //    value = GetFromMongo<T>(id);
    //    typeLookup.Add(id, value);
    //  }
    //  return (T)value;
    //}

    //Dictionary<ObjectId, object> GetTypeLookup<T>()
    //{
    //  lock (Lookup)
    //  {
    //    Dictionary<ObjectId, object> lookup;
    //    if (!Lookup.TryGetValue(typeof (T), out lookup))
    //    {
    //      lookup = new Dictionary<ObjectId, object>();
    //      Lookup.Add(typeof (T), lookup);
    //    }
    //    return lookup;
    //  }
    //}

    //T GetFromMongo<T>(ObjectId id) where T : MongoTopLevelObject
    //{
    //  var coll = MDB.GetCollection(typeof(T).Name);
    //  var obj = coll.FindOneAs<T>(Query.EQ("_id", id));
    //  obj.Database = this;
    //  return obj;
    //}

    public static Database GetProductionDatabase(string mongoHost)
    {
      var mongoClient = new MongoClient(mongoHost);
      var mongoServer = mongoClient.GetServer();
      var mongoDb = mongoServer.GetDatabase("versace");
      return new Database(mongoDb);
    }

    void StoreInMongo<T>(T value) where T : MongoTopLevelObject
    {
      var coll = MDB.GetCollection(typeof(T).Name);
      coll.Insert(value);
      value.Database = this;
    }
  }
}
