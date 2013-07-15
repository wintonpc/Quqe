using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Machine.Specifications;
using MongoDB.Bson;
using NUnit.Framework;
using Quqe;
using Quqe.NewVersace;
using Quqe.Rabbit;

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
      var dataSets = MakeTrainingAndValidationSets(db, "11/11/2001", "02/12/2002");
      var run = Functions.Evolve(protoRun, new LocalParallelTrainer(), dataSets.Item1, dataSets.Item2);
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

      foreach (var gen in run.Generations)
        foreach (var mixture in gen.Mixtures)
          mixture.Chromosomes.Select(x => x.OrderInMixture).ShouldEnumerateLike(
                                                                                mixture.Chromosomes.OrderBy(x => x.OrderInMixture).Select(x => x.OrderInMixture));
    }

    //[Test]
    public void LocalEvolveTest()
    {
      var mongoDb = TestHelpers.GetCleanDatabase();
      var db = new Database(mongoDb);

      var protoRun = new ProtoRun(db, "LocalEvolveTest", 4, Initialization.MakeProtoChromosome(), 10, 6, 4, 4, 0.05);
      var trainingStart = "11/11/2001";
      var validationEnd = "05/12/2003";
      var dataSets = MakeTrainingAndValidationSets(db, trainingStart, validationEnd);
      var run = Functions.Evolve(protoRun, new LocalParallelTrainer(), dataSets.Item1, dataSets.Item2);
      Trace.WriteLine("Generation fitnesses: " + run.Generations.Select(x => x.Evaluated.Fitness).Join(", "));
      var bestMixtures = run.Generations.SelectMany(g => g.Mixtures).OrderByDescending(m => m.Evaluated.Fitness).Take(10).ToList();

      var testingSet = MakeTrainingSet(db, validationEnd, "12/12/2003");
      Lists.Repeat(bestMixtures.Count, i => {
        var predictor = new MixturePredictor(bestMixtures[i], testingSet);
        var testedFitness = Functions.ComputeFitness(predictor, testingSet, 20);
        Trace.WriteLine(string.Format("Tested fitness for best mixture #{0}: {1}", i, testedFitness));
      });
    }

    [Test]
    public void InitialPopulationTest()
    {
      var mongoDb = TestHelpers.GetCleanDatabase();
      var db = new Database(mongoDb);
      var protoRun = new ProtoRun(db, "InitialPopulationTest", 3, Initialization.MakeFastestProtoChromosome(), 10, 6, 4, 5, 0.05);
      var gen = Initialization.MakeInitialGeneration(MakeTrainingSet(db, "11/11/2001", "12/11/2001"),
                                                     new Run(protoRun, Initialization.MakeFastestProtoChromosome()),
                                                     new LocalParallelTrainer());
      foreach (var mixture in gen.Mixtures)
      {
        var ordered = mixture.Chromosomes.OrderBy(c => c.OrderInMixture).ToArray();
        var rnns = ordered.Take(6).ToArray();
        var rbfs = ordered.Skip(6).ToArray();
        rnns.Length.ShouldEqual(6);
        rbfs.Length.ShouldEqual(4);
        rnns.Select(c => c.NetworkType).Distinct().Single().ShouldEqual(NetworkType.Rnn);
        rbfs.Select(c => c.NetworkType).Distinct().Single().ShouldEqual(NetworkType.Rbf);
      }
    }

    [Test]
    public void MixtureCrossover()
    {
      var db = new Database(TestHelpers.GetCleanDatabase());
      var protoChrom = Initialization.MakeProtoChromosome();
      var protoRun = new ProtoRun(db, "MixtureCrossoverTest", -1, protoChrom, 2, 6, 4, 10, 0.05);
      var run = new Run(protoRun, protoChrom);
      var seed = MakeTrainingSet(db, "11/11/2001", "12/11/2001");
      var gen = Initialization.MakeInitialGeneration(seed, run, new LocalParallelTrainer());
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
    public void BoolMutation()
    {
      var db = new Database(TestHelpers.GetCleanDatabase());
      var protoChrom = Initialization.MakeProtoChromosome();
      var protoRun = new ProtoRun(db, "MixtureCrossoverTest", -1, protoChrom, 2, 6, 4, 10, 0.05);
      var run = new Run(protoRun, protoChrom);

      int trues = 0;
      int falses = 0;

      var chrom = Initialization.MakeRandomChromosome(NetworkType.Rnn, protoChrom, 0);
      Lists.Repeat(10000, _ => {
        chrom = Functions.MutateChromosome(chrom, run);
        if (chrom.UsePCA)
          trues++;
        else
          falses++;
      });

      Trace.WriteLine("Trues: " + trues);
      Trace.WriteLine("Falses: " + falses);
      ((double)trues / falses).ShouldBeGreaterThan(0.9).ShouldBeLessThan(1.1);
    }

    static DataSet MakeTrainingSet(Database db, string startDate, string endDate)
    {
      return DataPreprocessing.LoadTrainingSet(db, "DIA", DateTime.Parse(startDate, null, DateTimeStyles.AdjustToUniversal), DateTime.Parse(endDate, null, DateTimeStyles.AdjustToUniversal),
        Signals.NextClose);
    }

    static Tuple2<DataSet> MakeTrainingAndValidationSets(Database db, string startDate, string endDate)
    {
      return DataPreprocessing.LoadTrainingAndValidationSets(db, "DIA", DateTime.Parse(startDate, null, DateTimeStyles.AdjustToUniversal), DateTime.Parse(endDate, null, DateTimeStyles.AdjustToUniversal),
        0.20, Signals.NextClose);
    }

    //[Test]
    public void AddProtoRun()
    {
      var db = Database.GetProductionDatabase(new MongoHostInfo("mamail.co", "quqe", "g00gleflex", "versace"));
      new ProtoRun(db, "Dist1", 4, Initialization.MakeProtoChromosome(), 10, 6, 4, 4, 0.05);
    }

    [Test]
    public void ChromosomeCrossover()
    {
      var protoChrom = Initialization.MakeProtoChromosome();
      var a = Initialization.MakeRandomChromosome(NetworkType.Rnn, protoChrom, 0);
      var b = Initialization.MakeRandomChromosome(NetworkType.Rnn, protoChrom, 0);
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

      Trace.WriteLine("");
      Trace.WriteLine("a0: " + a0);
      Trace.WriteLine("a1: " + a1);
      Trace.WriteLine("b0: " + b0);
      Trace.WriteLine("b1: " + b1);

      a1.ShouldNotLookLike(a0);
      b1.ShouldNotLookLike(b0);

      Lists.Repeat(a1.Genes.Length, i => {
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