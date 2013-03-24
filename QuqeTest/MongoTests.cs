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
using List = PCW.List;
using MongoDB.Bson.Serialization.Attributes;

namespace QuqeTest
{
  [TestFixture]
  class MongoTests
  {
    [Test]
    public void Sandbox()
    {
      var client = new MongoClient("mongodb://localhost");
      var server = client.GetServer();
      server.DropDatabase("sandbox");
      var mongoDb = server.GetDatabase("sandbox");

      var coll = mongoDb.GetCollection("widgets");
      coll.Insert(new Widget(new int[] { 1, 2, 3, 4, 5 }, List.Repeat(3, _ => new Sprocket()).ToArray()));

      var results = coll.FindAllAs<Widget>().ToList();
    }

    [Test]
    public void Sandbox2()
    {
      var client = new MongoClient("mongodb://localhost");
      var server = client.GetServer();
      var mongoDb = server.GetDatabase("test");

      var coll = mongoDb.GetCollection("Generation");
      var results = coll.FindAllAs<Generation>().ToList();
      var results2 = coll.FindAll().ToList();
    }

    class Widget
    {
      [BsonId]
      public ObjectId Id;

      public int Foo;
      public int[] Bars;
      public Sprocket[] Sprockets;

      public Widget(int[] bars, Sprocket[] sprockets)
      {
        Bars = bars;
        Sprockets = sprockets;
      }
    }

    class Sprocket
    {
      public int Ding = 2;
      public int Dong = 3;
    }

    [Test]
    public void DatabaseWorks()
    {

    }

    [Test]
    public void DatabaseReferences()
    {

    }
  }
}
