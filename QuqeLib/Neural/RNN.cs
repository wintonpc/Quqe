using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra.Double;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

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
    public readonly Vec Weights;

    public RNNSpec(int numInputs, List<LayerSpec> layers, Vec weights)
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

    public double Predict(Vec input)
    {
      return Propagate(input).Single();
    }

    Vec Propagate(Vec input)
    {
      return new DenseVector(RNNInterop.PropagateInput(PropagationContext, input.ToArray(), Spec.Layers.Last().NodeCount));
    }

    public static int GetWeightCount(List<LayerSpec> layers, int numInputs)
    {
      return RNNInterop.GetWeightCount(layers, numInputs);
    }

    public static Vec MakeRandomWeights(int size)
    {
      return QuqeUtil.MakeRandomVector(size, -1, 1);
    }

    public IPredictor Reset()
    {
      return new RNN(Spec);
    }
  }
}
