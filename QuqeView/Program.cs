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

      Get["/runs"] = p => Json(DB.QueryAll<Run>());

      Get["/correlations/{run_id}"] = p => {
        var run_id = new ObjectId((string)p["run_id"]);
        var run = DB.QueryOne<Run>(x => x.Id == run_id);

        var cis = run.Generations.SelectMany(g => g.Mixtures.SelectMany(mix => mix.Experts)).Select(x => new CorrInfo(x.Chromosome, x.TrainingSeconds)).ToArray();

        var rnns = cis.Where(x => x.Chrom.NetworkType == NetworkType.Rnn).ToArray();
        var rbfs = cis.Where(x => x.Chrom.NetworkType == NetworkType.Rbf).ToArray();

        var rnnTimes = rnns.Select(x => x.TrainingSeconds).ToArray();
        var rbfTimes = rbfs.Select(x => x.TrainingSeconds).ToArray();

        var rnnLayer1 = Correlation.Pearson(rnnTimes, rnns.Select(x => (double)x.Chrom.RnnLayer1NodeCount));
        var rnnLayer2 = Correlation.Pearson(rnnTimes, rnns.Select(x => (double)x.Chrom.RnnLayer2NodeCount));
        var sizePct = Correlation.Pearson(rnnTimes, rnns.Select(x => (double)x.Chrom.TrainingSizePct));
        var nodes = Correlation.Pearson(rnnTimes, rnns.Select(x => (double)x.Chrom.RnnLayer1NodeCount * x.Chrom.RnnLayer2NodeCount));
        var sizeXnodes = Correlation.Pearson(rnnTimes, rnns.Select(x => (double)x.Chrom.TrainingSizePct * x.Chrom.RnnLayer1NodeCount * x.Chrom.RnnLayer2NodeCount));
        var dbType = Correlation.Pearson(rnnTimes, rnns.Select(x => (double)(x.Chrom.DatabaseType == DatabaseType.A ? .3 : 1)));
        var sizePctXdbType = Correlation.Pearson(rnnTimes, rnns.Select(x => (double)x.Chrom.TrainingSizePct * (x.Chrom.DatabaseType == DatabaseType.A ? .3 : 1)));
        var epochs = Correlation.Pearson(rnnTimes, rnns.Select(x => (double)x.Chrom.RnnTrainingEpochs));
        var sizePctXepochs = Correlation.Pearson(rnnTimes, rnns.Select(x => (double)x.Chrom.TrainingSizePct * x.Chrom.RnnTrainingEpochs));
        var cc = Correlation.Pearson(rnnTimes, rnns.Select(x => (double)(x.Chrom.UseComplementCoding ? 1 : 0)));
        var sizePct2Xepochs = Correlation.Pearson(rnnTimes, rnns.Select(x => (double)x.Chrom.TrainingSizePct * x.Chrom.TrainingSizePct * x.Chrom.RnnTrainingEpochs));


        var doeRbf = Correlation.Pearson(rbfTimes, rbfs.Select(ci =>
        {
          var x = ci.Chrom;
          return 5.4678
                 + x.TrainingSizePct * -10.8217
                 + (x.DatabaseType == DatabaseType.A ? 0 : 1) * -1.6441
                 + (x.UseComplementCoding ? 1 : 0) * -2.3452
                 + (x.UsePCA ? 1 : 0) * -3.0230
                 + x.RnnTrainingEpochs / 100.0 * -0.7369
                 + x.RnnLayer2NodeCount * 0.0038
                 + x.RbfNetTolerance * -1.7168
                 + x.RbfGaussianSpread * -0.4654
                 + x.TrainingSizePct * x.TrainingSizePct * 12.9581
                 + (x.RnnTrainingEpochs / 100.0) * 0.0330
                 + x.RbfGaussianSpread * x.RbfGaussianSpread * 0.0493
                 + x.TrainingSizePct * (x.DatabaseType == DatabaseType.A ? 0 : 1) * 3.0484
                 + x.TrainingSizePct * (x.UseComplementCoding ? 1 : 0) * 2.5783
                 + x.TrainingSizePct * (x.UsePCA ? 1 : 0) * 6.8649
                 + x.TrainingSizePct * x.RnnTrainingEpochs / 100.0 * 0.6369
                 + x.TrainingSizePct * x.RnnLayer2NodeCount * -0.0096
                 + x.TrainingSizePct * x.RbfNetTolerance * -4.3113
                 + x.TrainingSizePct * x.RbfGaussianSpread * -0.2953
                 + (x.DatabaseType == DatabaseType.A ? 0 : 1) * (x.UsePCA ? 1 : 0) * 1.0614
                 + (x.DatabaseType == DatabaseType.A ? 0 : 1) * x.RbfGaussianSpread * 0.0748
                 + (x.UseComplementCoding ? 1 : 0) * (x.UsePCA ? 1 : 0) * 1.0379
                 + (x.UseComplementCoding ? 1 : 0) * x.RbfNetTolerance * 1.1799
                 + (x.UseComplementCoding ? 1 : 0) * x.RbfGaussianSpread * 0.0979
                 + (x.UsePCA ? 1 : 0) * x.RnnTrainingEpochs / 100.0 * 0.2522
                 + (x.UsePCA ? 1 : 0) * x.RnnLayer2NodeCount * -0.0066
                 + (x.UsePCA ? 1 : 0) * x.RbfNetTolerance * 0.5956
                 + x.RnnTrainingEpochs / 100.0 * x.RbfNetTolerance * 0.2322
                 + x.RnnLayer2NodeCount * x.RbfNetTolerance * 0.0048;
        }));


        var doeRnn = Correlation.Pearson(rnnTimes, rnns.Select(ci =>
        {
          var x = ci.Chrom;
          return 5.4678
                 + x.TrainingSizePct * -10.8217
                 + (x.DatabaseType == DatabaseType.A ? 0 : 1) * -1.6441
                 + (x.UseComplementCoding ? 1 : 0) * -2.3452
                 + (x.UsePCA ? 1 : 0) * -3.0230
                 + x.RnnTrainingEpochs / 100.0 * -0.7369
                 + x.RnnLayer2NodeCount * 0.0038
                 + x.RbfNetTolerance * -1.7168
                 + x.RbfGaussianSpread * -0.4654
                 + x.TrainingSizePct * x.TrainingSizePct * 12.9581
                 + (x.RnnTrainingEpochs / 100.0) * 0.0330
                 + x.RbfGaussianSpread * x.RbfGaussianSpread * 0.0493
                 + x.TrainingSizePct * (x.DatabaseType == DatabaseType.A ? 0 : 1) * 3.0484
                 + x.TrainingSizePct * (x.UseComplementCoding ? 1 : 0) * 2.5783
                 + x.TrainingSizePct * (x.UsePCA ? 1 : 0) * 6.8649
                 + x.TrainingSizePct * x.RnnTrainingEpochs / 100.0 * 0.6369
                 + x.TrainingSizePct * x.RnnLayer2NodeCount * -0.0096
                 + x.TrainingSizePct * x.RbfNetTolerance * -4.3113
                 + x.TrainingSizePct * x.RbfGaussianSpread * -0.2953
                 + (x.DatabaseType == DatabaseType.A ? 0 : 1) * (x.UsePCA ? 1 : 0) * 1.0614
                 + (x.DatabaseType == DatabaseType.A ? 0 : 1) * x.RbfGaussianSpread * 0.0748
                 + (x.UseComplementCoding ? 1 : 0) * (x.UsePCA ? 1 : 0) * 1.0379
                 + (x.UseComplementCoding ? 1 : 0) * x.RbfNetTolerance * 1.1799
                 + (x.UseComplementCoding ? 1 : 0) * x.RbfGaussianSpread * 0.0979
                 + (x.UsePCA ? 1 : 0) * x.RnnTrainingEpochs / 100.0 * 0.2522
                 + (x.UsePCA ? 1 : 0) * x.RnnLayer2NodeCount * -0.0066
                 + (x.UsePCA ? 1 : 0) * x.RbfNetTolerance * 0.5956
                 + x.RnnTrainingEpochs / 100.0 * x.RbfNetTolerance * 0.2322
                 + x.RnnLayer2NodeCount * x.RbfNetTolerance * 0.0048;
        }));

        var sb = new StringBuilder();
        sb.AppendFormat("rnnLayer1: {0:N3}<br>\n", rnnLayer1);
        sb.AppendFormat("rnnLayer2: {0:N3}<br>\n", rnnLayer2);
        sb.AppendFormat("sizePct: {0:N3}<br>\n", sizePct);
        sb.AppendFormat("nodes: {0:N3}<br>\n", nodes);
        sb.AppendFormat("sizeXnodes: {0:N3}<br>\n", sizeXnodes);
        sb.AppendFormat("dbType: {0:N3}<br>\n", dbType);
        sb.AppendFormat("sizePctXdbType: {0:N3}<br>\n", sizePctXdbType);
        sb.AppendFormat("epochs: {0:N3}<br>\n", epochs);
        sb.AppendFormat("sizePctXepochs: {0:N3}<br>\n", sizePctXepochs);
        sb.AppendFormat("cc: {0:N3}<br>\n", cc);
        sb.AppendFormat("doeRbf: {0:N3}<br>\n", doeRbf);
        sb.AppendFormat("doeRnn: {0:N3}<br>\n", doeRnn);
        sb.AppendFormat("sizePct2Xepochs: {0:N3}<br>\n", sizePct2Xepochs);

        //var sb = new StringBuilder();

        //sb.AppendLine(string.Join(",", new[] {
        //  "TrainingSeconds", "TrainingOffsetPct", "TrainingSizePct", "DatabaseType", "UseComplementCoding", "UsePCA", "PrincipalComponent",
        //  "RnnTrainingEpochs", "RnnLayer1NodeCount", "RnnLayer2NodeCount", "RbfNetTolerance", "RbfGaussianSpread"
        //}));
        //foreach (var x in rbfs)
        //  sb.AppendLine(string.Join(",", new object[] {
        //    x.TrainingSeconds,
        //    x.Chrom.TrainingOffsetPct,
        //    x.Chrom.TrainingSizePct,
        //    x.Chrom.DatabaseType == DatabaseType.A ? 0 : 1,
        //    x.Chrom.UseComplementCoding ? 1 : 0,
        //    x.Chrom.UsePCA ? 1 : 0,
        //    x.Chrom.PrincipalComponent,
        //    x.Chrom.RnnTrainingEpochs,
        //    x.Chrom.RnnLayer1NodeCount,
        //    x.Chrom.RnnLayer2NodeCount,
        //    x.Chrom.RbfNetTolerance,
        //    x.Chrom.RbfGaussianSpread
        //  }));

        return sb.ToString();
      };
    }

    class CorrInfo
    {
      public readonly Chromosome Chrom;
      public readonly double TrainingSeconds;

      public CorrInfo(Chromosome chrom, double trainingSeconds)
      {
        Chrom = chrom;
        TrainingSeconds = trainingSeconds;
      }
    }

    static Response Json<T>(IEnumerable<T> items)
    {
      var jsonBytes = Encoding.UTF8.GetBytes(new BsonArray(items.Select(x => x.ToBsonDocument())).ToJson(typeof (object), new JsonWriterSettings {
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
  }
}