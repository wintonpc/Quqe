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
  public delegate Vector<double> GetRecurrentInputFunc(int layer);

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
      public Vector<double> d; // [node] (this node's error contribution, training-only)
      public bool IsRecurrent;
      public ActivationFunc ActivationFunction;
      public ActivationFunc ActivationFunctionPrime;
      public int NodeCount { get { return W.RowCount; } }
    }

    class Frame
    {
      public List<Layer> Layers;
    }

    int NumInputs;
    List<LayerSpec> LayerSpecs;
    List<Layer> Layers;
    const double TimeZeroRecurrentInputValue = 0.5;

    public static AnnealResult<Vector> TrainBPTT(RNN net, double rate, Matrix trainingData, Vector outputData)
    {
      var time = new List<Frame>();
      var weights = net.GetWeightVector();

      for (int t = 0; t < trainingData.ColumnCount; t++)
      {
        time.Add(new Frame { Layers = SpecsToLayers(net.NumInputs, net.LayerSpecs, true) });
        SetWeightVector(time[t].Layers, weights);

        Propagate(trainingData.Column(t), time[t].Layers, l => t == 0 ? MakeTimeZeroRecurrentInput(net.Layers[l].NodeCount)
          : time[t - 1].Layers[l].z);
      }

      int t_max = trainingData.ColumnCount - 1;
      for (int t = t_max; t >= 0; t--)
      {
        var outputLayer = time[t].Layers.Last();
        var out_d = outputLayer.d;
        var expected = outputData[t];
        for (int i = 0; i < out_d.Count; i++)
        {
          double err = (expected - outputLayer.z[i]);
          if (t < t_max)
          {
            var nextLayerInTime = time[t + 1].Layers.Last();
            err += nextLayerInTime.Wr.Column(i) * nextLayerInTime.d;
          }
          out_d[i] = err * outputLayer.ActivationFunctionPrime(outputLayer.a[i]);
        }
        for (int l = time[t].Layers.Count - 2; l >= 0; l--)
        {
          var layer = time[t].Layers[l];
          for (int i = 0; i < layer.NodeCount; i++)
          {
            var subsequentLayer = time[t].Layers[l+1];
            double err = (subsequentLayer.W.Column(i) * subsequentLayer.d);
            if (t < t_max)
            {
              var nextLayerInTime = time[t + 1].Layers.Last();
              err += nextLayerInTime.Wr.Column(i) * nextLayerInTime.d;
            }
            layer.d[i] = err * layer.ActivationFunctionPrime(layer.a[i]);
          }
        }
      }

      throw new NotImplementedException();
    }

    public RNN(int numInputs, List<LayerSpec> layerSpecs)
    {
      NumInputs = numInputs;
      LayerSpecs = layerSpecs;
      Layers = SpecsToLayers(NumInputs, LayerSpecs);
    }

    public void ResetState()
    {
      foreach (var l in Layers)
        l.z = MakeTimeZeroRecurrentInput(l.NodeCount);
    }

    public Vector<double> Propagate(Vector<double> input)
    {
      return Propagate(input, Layers, l => Layers[l].z); // recursive inputs are previous outputs (z)
    }

    static Vector<double> Propagate(Vector<double> input, List<Layer> layers, GetRecurrentInputFunc getRecurrentInput)
    {
      for (int l = 0; l < layers.Count; l++)
      {
        var layer = layers[l];
        PropagateLayer(input, layer, getRecurrentInput(l));
        input = layer.z;
      }
      return layers.Last().z;
    }

    static void PropagateLayer(Vector<double> input, Layer layer, Vector<double> recurrentInput)
    {
      layer.a = layer.W * input + layer.Bias;
      if (layer.IsRecurrent)
        layer.a += layer.Wr * recurrentInput;
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

    static List<Layer> SpecsToLayers(int numInputs, List<LayerSpec> specs, bool isTraining = false)
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
          layer.z = MakeTimeZeroRecurrentInput(s.NodeCount);
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

        if (isTraining)
          layer.d = new DenseVector(s.NodeCount);

        layers.Add(layer);
      }
      return layers;
    }

    static Vector<double> MakeTimeZeroRecurrentInput(int size)
    {
      return new DenseVector(size, TimeZeroRecurrentInputValue);
    }

    public Vector<double> GetWeightVector()
    {
      List<double> weights = new List<double>();
      WalkWeights(Layers, w => weights.Add(w), null);
      return new DenseVector(weights.ToArray());
    }

    public void SetWeightVector(Vector<double> weights)
    {
      SetWeightVector(Layers, weights);
    }

    static void SetWeightVector(List<Layer> layers, Vector<double> weights)
    {
      var i = 0;
      WalkWeights(layers, null, () => {
        var w = weights[i];
        i++;
        return w;
      });
    }

    static void WalkWeights(List<Layer> layers, Action<double> observe, Func<double> getNextValue)
    {
      for (int layer = 0; layer < layers.Count; layer++)
      {
        var l = layers[layer];
        WalkMatrix(l.W, observe, getNextValue);
        if (l.IsRecurrent)
          WalkMatrix(l.Wr, observe, getNextValue);
        WalkVector(layers[layer].Bias, observe, getNextValue);
      }
    }

    static void WalkVector(Vector<double> v, Action<double> observe, Func<double> getNextValue)
    {
      var len = v.Count;
      for (int i = 0; i < len; i++)
        if (getNextValue != null)
          v[i] = getNextValue();
        else
          observe(v[i]);
    }

    static void WalkMatrix(Matrix<double> m, Action<double> observe, Func<double> getNextValue)
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
