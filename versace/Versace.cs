﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HostLib;
using System.IO;
using MongoDB.Bson;
using Quqe;
using RabbitMQ.Client;
using System.Diagnostics;

namespace versace
{
  class Versace
  {
    static void Main(string[] args)
    {
      var masterReqPath = args[0];
      var masterReq = (MasterRequest)RabbitMessageReader.Read(0, File.ReadAllBytes(masterReqPath));

      using (var rabbit = new Rabbit(ConfigurationManager.AppSettings["RabbitHost"]))
      {
        rabbit.SendMasterRequest(masterReq);
        rabbit.StartEvolution();
        var sw = new Stopwatch();
        sw.Start();

        var db = Database.GetProductionDatabase(ConfigurationManager.AppSettings["MongoHost"]);
        while (true)
        {
          RabbitMessage msg = rabbit.GetMasterMessage();
          if (msg is MasterUpdate)
          {
            var update = (MasterUpdate)msg;
            Console.WriteLine("Gen {0} {1}", update.GenerationNumber, update.Fitness);
          }
          else if (msg is MasterResult)
          {
            var result = (MasterResult)msg;
            var run = db.Get<Run>(result.RunId);
            Console.WriteLine("Finished Run {0} with fitness {1} in {2}", run.Id, run.Generations.Max(x => x.Evaluated.Fitness), sw.Elapsed);
          }
        }
      }
    }
  }
}