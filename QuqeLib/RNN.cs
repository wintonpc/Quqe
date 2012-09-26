using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;

namespace Quqe
{
  public enum ActivationType { LogisticSigmoid, Linear }
  public delegate double ActivationFunc(double a);

  public class LayerSpec
  {
    public int NodeCount;
    public bool IsRecurrent;
    public ActivationType ActivationType;
  }

  public class RNN
  {
    static RNN() { MathNet.Numerics.Control.DisableParallelization = true; }

    class Layer
    {
      public Matrix<double> W; // [node, input] (input weights)
      public Matrix<double> Wr; // [node, node] (recurrent weights)
      public Vector<double> Bias; // [node] (per-node bias)
      public Vector<double> a; // [node] (summed input)
      public Vector<double> z; // [node] (node output)
      public bool IsRecurrent;
      public ActivationFunc ActivationFunction;
      public ActivationFunc ActivationFunctionPrime;
      public int NodeCount { get { return W.RowCount; } }
    }

    int NumInputs;
    List<LayerSpec> LayerSpecs;
    List<Layer> Layers;
    const double TimeZeroRecurrentInputValue = 0.5;

    public RNN(int numInputs, List<LayerSpec> layerSpecs)
    {
      NumInputs = numInputs;
      LayerSpecs = layerSpecs;
      Layers = SpecsToLayers(NumInputs, LayerSpecs);
    }

    public void ResetState()
    {
      foreach (var l in Layers)
        l.z = GetTimeZeroRecurrentInput(l.NodeCount);
    }

    public Vector<double> Propagate(Vector<double> input)
    {
      foreach (var layer in Layers)
      {
        PropagateLayer(input, layer);
        input = layer.z;
      }
      return Layers.Last().z;
    }

    static void PropagateLayer(Vector<double> input, Layer layer)
    {
      layer.a = layer.W * input + layer.Bias;
      if (layer.IsRecurrent)
        layer.a += layer.Wr * layer.z; // recursive inputs (a) are previous outputs (z)
      layer.z = ApplyActivationFunction(layer.a, layer.ActivationFunction);
    }

    static Vector<double> ApplyActivationFunction(Vector<double> a, ActivationFunc f)
    {
      Vector<double> z = new DenseVector(a.Count);
      var len = a.Count;
      for (int i = 0; i < len; i++)
        z[i] = f(a[i]);
      return z;
    }

    static List<Layer> SpecsToLayers(int numInputs, List<LayerSpec> specs)
    {
      var layers = new List<Layer>();
      foreach (var s in specs)
      {
        var layer = new Layer {
          W = new DenseMatrix(s.NodeCount, layers.Any() ? layers.Last().z.Count : numInputs),
          Bias = new DenseVector(s.NodeCount),
          IsRecurrent = s.IsRecurrent
        };

        if (s.IsRecurrent)
        {
          layer.Wr = new DenseMatrix(s.NodeCount, s.NodeCount);
          layer.z = GetTimeZeroRecurrentInput(s.NodeCount);
        }

        if (s.ActivationType == ActivationType.LogisticSigmoid)
        {
          layer.ActivationFunction = LogisticSigmoid;
          layer.ActivationFunctionPrime = LogisticSigmoidPrime;
        }
        else if (s.ActivationType == ActivationType.Linear)
        {
          layer.ActivationFunction = Linear;
          layer.ActivationFunctionPrime = LinearPrime;
        }
        else
          throw new Exception("Unexpected ActivationType: " + s.ActivationType);

        layers.Add(layer);
      }
      return layers;
    }

    static Vector<double> GetTimeZeroRecurrentInput(int size)
    {
      return new DenseVector(size, TimeZeroRecurrentInputValue);
    }

    public Vector<double> GetWeightVector()
    {
      List<double> weights = new List<double>();
      WalkWeights(w => weights.Add(w), null);
      return new DenseVector(weights.ToArray());
    }

    public void SetWeightVector(Vector<double> weights)
    {
      var i = 0;
      WalkWeights(null, () => {
        var w = weights[i];
        i++;
        return w;
      });
    }

    void WalkWeights(Action<double> observe, Func<double> getNextValue)
    {
      for (int layer = 0; layer < Layers.Count; layer++)
      {
        var l = Layers[layer];
        WalkMatrix(l.W, observe, getNextValue);
        if (l.IsRecurrent)
          WalkMatrix(l.Wr, observe, getNextValue);
        WalkVector(Layers[layer].Bias, observe, getNextValue);
      }
    }

    void WalkVector(Vector<double> v, Action<double> observe, Func<double> getNextValue)
    {
      var len = v.Count;
      for (int i = 0; i < len; i++)
        if (getNextValue != null)
          v[i] = getNextValue();
        else
          observe(v[i]);
    }

    void WalkMatrix(Matrix<double> m, Action<double> observe, Func<double> getNextValue)
    {
      var nRows = m.RowCount;
      var nCols = m.ColumnCount;
      for (int i = 0; i < nRows; i++)
        for (int j = 0; j < nCols; j++)
          if (getNextValue != null)
            m[i, j] = getNextValue();
          else
            observe(m[i, j]);
    }

    static double Linear(double x)
    {
      return x;
    }

    static double LinearPrime(double x)
    {
      return 1;
    }

    static double LogisticSigmoid(double x)
    {
      return 1 / (1 + Math.Exp(-x));
    }

    static double LogisticSigmoidPrime(double x)
    {
      var v = LogisticSigmoid(x);
      return v * (1 - v);
    }

    public static AnnealResult<Vector> TrainSA(RNN net, Matrix trainingData, Vector outputData)
    {
      var result = Optimizer.Anneal(net.GetWeightVector().Count, 1, w => {
        net.ResetState();
        net.SetWeightVector(w);
        int correctCount = 0;
        double errorSum = 0;
        for (int i = 0; i < trainingData.ColumnCount; i++)
        {
          var output = net.Propagate(trainingData.Column(i))[0];
          errorSum += Math.Pow(output - outputData[i], 2);
          if (Math.Sign(output) == Math.Sign(outputData[i]))
            correctCount++;
        }
        //return (double)correctCount / trainingData.ColumnCount;
        return errorSum / trainingData.ColumnCount;
      });

      net.ResetState();
      net.SetWeightVector(result.Params);
      return result;
    }
  }
}
