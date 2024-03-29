﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Machine.Specifications;
using NUnit.Framework;
using Quqe;
using Quqe.NewVersace;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;

namespace QuqeTest
{
  [TestFixture]
  public class RNNTests
  {
    [Test]
    public void RNNTrainingIsReproducible()
    {
      var data = NNTestUtils.GetData("2004-01-01", "2004-05-01");

      var checksums = Lists.Repeat(5, i => {
        QuqeUtil.Random = new Random(42);
        var trainResult = Train(data, 8, 4, 1000);
        return NNTestUtils.Checksum(trainResult.RNNSpec.Weights);
      });
      checksums.Distinct().Count().ShouldEqual(1);
    }

    [Test]
    public void RNNPredictsWell()
    {
      QuqeUtil.Random = new Random(42);
      var data = NNTestUtils.GetData("2004-01-01", "2004-07-01");
      var trainResult = Train(data, 8, 4, 1000);
      var trainingFitness = Functions.ComputeFitness(new PredictorWithInputs(new RNN(trainResult.RNNSpec), data.Input), data);
      
      Trace.WriteLine("Training fitness: " + trainingFitness);
      trainingFitness.ShouldEqual(0.968);
    }

    [Test]
    public void RNNTrainsQuickly()
    {
      var sw = new Stopwatch();
      sw.Start();
      var trainingData = NNTestUtils.GetData("2004-01-01", "2005-01-01");
      Train(trainingData, 32, 16, 1000);
      sw.Stop();
      sw.ElapsedMilliseconds.ShouldBeGreaterThan(2000).ShouldBeLessThan(17000);
    }

    static RnnTrainResult Train(DataSet data, int layer1NodeCount, int layer2NodeCount, int epochMax)
    {
      var numInputs = data.Input.RowCount;
      var layers = new List<LayerSpec> {
        new LayerSpec(layer1NodeCount, true, ActivationType.LogisticSigmoid),
        new LayerSpec(layer2NodeCount, true, ActivationType.LogisticSigmoid),
        new LayerSpec(1, false, ActivationType.Linear)
      };

      var weightCount = RNN.GetWeightCount(layers, numInputs);
      var initialWeights = QuqeUtil.MakeRandomVector(weightCount, -1, 1);
      return RNN.TrainSCG(layers, initialWeights, epochMax, data.Input, data.Output);
    }
  }
}
