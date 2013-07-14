using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Quqe.Rabbit;

namespace Quqe
{
  public class Database
  {
    readonly MongoDatabase MDB;

    public MongoDatabase MongoDatabase { get { return MDB; } }

    public Database(MongoDatabase db)
    {
      MDB = db;
    }

    public T[] QueryAll<T>(Expression<Func<T, bool>> predicate = null, string orderKey = null) where T : MongoTopLevelObject
    {
      var query = predicate != null ? new QueryBuilder<T>().Where(predicate) : null;
      return InternalQuery<T>(query, orderKey);
    }

    T[] InternalQuery<T>(IMongoQuery query, string orderKey) where T : MongoTopLevelObject
    {
      var coll = MDB.GetCollection(typeof (T).Name);
      MongoCursor<T> cursor;
      if (query == null)
        cursor = coll.FindAllAs<T>();
      else
        cursor = coll.FindAs<T>(query);
      if (orderKey != null)
        cursor.SetSortOrder(SortBy.Ascending(orderKey));
      var items = cursor.ToArray();
      foreach (var item in items)
        item.Database = this;
      return items;
    }

    public T QueryOne<T>(Expression<Func<T, bool>> predicate) where T : MongoTopLevelObject
    {
      return QueryAll(predicate).Single();
    }

    public T Get<T>(ObjectId id) where T : MongoTopLevelObject
    {
      return QueryOne<T>(x => x.Id == id);
    }

    public void Store<T>(T value) where T : MongoTopLevelObject
    {
      StoreInMongo(value);
    }

    public void StoreAll<T>(IEnumerable<T> values) where T : MongoTopLevelObject
    {
      StoreAllInMongo(values);
    }

    public static Database GetProductionDatabase(MongoHostInfo mongo)
    {
      var mongoClient = new MongoClient(new MongoClientSettings {
        Server = new MongoServerAddress(mongo.Hostname),
        Credentials = new[] { MongoCredential.CreateMongoCRCredential(mongo.DatabaseName, mongo.Username, mongo.Password) },
      });
      var mongoServer = mongoClient.GetServer();
      var mongoDb = mongoServer.GetDatabase(mongo.DatabaseName);
      return new Database(mongoDb);
    }

    void StoreInMongo<T>(T value) where T : MongoTopLevelObject
    {
      var coll = MDB.GetCollection(typeof (T).Name);
      coll.Insert(value);
      value.Database = this;
    }

    void StoreAllInMongo<T>(IEnumerable<T> values) where T : MongoTopLevelObject
    {
      var coll = MDB.GetCollection(typeof (T).Name);
      coll.InsertBatch(values);
      foreach (var value in values)
        value.Database = this;
    }
  }
}