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
using System.Diagnostics;
using PCW;

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

      var protoRun = new ProtoRun(db, "EvolveTest", 1, Initialization.MakeFastestProtoChromosome(), 10, 6, 4, 5);
      var seed = Preprocessing.MakeTrainingSeed(DateTime.Parse("11/11/2001"), DateTime.Parse("02/12/2003"));
      var run = Functions.Evolve(protoRun, new LocalTrainer(), seed);
      run.Id.ShouldBeOfType<ObjectId>();
      run.ProtoChromosome.Genes.Length.ShouldEqual(11);

      run.Generations.Length.ShouldEqual(1 + 1);
      var gen0 = run.Generations.First();

      var dbTypes = gen0.Mixtures.First().Experts.Select(x => x.Chromosome.DatabaseType).Distinct();
      dbTypes.Count().ShouldEqual(2);

      gen0.Id.ShouldBeOfType<ObjectId>();
      gen0.Order.ShouldEqual(0);
      gen0.Mixtures.Count().ShouldEqual(10);
      gen0.Mixtures.First().Experts.Count(x => x.Chromosome.NetworkType == NetworkType.Rnn).ShouldEqual(6);
      gen0.Mixtures.First().Experts.Count(x => x.Chromosome.NetworkType == NetworkType.Rbf).ShouldEqual(4);
    }

    [Test]
    public void LocalEvolveTest()
    {
      var mongoDb = TestHelpers.GetCleanDatabase();
      var db = new Database(mongoDb);

      var protoRun = new ProtoRun(db, "LocalEvolveTest", 3, Initialization.MakeProtoChromosome(), 10, 10, 0, 5);
      var seed = Preprocessing.MakeTrainingSeed(DateTime.Parse("11/11/2001"), DateTime.Parse("02/12/2003"));
      var run = Functions.Evolve(protoRun, new LocalTrainer(), seed);
      Trace.WriteLine("Generation fitnesses: " + run.Generations.Select(x => x.Evaluated.Fitness).Join(", "));
    }

    [Test]
    public void MixtureCrossover()
    {
      var db = new Database(TestHelpers.GetCleanDatabase());
      var protoChrom = Initialization.MakeFastestProtoChromosome();
      var run = new Run(db, protoChrom);
      var seed = Preprocessing.MakeTrainingSeed(DateTime.Parse("11/11/2001"), DateTime.Parse("02/12/2003"));
      var protoRun = new ProtoRun(db, "MixtureCrossoverTest", -1, protoChrom, 2, 10, 0, 10);
      var gen = Initialization.MakeInitialGeneration(seed, run, protoRun, new LocalTrainer());
      gen.Mixtures.Length.ShouldEqual(2);
      var m1 = gen.Mixtures[0];
      var m2 = gen.Mixtures[1];

      var prev = Tuple2.Create(m1.Chromosomes, m2.Chromosomes);
      var cmis = Functions.CombineTwoMixtures(gen.Mixtures.Select(m => new MixtureEval(m, 1)).ToList());
      var curr = Tuple2.Create(cmis.Item1.Chromosomes, cmis.Item2.Chromosomes);

      for (int i = 0; i < m1.Chromosomes.Length; i++)
        AssertIsCrossedOverVersionOf(Tuple2.Create(curr.Item1[i], curr.Item2[i]),
                                     Tuple2.Create(prev.Item1[i], prev.Item2[i]));
    }

    [Test]
    public void ChromosomeCrossover()
    {
      var protoChrom = Initialization.MakeProtoChromosome();
      var a = Initialization.RandomChromosome(NetworkType.Rnn, protoChrom);
      var b = Initialization.RandomChromosome(NetworkType.Rnn, protoChrom);
      a.ShouldNotLookLike(b);

      var crossedOverChroms = Functions.CrossOverChromosomes(a, b);
      AssertIsCrossedOverVersionOf(crossedOverChroms, Tuple2.Create(a, b));
    }

    static void AssertIsCrossedOverVersionOf(Tuple2<Chromosome> curr, Tuple2<Chromosome> prev)
    {
      var a1 = curr.Item1;
      var b1 = curr.Item2;
      var a0 = prev.Item1;
      var b0 = prev.Item2;

      a1.ShouldNotLookLike(a0);
      b1.ShouldNotLookLike(b0);

      List.Repeat(a1.Genes.Length, i => {
        var a1g = a1.Genes[i];
        var b1g = b1.Genes[i];
        var a0g = a0.Genes[i];
        var b0g = b0.Genes[i];

        if (a1g.LooksLike(a0g))
          b1g.ShouldLookLike(b0g);
        else if (a1g.LooksLike(b0g))
          b1g.ShouldLookLike(a0g);
        else
          Assert.Fail("gene was mutated??");
      });
    }
  }
}
