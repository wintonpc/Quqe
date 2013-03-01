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
    public DataSeries<Bar> Predicted;
    public Mat Inputs;
    public Vec Outputs;
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
      MathNet.Numerics.Control.LinearAlgebraProvider = new MklLinearAlgebraProvider();
    }

    public static Dictionary<PreprocessingType, int> DatabaseAInputLength = new Dictionary<PreprocessingType, int>();
    static VersaceSettings _Settings = new VersaceSettings();
    public static VersaceSettings Settings
    {
      get { return _Settings; }
      set
      {
        if (value == null)
        {
          TrainingInput = ValidationInput = TestingInput = null;
          TrainingOutput = ValidationOutput = TestingOutput = null;
        }
        else
        {
          _Settings = value;
          LoadPreprocessedValues();
        }
      }
    }

    public static Mat TrainingInput { get; private set; }
    public static Mat ValidationInput { get; private set; }
    public static Mat TestingInput { get; private set; }
    public static Vec TrainingOutput { get; private set; }
    public static Vec ValidationOutput { get; private set; }
    public static Vec TestingOutput { get; private set; }

    public static List<string> GetTickers(string predictedSymbol)
    {
      return List.Create(predictedSymbol, "^IXIC", "^GSPC", "^DJI", "^DJT", "^DJU", "^DJA", "^N225", "^BVSP",
        "^GDAX", "^FTSE", /*"^CJJ", "USDCHF"*/ "^TYX", "^TNX", "^FVX", "^IRX", /*"EUROD"*/ "^XAU");
    }

    public static void Train(Action<List<PopulationInfo>> historyChanged, Action<VersaceResult> whenDone)
    {
      SyncContext sync = SyncContext.Current;
      Thread t = new Thread(() => {
        if (Settings.TrainingMethod == TrainingMethod.Evolve)
        {
          var result = Evolve(historyChanged);
          sync.Post(() => whenDone(result));
        }
      });
      t.IsBackground = true;
      t.Start();
    }

    public static VersaceResult Evolve(Action<List<PopulationInfo>> historyChanged = null)
    {
      GCSettings.LatencyMode = GCLatencyMode.Batch;
      var history = new List<PopulationInfo>();
      var population = List.Repeat(Settings.PopulationSize, n => VMixture.CreateRandom());
      VMixture bestMixture = null;
      for (int epoch = 0; epoch < Settings.EpochCount; epoch++)
      {
        Trace.WriteLine(string.Format("Epoch {0} started {1}", epoch, DateTime.Now));

        // train
        var trainer = new ParallelTrainer();
        trainer.Train(population.SelectMany(mixture => mixture.AllExperts), numTrained => {
          Trace.WriteLine(string.Format("Epoch {0}, trained {1} / {2}", epoch, numTrained, Settings.TotalExpertsPerMixture * Settings.PopulationSize));
        });

        // compute fitness
        trainer.ComputeFitness(population);

        // select
        var oldPopulation = population.ToList();
        var rankedPopulation = population.OrderByDescending(m => m.Fitness).ToList();
        var selected = rankedPopulation.Take(Settings.SelectionSize).Shuffle().ToList();
        var newPopulation = rankedPopulation.Take(Settings.PopulationSize - Settings.SelectionSize).ToList();

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

        var diversity = Diversity(oldPopulation);
        history.Add(new PopulationInfo { Fitness = bestThisEpoch.Fitness, Diversity = diversity });
        if (historyChanged != null)
          historyChanged(history);
        Trace.WriteLine(string.Format("Epoch {0} fitness:  {1:N1}%   (Best: {2:N1}%)   Diversity: {3:N4}", epoch, bestThisEpoch.Fitness * 100.0, bestMixture.Fitness * 100.0, diversity));
        Trace.WriteLine(string.Format("Epoch {0} ended {1}", epoch, DateTime.Now));
        Trace.WriteLine("===========================================================================");
      }
      var result = new VersaceResult(bestMixture, history, Versace.Settings);
      result.Save();
      return result;
    }

    static double Diversity(List<VMixture> population)
    {
      var d = new DenseMatrix(Settings.TotalExpertsPerMixture * population.First().AllExperts.First().Chromosome.Genes.Count, Settings.PopulationSize);
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
