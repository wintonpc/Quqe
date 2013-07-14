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

    public Database(MongoDatabase db)
    {
      MDB = db;
    }

    public T[] QueryAll<T>(Expression<Func<T, bool>> predicate, Func<T, IComparable> orderKey = null) where T : MongoTopLevelObject
    {
      var query = new QueryBuilder<T>().Where(predicate);
      return InternalQuery(query, false, orderKey);
    }

    public T[] RawQuery<T>(IMongoQuery query) where T : MongoTopLevelObject
    {
      return InternalQuery<T>(query, true, null);
    }

    T[] InternalQuery<T>(IMongoQuery query, bool isRaw, Func<T, IComparable> orderKey) where T : MongoTopLevelObject
    {
      var coll = MDB.GetCollection(typeof (T).Name);
      var cursor = coll.FindAs<T>(query);
      var items = (orderKey != null ? (IEnumerable<T>)cursor.OrderBy(orderKey) : cursor).ToArray();
      if (!isRaw)
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