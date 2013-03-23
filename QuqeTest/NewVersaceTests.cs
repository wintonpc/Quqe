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
  class NewVersaceTests
  {
    [Test]
    public void EvolveTest()
    {
      var client = new MongoClient("mongodb://localhost");
      var server = client.GetServer();
      server.DropDatabase("test");
      var mongoDb = server.GetDatabase("test");
      var db = new Database(mongoDb);

      var geneDescriptions = List.Repeat(2, i => ProtoGene.CreateBoolean("Gene" + i));
      var protoChromosome = new ProtoChromosome(geneDescriptions);

      var run = Functions.Evolve(db, new LocalTrainer(), 10, 1, 2, 3, protoChromosome);
      run.Id.ShouldBeOfType<ObjectId>();
      run.ProtoChromosomes.ProtoGenes.Length.ShouldEqual(2);

      var gen0 = run.Generations.First();

      gen0.Id.ShouldBeOfType<ObjectId>();
      gen0.Order.ShouldEqual(0);
      gen0.Mixtures.First().Experts.Count(x => x.Chromosome.NetworkType == NetworkType.Rnn).ShouldEqual(2);
      gen0.Mixtures.First().Experts.Count(x => x.Chromosome.NetworkType == NetworkType.Rbf).ShouldEqual(3);
    }
  }
}
