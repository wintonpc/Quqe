using NUnit.Framework;
using Quqe;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuqeTest
{
  [TestFixture]
  public class RNNTests
  {
    [Test]
    public void RNNPredictsWell()
    {
      Func<DateTime, DateTime, PreprocessedData> getData = (start, end) =>
        Versace.GetPreprocessedValues(PreprocessingType.Enhanced, "DIA", start, end, true, Versace.GetIdealSignalFunc(PredictionType.NextClose));

      var trainingData = getData(DateTime.Parse("2004-01-01"), DateTime.Parse("2005-02-01"));
      var testData = getData(DateTime.Parse("2005-02-01"), DateTime.Parse("2005-05-01"));

      var numInputs = trainingData.Inputs.RowCount;
      var layers = new List<LayerSpec> {
        new LayerSpec { NodeCount = 6, IsRecurrent = true, ActivationType = ActivationType.LogisticSigmoid },
        new LayerSpec { NodeCount = 3, IsRecurrent = true, ActivationType = ActivationType.LogisticSigmoid },
        new LayerSpec { NodeCount = 1, IsRecurrent = false, ActivationType = ActivationType.Linear }
      };

      QuqeUtil.Random = new Random(42);

      var weightCount = RNN.GetWeightCount(layers, numInputs);
      var trainResult = RNN.TrainSCG(layers, QuqeUtil.MakeRandomVector(weightCount, -1, 1), 100, trainingData.Inputs, trainingData.Outputs);
      Trace.WriteLine("Max Cost: " + trainResult.CostHistory.Max());
      Trace.WriteLine("Cost: " + trainResult.Cost);
      var rnnSpec = trainResult.RNNSpec;

      var net = new RNN(rnnSpec);
      var trainingFitness = VMixture.ComputePredictorFitness(net, trainingData.Inputs, trainingData.Outputs);
      var testFitness = VMixture.ComputePredictorFitness(net, testData.Inputs, testData.Outputs);

      Trace.WriteLine("Training fitness: " + trainingFitness);
      Trace.WriteLine("Test fitness: " + testFitness);
    }
  }
}
