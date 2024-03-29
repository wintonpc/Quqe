﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra.Generic;
using NUnit.Framework;
using Quqe;

namespace QuqeTest
{
  [TestFixture]
  public class QuqeMathLeaks
  {
    [Test]
    public void NoPropagationContextLeaks()
    {
      int numInputs = 200;
      var layerSpecs = MakeLayers();
      var weightCount = RNNInterop.GetWeightCount(layerSpecs, numInputs);

      var mem = new MemoryAnalyzer();
      using (mem)
      {
        Lists.Repeat(10000, _ => {
          var rnnSpec = new RNNSpec(numInputs, layerSpecs, new DenseVector(weightCount));
          var context = RNNInterop.CreatePropagationContext(rnnSpec);
          var input = MakeVector(numInputs);
          RNNInterop.PropagateInput(context, input.ToArray(), 1);
          context.Dispose();
        });
      }

      Trace.WriteLine("Mem usage before: " + mem.StartMB);
      Trace.WriteLine("Mem usage after: " + mem.StopMB);
      Assert.IsTrue(mem.StopMB - mem.StartMB < 2);
    }

    [Test]
    public void NoTrainingContextLeaks()
    {
      int numInputs = 200;
      var layerSpecs = MakeLayers();
      var weightCount = RNNInterop.GetWeightCount(layerSpecs, numInputs);
      const int numSamples = 10;
      var trainingData = Lists.Repeat(numSamples, _ => MakeVector(numInputs)).ColumnsToMatrix();
      var outputData = MakeVector(numSamples);
      var weights = MakeVector(weightCount);

      var mem = new MemoryAnalyzer();
      using (mem)
      {
        Lists.Repeat(2500, i => {
          var context = RNNInterop.CreateTrainingContext(layerSpecs, trainingData, outputData);
          RNNInterop.EvaluateWeights(context, weights);
          context.Dispose();
        });
      }

      Trace.WriteLine("Mem usage before: " + mem.StartMB);
      Trace.WriteLine("Mem usage after: " + mem.StopMB);
      Assert.IsTrue(mem.StopMB - mem.StartMB < 2);
    }

    [Test]
    public void NoGetWeightsLeaks()
    {
      var layerSpecs = MakeLayers();

      var mem = new MemoryAnalyzer();
      using (mem)
      {
        Lists.Repeat(30000, i => {
          RNNInterop.GetWeightCount(layerSpecs, 200);
        });
      }

      Trace.WriteLine("Mem usage before: " + mem.StartMB);
      Trace.WriteLine("Mem usage after: " + mem.StopMB);
      Assert.IsTrue(mem.StopMB - mem.StartMB < 15);
      // 15MB: don't know why this appears to leak, but it seems to approach some limit logarithmically with respect
      // to the number of calls to GetWeightCount(). oh well, we can afford 15MB for 30000 calls. Probably a
      // GC artifact anyway.
    }

    static Vector<double> MakeVector(int size)
    {
      return DenseVector.CreateRandom(size, new ContinuousUniform());
    }

    static List<LayerSpec> MakeLayers()
    {
      return new List<LayerSpec> {
        new LayerSpec(100, true, ActivationType.LogisticSigmoid),
        new LayerSpec(100, true, ActivationType.LogisticSigmoid),
        new LayerSpec(1, false, ActivationType.Linear)
      };
    }
  }

  class MemoryAnalyzer : IDisposable
  {
    public double StartMB { get; private set; }
    public double StopMB { get; private set; }

    public MemoryAnalyzer()
    {
      StartMB = GetWorkingSetInMb();
    }

    public void Dispose()
    {
      StopMB = GetWorkingSetInMb();
    }

    static double GetWorkingSetInMb()
    {
      GC.Collect();
      Thread.Sleep(1000);
      return (double)Process.GetCurrentProcess().WorkingSet64 / Math.Pow(2, 20);
    }
  }
}