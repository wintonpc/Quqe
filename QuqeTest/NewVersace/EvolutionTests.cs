using MongoDB.Driver;
using NUnit.Framework;
using Quqe;
using Quqe.NewVersace;
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
  public class EvolutionTests
  {
    [Test]
    public void EvolveTest()
    {
      var mongoDb = TestHelpers.GetCleanDatabase();
      var db = new Database(mongoDb);

      var protoRun = new ProtoRun(db, "EvolveTest", 3, Initialization.MakeFastestProtoChromosome(), 10, 6, 4, 5, 0.05);
      var seed = MakeTrainingSet("11/11/2001", "02/12/2003");
      var run = Functions.Evolve(protoRun, new LocalParallelTrainer(), seed);
      run.Id.ShouldBeOfType<ObjectId>();
      run.ProtoChromosome.Genes.Length.ShouldEqual(11);

      run.Generations.Length.ShouldEqual(3);
      run.Generations[0].Order.ShouldEqual(0);
      run.Generations[1].Order.ShouldEqual(1);
      run.Generations[2].Order.ShouldEqual(2);

      var dbTypes = run.Generations[1].Mixtures.First().Experts.Select(x => x.Chromosome.DatabaseType).Distinct();
      dbTypes.Count().ShouldEqual(2);

      var gen2 = run.Generations[2];
      gen2.Id.ShouldBeOfType<ObjectId>();
      gen2.Mixtures.Count().ShouldEqual(10);
      gen2.Mixtures.First().Experts.Count(x => x.Chromosome.NetworkType == NetworkType.Rnn).ShouldEqual(6);
      gen2.Mixtures.First().Experts.Count(x => x.Chromosome.NetworkType == NetworkType.Rbf).ShouldEqual(4);
    }

    [Test]
    public void LocalEvolveTest()
    {
      var mongoDb = TestHelpers.GetCleanDatabase();
      var db = new Database(mongoDb);

      var protoRun = new ProtoRun(db, "LocalEvolveTest", 3, Initialization.MakeProtoChromosome(), 10, 10, 0, 5, 0.05);
      var seed = MakeTrainingSet("11/11/2001", "02/12/2003");
      var run = Functions.Evolve(protoRun, new LocalParallelTrainer(), seed);
      Trace.WriteLine("Generation fitnesses: " + run.Generations.Select(x => x.Evaluated.Fitness).Join(", "));
    }

    [Test]
    public void MixtureCrossover()
    {
      var db = new Database(TestHelpers.GetCleanDatabase());
      var protoChrom = Initialization.MakeFastestProtoChromosome();
      var protoRun = new ProtoRun(db, "MixtureCrossoverTest", -1, protoChrom, 2, 10, 0, 10, 0.05);
      var run = new Run(protoRun, protoChrom);
      var seed = MakeTrainingSet("11/11/2001", "02/12/2003");
      var gen = Initialization.MakeInitialGeneration(seed, run, new LocalTrainer());
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

    static DataSet MakeTrainingSet(string startDate, string endDate)
    {
      return DataPreprocessing.MakeTrainingSet("DIA", DateTime.Parse(startDate), DateTime.Parse(endDate), Signals.NextClose);
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