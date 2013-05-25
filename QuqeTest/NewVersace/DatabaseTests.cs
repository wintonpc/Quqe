using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Machine.Specifications;
using Quqe;
using MongoDB.Bson;

namespace QuqeTest
{
  [TestFixture]
  class DatabaseTests
  {
    [Test]
    public void StoreObject()
    {
      var mongoDb = TestHelpers.GetCleanDatabase();
      var db1 = new Database(mongoDb);
      var obj = new TestObject(db1, "foo");
      obj.Id.ShouldEqual(ObjectId.Empty);
      db1.Store(obj);
      obj.Id.ShouldNotEqual(ObjectId.Empty);

      var objCollection = mongoDb.GetCollection("TestObject");
      var allObjs = objCollection.FindAll().ToList();
      allObjs.Count.ShouldEqual(1);
      var firstObj = allObjs.Single();
      firstObj["_id"].ShouldEqual(obj.Id);
      firstObj["Value"].ShouldEqual("foo");
    }

    [Test]
    public void GetObject()
    {
      var mongoDb = TestHelpers.GetCleanDatabase();
      var db1 = new Database(mongoDb);
      var protoRun = new ProtoRun(db1, "", 0, null, 0, 0, 0, 0, 0);
      var run = new Run(protoRun, Initialization.MakeProtoChromosome());
      var id = run.Id;

      var db2 = new Database(mongoDb);
      var db2Run = db2.Get<Run>(id);
      db2Run.Database.ShouldNotBeNull();
      db2Run.Database.ShouldNotEqual(db1);
      db2Run.Id.ShouldEqual(run.Id);
      db2Run.ProtoChromosome.ShouldLookLike(run.ProtoChromosome);

      var db2Run2 = db2.Get<Run>(id);
      db2Run2.ShouldLookLike(db2Run);
    }

    [Test]
    public void Querying()
    {
      var mongoDb = TestHelpers.GetCleanDatabase();
      var db1 = new Database(mongoDb);
      var protoRun = new ProtoRun(db1, "", 0, null, 0, 0, 0, 0, 0);
      var run1 = new Run(protoRun, Initialization.MakeProtoChromosome());
      var run2 = new Run(protoRun, Initialization.MakeProtoChromosome());

      var db2 = new Database(mongoDb);
      var runs1 = db2.QueryAll<Run>(_ => true);
      runs1.Length.ShouldEqual(2);
      runs1[0].Id.ShouldEqual(run1.Id);
      runs1[1].Id.ShouldEqual(run2.Id);

      var runs2 = db2.QueryAll<Run>(x => x.Id == run1.Id);
      var runs2Single = runs2.Single();
      runs2Single.Id.ShouldEqual(run1.Id);
      runs2Single.ShouldLookLike(runs1[0]);
      runs2Single.ShouldNotEqual(runs1[0]);
      runs2Single.Database.ShouldEqual(db2);
    }
  }

  class TestObject : MongoTopLevelObject
  {
    public string Value { get; private set; }

    public TestObject(Database db, string value)
      : base(db)
    {
      Value = value;
    }
  }
}
