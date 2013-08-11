using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using NUnit.Framework;
using Machine.Specifications;
using Quqe;
using Quqe.Rabbit;

namespace QuqeTest
{
  [TestFixture]
  class BacktestingTests
  {
    [Test]
    public void Backtest1()
    {
      var db = Database.GetProductionDatabase(new MongoHostInfo("localhost", "guest", "", "versace"));
      var run = db.QueryAll<Run>().Last();
      var allMixtures = run.Generations.SelectMany(x => x.Mixtures).ToArray();
      var bestFitness = allMixtures.Max(x => x.Evaluated.Fitness);
      var bestMixture = allMixtures.First(x => x.Evaluated.Fitness == bestFitness);

      Backtesting.Backtest(db, bestMixture, "DIA", DateTime.Parse("2/12/2003"), DateTime.Parse("8/12/2003"), 10000, 4, 5);
    }
  }
}
