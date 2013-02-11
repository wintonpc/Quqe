﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Diagnostics;
using YamlDotNet.RepresentationModel.Serialization;
using System.Xml.Serialization;
using System.Xml.Linq;
using System.Xml;
using System.Threading.Tasks;
using System.Runtime;
using System.Reflection;
using System.Threading;
using MathNet.Numerics.Statistics;
using System.Globalization;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

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
    static Random Random = new Random();
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

        int numTrained = 0;
        object trainLock = new object();
        Action trainedOne = () => {
          lock (trainLock)
          {
            numTrained++;
            Trace.WriteLine(string.Format("Epoch {0}, trained {1} / {2}", epoch, numTrained, Settings.ExpertsPerMixture * Settings.PopulationSize));
            if (numTrained % 10 == 0)
              GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
          }
        };

        bool parallelize = true;

        if (!parallelize)
        {
          foreach (var expert in population.Where(mixture => mixture.Fitness == 0)
            .SelectMany(mixture => mixture.Chromosomes).Select(m => m.Expert))
          {
            expert.Train();
            trainedOne();
          }
        }
        else
        {
          // optimize training order to keep the most load on the CPUs
          var allUntrainedExperts = population.Where(mixture => mixture.Fitness == 0).SelectMany(mixture => mixture.Chromosomes).ToList();
          var rnnExperts = allUntrainedExperts.Where(x => x.NetworkType == NetworkType.RNN).OrderByDescending(x => {
            var inputFactor = (x.UseComplementCoding ? 2 : 1) * (x.DatabaseType == DatabaseType.A ? DatabaseAInputLength[Settings.PreprocessingType] : TrainingInput.RowCount);
            return x.ElmanHidden1NodeCount * x.ElmanHidden2NodeCount * x.ElmanTrainingEpochs * inputFactor;
          }).ToList();
          var rbfExperts = allUntrainedExperts.Where(x => x.NetworkType == NetworkType.RBF).OrderByDescending(x => {
            var inputFactor = (x.UseComplementCoding ? 2 : 1) * (x.DatabaseType == DatabaseType.A ? DatabaseAInputLength[Settings.PreprocessingType] : TrainingInput.RowCount);
            return x.TrainingSizePct * inputFactor;
          }).ToList();
          var reordered = rnnExperts.Concat(rbfExperts).ToList();
          Parallel.Invoke(new ParallelOptions { MaxDegreeOfParallelism = 8 },
            reordered
            .Select(m => m.Expert).Select(expert => new Action(() => {
              expert.Train();
              //expert.TrainEx(rnnTrialCount: 3);
              trainedOne();
            })).ToArray());
        }

        foreach (var mixture in population.Where(mixture => mixture.Fitness == 0))
          mixture.ComputeAndSetFitness();
        var oldPopulation = population.ToList();
        var rankedPopulation = population.OrderByDescending(m => m.Fitness).ToList();
        var selected = rankedPopulation.Take(Settings.SelectionSize).Shuffle().ToList();
        population = rankedPopulation.Take(Settings.PopulationSize - Settings.SelectionSize).ToList();
        //population = new List<VMixture>();

        Debug.Assert(selected.Count % 2 == 0);
        for (int i = 0; i < selected.Count; i += 2)
          population.AddRange(selected[i].Crossover(selected[i + 1]));

        //for (int i = 0; i < Settings.PopulationSize / 2; i++)
        //{
        //  var a = selected.RandomItem();
        //  var b = selected.Except(List.Create(a)).RandomItem();
        //  population.AddRange(a.CrossoverAndMutate(b));
        //}

        //// mutate the very best just a little bit for some variation
        //for (int i = 0; i < SELECTION_SIZE / 2; i++)
        //  population[i] = population[i].Mutate(mutationRate: 0.5, dampingFactor: 4);
        //// mutate the rest entirely randomly
        //for (int i = SELECTION_SIZE / 2; i < population.Count; i++)
        //  population[i] = population[i].Mutate(dampingFactor: 0);

        population = population.Select(x => x.Mutate(dampingFactor: 0)).ToList();

        if ((epoch + 1) % 4 == 0)
        {
          foreach (var mixture in population.ToList())
          {
            var otherGood = oldPopulation.Except(List.Create(mixture)).SelectMany(mix => mix.Chromosomes.Where(m => !m.Expert.IsDegenerate)).ToList();
            foreach (var chrom in mixture.Chromosomes.ToList())
            {
              if (chrom.Expert.IsDegenerate)
              {
                mixture.Chromosomes.Remove(chrom);
                VChromosome otherGoodClone;
                if (!otherGood.Any())
                  otherGoodClone = new VMixture().Chromosomes.First();
                else
                  otherGoodClone = XSer.Read<VChromosome>(XSer.Write(otherGood.RandomItem()));
                mixture.Chromosomes.Add(otherGoodClone);
              }
            }
          }
        }

        var bestThisEpoch = rankedPopulation.First();
        if (bestMixture == null || bestThisEpoch.Fitness > bestMixture.Fitness)
          bestMixture = bestThisEpoch;
        var diversity = Diversity(oldPopulation);
        history.Add(new PopulationInfo { Fitness = bestThisEpoch.Fitness, Diversity = diversity });
        if (historyChanged != null)
          historyChanged(history);
        Trace.WriteLine(string.Format("Epoch {0} fitness:  {1:N1}%   (Best: {2:N1}%)   Diversity: {3:N4}", epoch, bestThisEpoch.Fitness * 100.0, bestMixture.Fitness * 100.0, diversity));
        var oldChromosomes = oldPopulation.SelectMany(m => m.Chromosomes).ToList();
        Trace.WriteLine(string.Format("Epoch {0} composition:   Elman {1:N1}%   RBF {2:N1}%", epoch,
          (double)oldChromosomes.Count(x => x.NetworkType == NetworkType.RNN) / oldChromosomes.Count * 100,
          (double)oldChromosomes.Count(x => x.NetworkType == NetworkType.RBF) / oldChromosomes.Count * 100));
        Trace.WriteLine(string.Format("Epoch {0} ended {1}", epoch, DateTime.Now));
        GC.Collect();
        Trace.WriteLine("===========================================================================");
      }
      var result = new VersaceResult(bestMixture, history, Versace.Settings);
      result.Save();
      return result;
    }

    static double Diversity(List<VMixture> population)
    {
      var d = new DenseMatrix(Settings.ExpertsPerMixture * population.First().Chromosomes.First().Genes.Count, Settings.PopulationSize);
      for (int m = 0; m < population.Count; m++)
        d.SetColumn(m, population[m].Chromosomes.SelectMany(mem => mem.Genes.Select(x => (x.GetDoubleValue() - x.GetDoubleMin()) / (x.GetDoubleMax() - x.GetDoubleMin()))).ToArray());
      return d.Rows().Sum(r => Statistics.Variance(r));
    }
  }
}