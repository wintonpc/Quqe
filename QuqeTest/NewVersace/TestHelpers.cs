using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuqeTest
{
  static class TestHelpers
  {
    public static MongoDatabase GetCleanDatabase()
    {
      var client = new MongoClient("mongodb://localhost");
      var server = client.GetServer();
      server.DropDatabase("test");
      return server.GetDatabase("test");
    }
  }
}
