using PCW;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Quqe.NewVersace;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public static partial class Functions
  {
    public static Run Evolve(ProtoRun protoRun, IGenTrainer trainer, TrainingSeed seed)
    {
      var run = new Run(protoRun, protoRun.ProtoChromosome);
      var gen = Initialization.MakeInitialGeneration(seed, run, trainer);

      while (true)
      {
        var evaluated = Evaluate(gen, seed);
        if (gen.Order == protoRun.NumGenerations - 1)
          return run;

        gen = Train(trainer, seed, run, gen.Order + 1,
                    Mutate(run,
                           Combine(protoRun.MixturesPerGeneration,
                                   Select(protoRun.SelectionSize,
                                          evaluated))));
      }
    }

    static Generation Train(IGenTrainer trainer, TrainingSeed seed, Run run, int generationNum, MixtureInfo[] pop)
    {
      var gen = new Generation(run, generationNum);
      trainer.Train(seed, gen, pop.Select(mi => new MixtureInfo(new Mixture(gen, mi.Parents).Id, mi.Chromosomes)).ToArray(),
                    progress => Trace.WriteLine(string.Format("Generation {0}: Trained {1} of {2}",
                                                              generationNum, progress.Completed, progress.Total)));
      return gen;
    }

    static MixtureEval[] Evaluate(Generation gen, TrainingSeed seed)
    {
      var evaluatedMixtures = gen.Mixtures.Select(m => EvaluateMixture(m, seed)).ToArray();
      new GenEval(gen, evaluatedMixtures.Max(x => x.Fitness));
      return evaluatedMixtures;
    }

    static MixtureEval EvaluateMixture(Mixture m, TrainingSeed seed)
    {
      var fitness = MixtureFitness(m, seed);
      return new MixtureEval(m, fitness);
    }

    static double MixtureFitness(Mixture m, TrainingSeed tSeed)
    {
      var expertPredictors = m.Experts.Select(x => new ExpertPredictor(tSeed, x)).ToList();
      var predictions = List.Repeat(tSeed.Input.ColumnCount, t => MixturePredict(expertPredictors, t));
      var accuracy = predictions.Zip(tSeed.Output, (predicted, actual) => Math.Sign(predicted) == Math.Sign(actual) ? 1 : 0).Average();
      return accuracy;
    }

    class ExpertPredictor
    {
      public readonly ExpertSeed ExpertSeed;
      public readonly IPredictor Predictor;

      public ExpertPredictor(TrainingSeed tSeed, Expert expert)
      {
        var data = ExpertPreprocessing.PrepareData(tSeed, expert.Chromosome);
        ExpertSeed = new ExpertSeed(data.Item1, data.Item2, expert.Chromosome);
        Predictor = MakePredictor(expert);
      }
    }

    static double MixturePredict(List<ExpertPredictor> expertPredictors, int t)
    {
      //return Math.Sign(expertPredictors.Select(p => p.Predict(inputs)).Average());
      var outputs = expertPredictors.Select(ep => {
        var prediction = ep.Predictor.Predict(ep.ExpertSeed.Input.Column(t));
        return prediction;
      }).ToArray();
      return Math.Sign(outputs.Average());
    }

    static IPredictor MakePredictor(Expert expert)
    {
      if (expert is RnnTrainRec)
      {
        var trainRec = (RnnTrainRec)expert;
        return new RNN(trainRec.RnnSpec.ToRnnSpec());
      }
      else if (expert is RbfTrainRec)
      {
        var trainRec = (RbfTrainRec)expert;
        return new RBFNet(trainRec.Bases.Select(b => b.ToRadialBasis()), trainRec.OutputBias, trainRec.Spread, trainRec.IsDegenerate);
      }
      else
        throw new Exception("Unexpected expert type");
    }

    static MixtureEval[] Select(int selectionSize, IEnumerable<MixtureEval> ms)
    {
      return ms.OrderByDescending(x => x.Fitness).Take(selectionSize).ToArray();
    }

    internal static MixtureInfo[] Combine(int outputSize, IList<MixtureEval> ms)
    {
      return List.Repeat((int)Math.Ceiling(outputSize / 2.0), _ => CombineTwoMixtures(ms)).SelectMany(x => x).Take(outputSize).ToArray();
    }

    internal static Tuple2<MixtureInfo> CombineTwoMixtures(IList<MixtureEval> ms)
    {
      var parents = SelectTwoAccordingToQuality(ms, x => x.Fitness);

      Func<MixtureEval, Chromosome[]> chromosomesOf = me => me.Mixture.Experts.Select(x => x.Chromosome).ToArray();

      Func<IEnumerable<Chromosome>, MixtureInfo> chromosomesToMixture = chromosomes =>
                                                                        new MixtureInfo(parents.Select(p => p.Mixture), chromosomes);

      return CrossOver(chromosomesOf(parents.Item1), chromosomesOf(parents.Item2), CrossOverChromosomes, chromosomesToMixture);
    }

    internal static Tuple2<Chromosome> CrossOverChromosomes(Chromosome a, Chromosome b)
    {
      Debug.Assert(a.NetworkType == b.NetworkType);

      Func<Gene, Gene, Tuple2<Gene>> crossGenes = (x, y) => {
        Debug.Assert(x.Name == y.Name);
        return QuqeUtil.Random.Next(2) == 0 ? Tuple2.Create(x, y) : Tuple2.Create(y, x);
      };

      return CrossOver(a.Genes, b.Genes, crossGenes, genes => new Chromosome(a.NetworkType, genes));
    }

    static MixtureInfo[] Mutate(Run run, IEnumerable<MixtureInfo> ms)
    {
      return ms.Select(mi => new MixtureInfo(mi.Parents, MutateChromosomes(mi.Chromosomes, run))).ToArray();
    }

    static Chromosome[] MutateChromosomes(IEnumerable<Chromosome> chromosomes, Run run)
    {
      return chromosomes.Select(x => MutateChromosome(x, run)).ToArray();
    }

    static Chromosome MutateChromosome(Chromosome c, Run run)
    {
      return new Chromosome(c.NetworkType, c.Genes.Select(x => MutateGene(x, run)));
    }

    static Gene MutateGene(Gene g, Run run)
    {
      return new Gene(g.Name, QuqeUtil.WithProb(run.ProtoRun.MutationRate) ? RandomGeneValue(g.GetProto(run)) : g.Value);
    }

    public static double RandomGeneValue(ProtoGene gd)
    {
      return Quantize(RandomDouble(gd.MinValue, gd.MaxValue), gd.MinValue, gd.Granularity);
    }
  }
}