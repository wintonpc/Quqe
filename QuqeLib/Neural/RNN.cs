using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using PCW;
using System.Xml.Linq;
using System.Threading.Tasks;

namespace Quqe
{
  public enum ActivationType { LogisticSigmoid, Linear }

  public class LayerSpec
  {
    public int NodeCount;
    public bool IsRecurrent;
    public ActivationType ActivationType;
  }

  public class RNNSpec
  {
    public readonly int NumInputs;
    public readonly List<LayerSpec> Layers;
    public readonly Vector<double> Weights;

    public RNNSpec(int numInputs, List<LayerSpec> layers, Vector<double> weights)
    {
      NumInputs = numInputs;
      Layers = layers;
      Weights = weights;
    }
  }

  public partial class RNN : IPredictor
  {
    public readonly RNNSpec Spec;
    readonly RNNInterop.PropagationContext PropagationContext;

    public RNN(RNNSpec spec)
    {
      Spec = spec;
      PropagationContext = RNNInterop.CreatePropagationContext(spec);
    }

    public void Dispose()
    {
      PropagationContext.Dispose();
    }

    public double Predict(Vector<double> input)
    {
      return Propagate(input).Single();
    }

    Vector<double> Propagate(Vector<double> input)
    {
      return new DenseVector(RNNInterop.PropagateInput(PropagationContext, input.ToArray(), Spec.Layers.Last().NodeCount));
    }

    public static int GetWeightCount(List<LayerSpec> layers, int numInputs)
    {
      return RNNInterop.GetWeightCount(layers, numInputs);
    }

    public static Vector<double> MakeRandomWeights(int size)
    {
      return Optimizer.RandomVector(size, -1, 1);
    }

    public IPredictor Reset()
    {
      return new RNN(Spec);
    }
  }
}
