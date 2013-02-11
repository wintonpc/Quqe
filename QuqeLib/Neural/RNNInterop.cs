using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public static class RNNInterop
  {
    const int ACTIVATION_LOGSIG = 0;
    const int ACTIVATION_PURELIN = 1;
    struct QMLayerSpec
    {
      public QMLayerSpec(LayerSpec spec)
      {
        NodeCount = spec.NodeCount;
        IsRecurrent = spec.IsRecurrent;
        ActivationType = spec.ActivationType == Quqe.ActivationType.LogisticSigmoid ?
          ACTIVATION_LOGSIG : ACTIVATION_PURELIN;
      }

      int NodeCount;
      bool IsRecurrent;
      int ActivationType;
    };

    public class WeightEvalInfo
    {
      public Vec Output;
      public double Error;
      public Vec Gradient;
    }

    interface IContext
    {
      IntPtr Ptr { get; }
    }

    public class PropagationContext : ContextBase
    {
      internal PropagationContext(IntPtr ptr) : base(ptr) { }
      protected override void DestroyContext() { QMDestroyPropagationContext(Ptr); }
    }

    public class TrainingContext : ContextBase
    {
      public readonly int NumOutputs;
      internal TrainingContext(IntPtr ptr, int numOutputs) : base(ptr) { NumOutputs = numOutputs; }
      protected override void DestroyContext() { QMDestroyTrainingContext(Ptr); }
    }

    public abstract class ContextBase : IDisposable, IContext
    {
      readonly IntPtr _Ptr;
      public IntPtr Ptr { get { return _Ptr; } }

      protected ContextBase(IntPtr ptr)
      {
        _Ptr = ptr;
      }

      bool IsDiposed;
      public void Dispose()
      {
        if (IsDiposed) return;
        IsDiposed = true;
        DestroyContext();
      }

      protected abstract void DestroyContext();
    }

    public static int GetWeightCount(List<LayerSpec> layers, int numInputs)
    {
      return QMGetWeightCount(Structify(layers), layers.Count, numInputs);
    }

    public static TrainingContext CreateTrainingContext(List<LayerSpec> layers, Mat trainingData, Vec outputData)
    {
      var ptr = QMCreateTrainingContext(Structify(layers), layers.Count, trainingData.ToRowWiseArray(), outputData.ToArray(),
        trainingData.RowCount, trainingData.ColumnCount);
      return new TrainingContext(ptr, outputData.Count);
    }

    public static WeightEvalInfo EvaluateWeights(this TrainingContext trainingContext, Vec weights)
    {
      var weightArray = weights.ToArray();
      double error;
      double[] grad = new double[weightArray.Length];
      double[] output = new double[trainingContext.NumOutputs];
      QMEvaluateWeights(trainingContext.Ptr, weightArray, weightArray.Length, output, out error, grad);
      return new WeightEvalInfo {
        Output = new DenseVector(output),
        Error = error,
        Gradient = new DenseVector(grad)
      };
    }

    public static PropagationContext CreatePropagationContext(RNNSpec spec)
    {
      return new PropagationContext(QMCreatePropagationContext(Structify(spec.Layers), spec.Layers.Count,
        spec.NumInputs, spec.Weights.ToArray(), spec.Weights.Count));
    }

    public static double[] PropagateInput(PropagationContext context, double[] input, int numOutputs)
    {
      var outputs = new double[numOutputs];
      QMPropagateInput(context.Ptr, input, outputs);
      return outputs;
    }

    static QMLayerSpec[] Structify(IEnumerable<LayerSpec> layers)
    {
      return layers.Select(x => new QMLayerSpec(x)).ToArray();
    }

    [DllImport("QuqeMath.dll", EntryPoint = "GetWeightCount", CallingConvention = CallingConvention.Cdecl)]
    extern static int QMGetWeightCount(QMLayerSpec[] layerSpecs, int numLayers, int nInputs);

    [DllImport("QuqeMath.dll", EntryPoint = "CreateTrainingContext", CallingConvention = CallingConvention.Cdecl)]
    extern static IntPtr QMCreateTrainingContext(QMLayerSpec[] layerSpecs, int numLayers, double[] trainingData, double[] outputData,
      int nInputs, int nSamples);

    [DllImport("QuqeMath.dll", EntryPoint = "EvaluateWeights", CallingConvention = CallingConvention.Cdecl)]
    extern static void QMEvaluateWeights(IntPtr trainingContext, double[] weights, int nWeights, double[] output, out double error, double[] gradient);

    [DllImport("QuqeMath.dll", EntryPoint = "DestroyTrainingContext", CallingConvention = CallingConvention.Cdecl)]
    extern static void QMDestroyTrainingContext(IntPtr context);

    [DllImport("QuqeMath.dll", EntryPoint = "CreatePropagationContext", CallingConvention = CallingConvention.Cdecl)]
    extern static IntPtr QMCreatePropagationContext(QMLayerSpec[] layerSpecs, int numLayers, int nInputs, double[] weights, double nWeights);

    [DllImport("QuqeMath.dll", EntryPoint = "PropagateInput", CallingConvention = CallingConvention.Cdecl)]
    extern static void QMPropagateInput(IntPtr propagationContext, double[] input, double[] output);

    [DllImport("QuqeMath.dll", EntryPoint = "DestroyPropagationContext", CallingConvention = CallingConvention.Cdecl)]
    extern static void QMDestroyPropagationContext(IntPtr context);
  }
}
