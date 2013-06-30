using MongoDB.Driver;

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
