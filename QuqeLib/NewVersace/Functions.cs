using PCW;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public static partial class Functions
  {
    public static Run Evolve(ProtoRun protoRun, IGenTrainer trainer, TrainingSeed seed)
    {
      var run = new Run(protoRun.Database, protoRun.ProtoChromosome);
      var initialGen = Initialization.MakeInitialGeneration(seed, run, protoRun, trainer);

      List.Iterate(protoRun.NumGenerations, initialGen, (i, gen) =>
                                                        Train(trainer, seed, run, i, Mutate(Combine(protoRun.MixturesPerGeneration, Select(protoRun.SelectionSize, Evaluate(gen.Mixtures, seed))))));
      return run;
    }

    static Generation Train(IGenTrainer trainer, TrainingSeed seed, Run run, int generationNum, MixtureInfo[] pop)
    {
      var gen = new Generation(run, generationNum);
      trainer.Train(seed, gen, pop.Select(mi => new MixtureInfo(new Mixture(gen, mi.Parents).Id, mi.Chromosomes)),
                    progress => Trace.WriteLine(string.Format("Generation {0}: Trained {1} of {2}",
                                                              generationNum, progress.Completed, progress.Total)));
      return gen;
    }

    static MixtureEval[] Evaluate(Mixture[] mixtures, TrainingSeed seed)
    {
      return mixtures.Select(m => EvaluateMixture(m, seed)).ToArray();
    }

    static MixtureEval EvaluateMixture(Mixture m, TrainingSeed seed)
    {
      var fitness = MixtureFitness(m, seed);
      return new MixtureEval(m, fitness);
    }

    static double MixtureFitness(Mixture m, TrainingSeed seed)
    {
      var predictions = seed.Input.Columns().Select(inputs => MixturePredict(m, inputs));
      var accuracy = predictions.Zip(seed.Output, (predicted, actual) => Math.Sign(predicted) == Math.Sign(actual) ? 1 : 0).Average();
      return accuracy;
    }

    static double MixturePredict(Mixture m, Vec inputs)
    {
      return Math.Sign(m.Experts.Average(expert => MakePredictor(expert).Predict(inputs)));
    }

    static IPredictor MakePredictor(Expert expert)
    {
      if (expert is RnnTrainRec)
      {
        var trainRec = (RnnTrainRec)expert;
        return new RNN(trainRec.RnnSpec.ToRNNSpec());
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
      return List.Repeat((int)Math.Ceiling(outputSize/2.0), _ => CombineTwoMixtures(ms)).SelectMany(x => x).Take(outputSize).ToArray();
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

    static MixtureInfo[] Mutate(IEnumerable<MixtureInfo> ms)
    {
      throw new NotImplementedException();
    }

    public static double RandomGeneValue(ProtoGene gd)
    {
      return Quantize(RandomDouble(gd.MinValue, gd.MaxValue), gd.MinValue, gd.Granularity);
    }
  }
}