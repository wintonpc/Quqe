using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using Quqe;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Machine.Specifications;

namespace QuqeTest
{
  [TestFixture]
  class MongoTests
  {
    [Test]
    public void Sandbox()
    {

    }

    [Test]
    public void DatabaseWorks()
    {
      var client = new MongoClient("mongodb://localhost");
      var server = client.GetServer();
      server.DropDatabase("test");
      var testDb = server.GetDatabase("test");

      var db1 = new Database(testDb);
      var gd1 = GeneDesc.Create(db1, "name", 0, 100, GeneType.Continuous);
      db1.Set(gd1, x => x.Id);

      var db2 = new Database(testDb);
      var gd2 = db2.Get<GeneDesc>(gd1.Id);
    }

    [Test]
    public void DatabaseReferences()
    {
      var client = new MongoClient("mongodb://localhost");
      var server = client.GetServer();
      server.DropDatabase("test");
      var testDb = server.GetDatabase("test");

      var db1 = new Database(testDb);
      var gd = GeneDesc.Create(db1, "name", 0, 100, GeneType.Continuous);
      var g = new Gene(gd, 55);
      g.GeneDescId.ShouldEqual(gd.Id);
    }
  }
}
