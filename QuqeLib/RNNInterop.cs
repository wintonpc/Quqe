using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;

namespace Quqe
{
  public partial class RNN
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

    class WeightEvalInfo
    {
      public Vector<double> Output;
      public double Error;
      public Vector<double> Gradient;
    }

    static WeightEvalInfo EvaluateWeightsFast(IntPtr context, double[] weights, int numOutputs)
    {
      double error;
      double[] grad = new double[weights.Length];
      double[] output = new double[numOutputs];
      QMEvaluateWeights(context, weights, weights.Length, output, out error, grad);
      return new WeightEvalInfo {
        Output = new DenseVector(output),
        Error = error,
        Gradient = new DenseVector(grad)
      };
    }

    [DllImport("QuqeMath.dll", EntryPoint = "CreateWeightContext", CallingConvention = CallingConvention.Cdecl)]
    extern static IntPtr QMCreateWeightContext(QMLayerSpec[] layerSpecs, int numLayers, double[] trainingData, double[] outputData,
      int nInputs, int nSamples);

    [DllImport("QuqeMath.dll", EntryPoint = "EvaluateWeights", CallingConvention = CallingConvention.Cdecl)]
    extern static void QMEvaluateWeights(IntPtr context, double[] weights, int nWeights, double[] output, out double error, double[] gradient);

    [DllImport("QuqeMath.dll", EntryPoint = "DestroyWeightContext", CallingConvention = CallingConvention.Cdecl)]
    extern static IntPtr QMDestroyWeightContext(IntPtr context);

    [DllImport("QuqeMath.dll", EntryPoint = "CreatePropagationContext", CallingConvention = CallingConvention.Cdecl)]
    extern static IntPtr QMCreatePropagationContext(QMLayerSpec[] layerSpecs, int numLayers, int nInputs, double[] weights, double nWeights);

    [DllImport("QuqeMath.dll", EntryPoint = "PropagateInput", CallingConvention = CallingConvention.Cdecl)]
    extern static void QMPropagateInput(IntPtr context, double[] input, double[] output);

    [DllImport("QuqeMath.dll", EntryPoint = "DestroPropagationContext", CallingConvention = CallingConvention.Cdecl)]
    extern static IntPtr QMDestroyPropagationContext(IntPtr context);
  }
}
