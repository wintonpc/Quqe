using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra.Double;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;
using System.Diagnostics;

namespace Quqe
{
  public enum ActivationType { LogisticSigmoid, Linear }

  public class LayerSpec
  {
    public readonly int NodeCount;
    public readonly bool IsRecurrent;
    public readonly ActivationType ActivationType;

    public LayerSpec(int nodeCount, bool isRecurrent, ActivationType activationType)
    {
      NodeCount = nodeCount;
      IsRecurrent = isRecurrent;
      ActivationType = activationType;
    }
  }

  public class RNNSpec
  {
    public readonly int NumInputs;
    public readonly List<LayerSpec> Layers;
    public readonly Vec Weights;

    public RNNSpec(int numInputs, IEnumerable<LayerSpec> layers, Vec weights)
    {
      NumInputs = numInputs;
      Layers = layers.ToList();
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
      Debug.Assert(!spec.Weights.Any(x => double.IsNaN(x)));
      PropagationContext = RNNInterop.CreatePropagationContext(spec);
    }

    public void Dispose()
    {
      PropagationContext.Dispose();
    }

    public double Predict(Vec input)
    {
      return Propagate(input).Single();
    }

    Vec Propagate(Vec input)
    {
      Debug.Assert(!input.Any(x => double.IsNaN(x)));
      var output = new DenseVector(RNNInterop.PropagateInput(PropagationContext, input.ToArray(), Spec.Layers.Last().NodeCount));
      Debug.Assert(!output.Any(x => double.IsNaN(x)));
      return output;
    }

    public static int GetWeightCount(List<LayerSpec> layers, int numInputs)
    {
      return RNNInterop.GetWeightCount(layers, numInputs);
    }

    public static Vec MakeRandomWeights(int size)
    {
      return QuqeUtil.MakeRandomVector(size, -1, 1);
    }
  }
}
