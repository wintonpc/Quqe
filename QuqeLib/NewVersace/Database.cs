using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Quqe
{
  public class Database
  {
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
      return QueryOne<T>(x => x.Id == id);
    }

    public void Store<T>(T value) where T : MongoTopLevelObject
    {
      StoreInMongo(value);
    }

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
