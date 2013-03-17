using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Threading;
using MathNet.Numerics.Algorithms.LinearAlgebra.Mkl;
using MathNet.Numerics.Statistics;
using PCW;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using MathNet.Numerics.LinearAlgebra.Double;

namespace Quqe
{
  public class PopulationInfo
  {
    public double Fitness;
    public double Diversity;
  }

  public class PreprocessedData
  {
    public readonly DataSeries<Bar> PredictedSeries;
    public readonly List<DataSeries<Value>> AllInputSeries;
    public readonly Mat Input;
    public readonly Vec Output;
    public readonly int DatabaseAInputLength;

    public PreprocessedData(DataSeries<Bar> predictedSeries, List<DataSeries<Value>> allInputSeries, Mat input, Vec output, int databaseAInputLength)
    {
      PredictedSeries = predictedSeries;
      AllInputSeries = allInputSeries;
      Input = input;
      Output = output;
      DatabaseAInputLength = databaseAInputLength;
    }
  }

  public enum DatabaseType { A, B }

  public interface IPredictor : IDisposable
  {
    double Predict(Vec input);
    IPredictor Reset();
  }

  public static partial class Versace
  {
    static Versace()
    {
      MathNet.Numerics.Control.DisableParallelization = true;
    }

    public static List<string> GetTickers(string predictedSymbol)
    {
      return List.Create(predictedSymbol, "^IXIC", "^GSPC", "^DJI", "^DJT", "^DJU", "^DJA", "^N225", "^BVSP",
        "^GDAXI", "^FTSE", /*"^CJJ", "USDCHF"*/ "^TYX", "^TNX", "^FVX", "^IRX", /*"EUROD"*/ "^XAU");
    }

    public static void Train(VersaceContext context, Action<List<PopulationInfo>> historyChanged, Action<VersaceResult> whenDone)
    {
      SyncContext sync = SyncContext.Current;
      Thread t = new Thread(() => {
        if (context.Settings.TrainingMethod == TrainingMethod.Evolve)
        {
          var result = Evolve(context, historyChanged);
          sync.Post(() => whenDone(result));
        }
      });
      t.IsBackground = true;
      t.Start();
    }

    public static VersaceResult Evolve(VersaceContext context, Action<List<PopulationInfo>> historyChanged = null)
    {
      var settings = context.Settings;
      GCSettings.LatencyMode = GCLatencyMode.Batch;
      var history = new List<PopulationInfo>();
      var population = List.Repeat(settings.PopulationSize, n => VMixture.CreateRandom(context));
      VMixture bestMixture = null;
      for (int epoch = 0; epoch < settings.EpochCount; epoch++)
      {
        Trace.WriteLine(string.Format("Epoch {0} started {1}", epoch, DateTime.Now));

        // train
        var trainer = new ParallelTrainer();
        trainer.Train(population.SelectMany(mixture => mixture.AllExperts), numTrained => {
          Trace.WriteLine(string.Format("Epoch {0}, trained {1} / {2}", epoch, numTrained, settings.TotalExpertsPerMixture * settings.PopulationSize));
        });

        // compute fitness
        trainer.ComputeFitness(population);

        // select
        var oldPopulation = population.ToList();
        var rankedPopulation = population.OrderByDescending(m => m.Fitness).ToList();
        var selected = rankedPopulation.Take(settings.SelectionSize).Shuffle(QuqeUtil.Random).ToList();
        var newPopulation = rankedPopulation.Take(settings.PopulationSize - settings.SelectionSize).ToList();

        // crossover
        Debug.Assert(selected.Count % 2 == 0);
        for (int i = 0; i < selected.Count; i += 2)
          newPopulation.AddRange(selected[i].Crossover(selected[i + 1]));

        // mutate
        population = newPopulation.Select(x => x.Mutate()).ToList();

        // remember the best
        var bestThisEpoch = rankedPopulation.First();
        if (bestMixture == null || bestThisEpoch.Fitness > bestMixture.Fitness)
          bestMixture = bestThisEpoch;

        var diversity = Diversity(settings, oldPopulation);
        history.Add(new PopulationInfo { Fitness = bestThisEpoch.Fitness, Diversity = diversity });
        if (historyChanged != null)
          historyChanged(history);
        Trace.WriteLine(string.Format("Epoch {0} fitness:  {1:N1}%   (Best: {2:N1}%)   Diversity: {3:N4}", epoch, bestThisEpoch.Fitness * 100.0, bestMixture.Fitness * 100.0, diversity));
        Trace.WriteLine(string.Format("Epoch {0} ended {1}", epoch, DateTime.Now));
        Trace.WriteLine("===========================================================================");
      }
      var result = new VersaceResult(bestMixture, history, settings);
      result.Save();
      return result;
    }

    static double Diversity(VersaceSettings settings, List<VMixture> population)
    {
      var d = new DenseMatrix(settings.TotalExpertsPerMixture * population.First().AllExperts.First().Chromosome.Genes.Count, settings.PopulationSize);
      for (int m = 0; m < population.Count; m++)
      {
        var chromosomes = population[m].AllExperts.Select(x => x.Chromosome);
        Func<VGene, double> valuePercent = x => {
          var range = x.GetDoubleMax() - x.GetDoubleMin();
          return range == 0 ? 0 : (x.GetDoubleValue() - x.GetDoubleMin()) / range;
        };
        d.SetColumn(m, chromosomes.SelectMany(mem => mem.Genes.Select(valuePercent)).ToArray());
      }
      return d.Rows().Sum(r => r.Variance());
    }
  }
}
