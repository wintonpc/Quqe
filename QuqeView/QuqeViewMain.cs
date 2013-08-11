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
using MathNet.Numerics.Statistics;

namespace QuqeView
{
  class QuqeViewMain
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

      Get["/runs"] = p => Json(DB.QueryAll<Run>().Select(RunToBson).Where(x => x != null));

      Get["/runIds"] = p => Json(DB.QueryAll<Run>().Select(x => x.Id.ToString()));

      Get["/run/{runId}"] = p => {
        var runId = new ObjectId((string)p["runId"]);
        var run = DB.QueryOne<Run>(x => x.Id == runId);
        return Json(RunToBson(run));
      };

      Get["/fitnesses/{runId}"] = p => {
        var runId = new ObjectId((string)p["runId"]);
        var run = DB.QueryOne<Run>(x => x.Id == runId);
        return Json(run.Generations.Select(x => x.Evaluated.Fitness));
      };

      Get["/bars/{symbol}"] = p => {
        var symbol = (string)p["symbol"];
        return Json(DB.QueryAll<DbBar>(x => x.Symbol == symbol, "Timestamp"));
      };

      Get["/bars/{symbol}/from/{fYear}/{fMonth}/{fDay}/to/{tYear}/{tMonth}/{tDay}"] = p => {
        var symbol = (string)p["symbol"];
        var from = new DateTime((int)p["fYear"], (int)p["fMonth"], (int)p["fDay"]);
        var to = new DateTime((int)p["tYear"], (int)p["tMonth"], (int)p["tDay"]);
        return Json(DB.QueryAll<DbBar>(x => x.Symbol == symbol, "Timestamp").Where(x => from <= x.Timestamp && x.Timestamp <= to));
      };

      Get["/run/{runId}/gen/{genIdx}"] = p => {
        var runId = new ObjectId((string)p["runId"]);
        var genIdx = (int)p["genIdx"];
        var run = DB.QueryOne<Run>(x => x.Id == runId);
        var gen = run.Generations[genIdx];
        var genDoc = gen.ToBsonDocument();

        //genDoc["Mixtures"] = ToBson(gen.Mixtures.Select(MixtureToBson));
        genDoc["Evaluated"] = ToBson(gen.Evaluated);

        return Json(genDoc);
      };
    }

    static BsonDocument RunToBson(Run run)
    {
      try
      {
        var runDoc = run.ToBsonDocument();
        runDoc["Generations"] = ToBson(run.Generations.OrderBy(x => x.Order).Select(GenerationToBson));
        return runDoc;
      }
      catch (Exception)
      {
        return null;
      }
    }

    static BsonDocument GenerationToBson(Generation gen)
    {
      var genDoc = gen.ToBsonDocument();
      genDoc["Evaluated"] = ToBson(gen.Evaluated);
      return genDoc;
    }

    static BsonDocument MixtureToBson(Mixture m)
    {
      var mixtureDoc = m.ToBsonDocument();
      mixtureDoc["Experts"] = ToBson(m.Experts.Select(ExpertToBson));
      return mixtureDoc;
    }

    static BsonDocument ExpertToBson(Expert expert)
    {
      return expert.ToBsonDocument();
    }

    static Response Json(object obj)
    {
      return Json(ToBson(obj));
    }

    static Response Json(BsonValue bson)
    {
      var jsonBytes = Encoding.UTF8.GetBytes(bson.ToJson(typeof (object), new JsonWriterSettings {
        OutputMode = JsonOutputMode.JavaScript,
        Indent = true,
        NewLineChars = "\n",
        IndentChars = " "
      }));

      return new Response {
        Contents = s => s.Write(jsonBytes, 0, jsonBytes.Length),
        ContentType = "application/json"
      };
    }

    static BsonValue ToBson(object obj)
    {
      if (obj is System.Collections.IEnumerable)
        return new BsonArray(((System.Collections.IEnumerable)obj).Cast<object>().Select(OneToBson));
      else
        return (BsonValue)OneToBson(obj);
    }

    static object OneToBson(object obj)
    {
      if (obj is double)
        return obj;
      if (obj is string)
        return obj;
      return obj.ToBsonDocument();
    }
  }
}