using System.Diagnostics;
using MongoDB.Bson.IO;
using Nancy.Hosting.Self;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy;
using Quqe;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using Nancy.Conventions;
using Quqe.Rabbit;

namespace QuqeView
{
  class Program
  {
    static void Main(string[] args)
    {
      var host = new NancyHost(new HostConfiguration() {
        UrlReservations = new UrlReservations { CreateAutomatically = true, User = "Administrator" }
      }, new Uri("http://localhost:8888/quqe/"));
      host.Start();
      Console.WriteLine("Nancy is running. Press ENTER to exit...");
      Console.ReadLine();
    }
  }

  public class CustomConventions : DefaultNancyBootstrapper
  {
    protected override void ConfigureConventions(NancyConventions nancyConventions)
    {
      base.ConfigureConventions(nancyConventions);

      nancyConventions.StaticContentsConventions.AddRange(new[] {
        StaticContentConventionBuilder.AddDirectory("/pages", "pages")
      });
    }
  }

  public class QuqeModule : NancyModule
  {
    Database DB;

    public QuqeModule()
    {
      DB = Database.GetProductionDatabase(new MongoHostInfo("localhost", "guest", "", "versace"));

      Get["/"] = x => {
        return "hello world";
      };

      Get["/protoruns"] = p => Json(DB.QueryAll<ProtoRun>());
    }

    static string Json<T>(IEnumerable<T> items)
    {
      return new BsonArray(items.Select(x => x.ToBsonDocument())).ToJson(typeof (object), new JsonWriterSettings {
        OutputMode = JsonOutputMode.JavaScript,
        Indent = true,
        NewLineChars = "\n",
        IndentChars = " "
      });
    }
  }
}