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
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;
using System.IO;
using System.Security.Cryptography;
using PCW;
using List = PCW.List;

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

      Environment.SetEnvironmentVariable("OPENBLAS_NUM_THREADS", "1");
      Trace.WriteLine("OPENBLAS_NUM_THREADS=" + Environment.GetEnvironmentVariable("OPENBLAS_NUM_THREADS"));

      var weightCount = RNN.GetWeightCount(layers, numInputs);
      var initialWeights = QuqeUtil.MakeRandomVector(weightCount, -1, 1);

      var vs = new List<Vec>();
      List.Repeat(5, i => {
        QuqeUtil.Random = new Random(42);
        Trace.WriteLine(string.Format("Training {0} ...", i));
        var trainResult = RNN.TrainSCG(layers, initialWeights, 1000, data.Inputs, data.Outputs);
        var ws = trainResult.RNNSpec.Weights;
        vs.Add(ws);
        Trace.WriteLine(string.Format("{0:D0} Initial: {1} Trained: {2}", i, Checksum(initialWeights), Checksum(ws)));
        Trace.WriteLine("");
      });
      Trace.WriteLine(string.Format("Worst precision difference: {0}", WorstDiff(vs)));
      WorstDiff(vs).ShouldEqual(0);
    }

    int WorstDiff(List<Vec> vs)
    {
      var worst = Int32.MaxValue;
      for (int p = 1; p <= 15; p++)
        for (int i = 0; i < vs.First().Count; i++)
          if (vs.Select(v => Math.Round(v[i], p)).Distinct().Count() > 1)
            return p;
      return 0;
    }

    [Test]
    public void RNNPredictsWell()
    {
      Func<DateTime, DateTime, PreprocessedData> getData = (start, end) =>
        Versace.GetPreprocessedValues(PreprocessingType.Enhanced, "DIA", start, end, true, Versace.GetIdealSignalFunc(PredictionType.NextClose));

      var trainingData = getData(DateTime.Parse("2004-01-01"), DateTime.Parse("2006-01-01"));
      var testData = getData(DateTime.Parse("2006-01-01"), DateTime.Parse("2006-06-01"));

      var numInputs = trainingData.Inputs.RowCount;
      var layers = new List<LayerSpec> {
        new LayerSpec { NodeCount = 8, IsRecurrent = true, ActivationType = ActivationType.LogisticSigmoid },
        new LayerSpec { NodeCount = 4, IsRecurrent = true, ActivationType = ActivationType.LogisticSigmoid },
        new LayerSpec { NodeCount = 1, IsRecurrent = false, ActivationType = ActivationType.Linear }
      };

      Environment.SetEnvironmentVariable("OPENBLAS_NUM_THREADS", "8");
      Trace.WriteLine("OPENBLAS_NUM_THREADS=" + Environment.GetEnvironmentVariable("OPENBLAS_NUM_THREADS"));

      QuqeUtil.Random = new Random(42);

      var weightCount = RNN.GetWeightCount(layers, numInputs);
      var initialWeights = QuqeUtil.MakeRandomVector(weightCount, -1, 1);
      var trainResult = RNN.TrainSCG(layers, initialWeights, 1000, trainingData.Inputs, trainingData.Outputs);
      //Trace.WriteLine("Max Cost: " + trainResult.CostHistory.Max());
      //Trace.WriteLine("Cost: " + trainResult.Cost);
      var rnnSpec = trainResult.RNNSpec;

      var truncSpec = new RNNSpec(rnnSpec.NumInputs, rnnSpec.Layers,
        new DenseVector(rnnSpec.Weights.Select(x => Math.Round(x, 2)).ToArray()));

      Trace.WriteLine("rnnSpec.Weights  : " + rnnSpec.Weights.Take(5).Join("  "));
      Trace.WriteLine("truncSpec.Weights checksum: " + Checksum(truncSpec.Weights));

      foreach (var spec in List.Create(rnnSpec, truncSpec))
      {
        var net = new RNN(spec);
        var trainingFitness = VMixture.ComputePredictorFitness(net, trainingData.Inputs, trainingData.Outputs);
        var testFitness = VMixture.ComputePredictorFitness(net, testData.Inputs, testData.Outputs);

        //Trace.WriteLine("Training fitness: " + trainingFitness);
        Trace.WriteLine("Test fitness: " + testFitness);
      }

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

    static string Checksum(Mat m)
    {
      var ms = new MemoryStream();
      var bw = new BinaryWriter(ms);
      foreach (var x in m.ToArray())
        bw.Write(x);
      using (var md5 = MD5.Create())
        return Convert.ToBase64String(md5.ComputeHash(ms.ToArray()));
    }
  }
}
