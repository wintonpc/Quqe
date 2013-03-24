using MongoDB.Driver;
using NUnit.Framework;
using Quqe;
using List = PCW.List;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Machine.Specifications;
using MongoDB.Bson;

namespace QuqeTest
{
  [TestFixture]
  class EvolutionTests
  {
    [Test]
    public void EvolveTest()
    {
      var mongoDb = TestHelpers.GetCleanDatabase();
      var db = new Database(mongoDb);

      var runSetup = new RunSetupInfo(Initialization.MakeProtoChromosome(), 10, 6, 4, 5);
      var run = Functions.Evolve(db, new LocalTrainer(), 1, runSetup);
      run.Id.ShouldBeOfType<ObjectId>();
      run.ProtoChromosome.Genes.Length.ShouldEqual(11);

      run.Generations.Length.ShouldEqual(1 + 1);
      var gen0 = run.Generations.First();

      gen0.Id.ShouldBeOfType<ObjectId>();
      gen0.Order.ShouldEqual(0);
      gen0.Mixtures.Count().ShouldEqual(10);
      gen0.Mixtures.First().Experts.Count(x => x.Chromosome.NetworkType == NetworkType.Rnn).ShouldEqual(6);
      gen0.Mixtures.First().Experts.Count(x => x.Chromosome.NetworkType == NetworkType.Rbf).ShouldEqual(4);
    }
  }
}
