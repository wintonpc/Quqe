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
    readonly IntPtr PropagationContext;

    public RNN(RNNSpec spec)
    {
      Spec = spec;
      PropagationContext = QMCreatePropagationContext(LayerSpecsToQMLayerSpecs(spec.Layers), spec.Layers.Count, spec.NumInputs,
        spec.Weights.ToArray(), spec.Weights.Count);
    }

    bool IsDisposed;
    public void Dispose()
    {
      if (IsDisposed) return;
      IsDisposed = true;
      QMDestroyPropagationContext(PropagationContext);
    }

    public double Predict(Vector<double> input)
    {
      return Propagate(input)[0];
    }

    Vector<double> Propagate(Vector<double> input)
    {
      var numOutputs = Spec.Layers.Last().NodeCount;
      var outputs = new double[numOutputs];
      QMPropagateInput(PropagationContext, input.ToArray(), outputs);
      return new DenseVector(outputs);
    }

    static QMLayerSpec[] LayerSpecsToQMLayerSpecs(List<LayerSpec> layers)
    {
      return layers.Select(x => new QMLayerSpec(x)).ToArray();
    }
  }
}
