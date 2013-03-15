using NUnit.Framework;
using Quqe;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Machine.Specifications;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra.Double;
using QuqeViz;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;
using System.IO;
using System.Security.Cryptography;
using PCW;
using List = PCW.List;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace QuqeTest
{
  [TestFixture]
  public class RNNTests
  {
    [Test]
    public void RNNReproduceability()
    {
      Func<DateTime, DateTime, PreprocessedData> getData = (start, end) =>
        Versace.GetPreprocessedValues(PreprocessingType.Enhanced, "DIA", start, end, true, Versace.GetIdealSignalFunc(PredictionType.NextClose));

      var data = getData(DateTime.Parse("2004-01-01"), DateTime.Parse("2004-05-01"));

      var numInputs = data.Inputs.RowCount;
      var layers = new List<LayerSpec> {
        new LayerSpec { NodeCount = 8, IsRecurrent = true, ActivationType = ActivationType.LogisticSigmoid },
        new LayerSpec { NodeCount = 4, IsRecurrent = true, ActivationType = ActivationType.LogisticSigmoid },
        new LayerSpec { NodeCount = 1, IsRecurrent = false, ActivationType = ActivationType.Linear }
      };

      QuqeUtil.Random = new Random(42);

      var weightCount = RNN.GetWeightCount(layers, numInputs);
      var initialWeights = QuqeUtil.MakeRandomVector(weightCount, -1, 1);

      var checksums = new HashSet<string>();
      List.Repeat(5, i => {
        QuqeUtil.Random = new Random(42);
        var trainResult = RNN.TrainSCG(layers, initialWeights, 1000, data.Inputs, data.Outputs);
        var ws = trainResult.RNNSpec.Weights;
        checksums.Add(Checksum(trainResult.RNNSpec.Weights));
      });
      checksums.Count.ShouldEqual(1);
    }

    [Test]
    [STAThread]
    public void RNNPredictsWell()
    {
      Func<DateTime, DateTime, PreprocessedData> getData = (start, end) =>
        Versace.GetPreprocessedValues(PreprocessingType.Enhanced, "DIA", start, end, true, Versace.GetIdealSignalFunc(PredictionType.NextClose));

      var trainingData = getData(DateTime.Parse("2004-01-01"), DateTime.Parse("2006-01-01"));
      var testData = getData(DateTime.Parse("2006-01-01"), DateTime.Parse("2006-06-01"));

      var numInputs = trainingData.Inputs.RowCount;
      var layers = new List<LayerSpec> {
        new LayerSpec { NodeCount = 12, IsRecurrent = true, ActivationType = ActivationType.LogisticSigmoid },
        new LayerSpec { NodeCount = 5, IsRecurrent = true, ActivationType = ActivationType.LogisticSigmoid },
        new LayerSpec { NodeCount = 1, IsRecurrent = false, ActivationType = ActivationType.Linear }
      };

      QuqeUtil.Random = new Random(42);

      var weightCount = RNN.GetWeightCount(layers, numInputs);
      var initialWeights = QuqeUtil.MakeRandomVector(weightCount, -1, 1);
      var trainResult = RNN.TrainSCG(layers, initialWeights, 1000, trainingData.Inputs, trainingData.Outputs);
      var rnnSpec = trainResult.RNNSpec;

      var net = new RNN(rnnSpec);
      var trainingFitness = VMixture.ComputePredictorFitness(net, trainingData.Inputs, trainingData.Outputs);
      //var testFitness = VMixture.ComputePredictorFitness(net, testData.Inputs, testData.Outputs);

      var trainingFitnessAtEachEpoch = trainResult.WeightHistory.Select((w, i) =>
        VMixture.ComputePredictorFitness(new RNN(new RNNSpec(rnnSpec.NumInputs, rnnSpec.Layers, w)),
        trainingData.Inputs, trainingData.Outputs)).ToList();

      var testingFitnessAtEachEpoch = trainResult.WeightHistory.Select((w, i) =>
        VMixture.ComputePredictorFitness(new RNN(new RNNSpec(rnnSpec.NumInputs, rnnSpec.Layers, w)),
        testData.Inputs, testData.Outputs)).ToList();

      var plot = new EqPlotWindow();
      plot.Show();
      plot.ThePlot.DrawLineGraph(List.Create(
        new PlotDesc { ys = trainResult.CostHistory.Skip(1).ToList(), Color = Colors.Blue },
        new PlotDesc { ys = trainingFitnessAtEachEpoch.Select(x => -x).Skip(1).ToList(), Color = Colors.Green },
        new PlotDesc { ys = testingFitnessAtEachEpoch.Select(x => -x).Skip(1).ToList(), Color = Colors.HotPink }));

      Trace.WriteLine("Training fitness: " + trainingFitness);
      Trace.WriteLine("Best test fitness: " + testingFitnessAtEachEpoch.Max());

      //trainingFitness.ShouldEqual(0.875);
    }

    static string Checksum(Vec v)
    {
      var ms = new MemoryStream();
      var bw = new BinaryWriter(ms);
      foreach (var x in v)
        bw.Write(x);
      using (var md5 = MD5.Create())
        return Convert.ToBase64String(md5.ComputeHash(ms.ToArray()));
    }
  }
}
