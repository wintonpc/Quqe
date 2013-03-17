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
      var data = GetData("2004-01-01", "2004-05-01");

      var checksums = List.Repeat(5, i => {
        QuqeUtil.Random = new Random(42);
        var trainResult = Train(data, 8, 4, 1000);
        var ws = trainResult.RNNSpec.Weights;
        return Checksum(trainResult.RNNSpec.Weights);
      });
      checksums.Distinct().Count().ShouldEqual(1);
    }

    [Test]
    public void RNNPredictsWell()
    {
      QuqeUtil.Random = new Random(42);
      var trainingData = GetData("2004-01-01", "2004-07-01");
      var trainResult = Train(trainingData, 8, 4, 1000);
      var trainingFitness = VMixture.ComputePredictorFitness(new RNN(trainResult.RNNSpec), trainingData.Inputs, trainingData.Outputs);
      
      Trace.WriteLine("Training fitness: " + trainingFitness);
      trainingFitness.ShouldEqual(0.984);
    }

    static PreprocessedData GetData(string startDate, string endDate)
    {
      return Versace.GetPreprocessedValues(PreprocessingType.Enhanced, "DIA", DateTime.Parse(startDate), DateTime.Parse(endDate),
        true, Versace.GetIdealSignalFunc(PredictionType.NextClose));
    }

    static RnnTrainResult Train(PreprocessedData trainingData, int layer1NodeCount, int layer2NodeCount, int epochMax)
    {
      var numInputs = trainingData.Inputs.RowCount;
      var layers = new List<LayerSpec> {
        new LayerSpec { NodeCount = layer1NodeCount, IsRecurrent = true, ActivationType = ActivationType.LogisticSigmoid },
        new LayerSpec { NodeCount = layer2NodeCount, IsRecurrent = true, ActivationType = ActivationType.LogisticSigmoid },
        new LayerSpec { NodeCount = 1, IsRecurrent = false, ActivationType = ActivationType.Linear }
      };

      var weightCount = RNN.GetWeightCount(layers, numInputs);
      var initialWeights = QuqeUtil.MakeRandomVector(weightCount, -1, 1);
      return RNN.TrainSCG(layers, initialWeights, epochMax, trainingData.Inputs, trainingData.Outputs);
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
