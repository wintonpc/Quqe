﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Diagnostics;

namespace Quqe
{
  public class ElmanNet : IPredictor
  {
    double[, ,] Weights; // Weights[layer, node, input]
    double[,] Registers; // Registers[layer, node]
    double[,] Biases; // Biases[layer, node]
    int NumInputs;
    int[] NodeCounts; // NodeCounts[layer]
    int NumLayers;
    public ElmanNet(int numInputs, List<int> hiddenNodeCounts, int numOutputs)
    {
      var numLayers = hiddenNodeCounts.Count + 1;
      var maxNodesPerLayer = hiddenNodeCounts.Max();
      var maxInputs = Math.Max(numInputs, maxNodesPerLayer);
      Weights = new double[numLayers, maxNodesPerLayer, maxInputs + maxNodesPerLayer]; // +maxNodesPerLayer for registers
      Registers = new double[numLayers - 1, maxNodesPerLayer]; // none for output layer
      Biases = new double[numLayers - 1, maxNodesPerLayer]; // none for output layer
      NumInputs = numInputs;
      NodeCounts = hiddenNodeCounts.Concat(List.Create(numOutputs)).ToArray();
      NumLayers = NodeCounts.Length;
      Reset();
    }

    double IPredictor.Predict(double[] input) { return Propagate(input); }

    public void Reset()
    {
      for (int layer = 0; layer < NumLayers - 1; layer++)
        for (int node = 0; node < Registers.GetLength(1); node++)
          Registers[layer, node] = 0.5;
    }

    public double Propagate(double[] inputs)
    {
      if (inputs.Length != NumInputs)
        throw new Exception(string.Format("Got {0} inputs but expected {1}", inputs.Length, NumInputs));
      if (NodeCounts.Last() > 1)
        throw new Exception("Bad assumption: There is only one output node. There are actually " + NodeCounts.Last());
      return Propagate(inputs, 0)[0];
    }

    public double[] Propagate(double[] inputs, int layer)
    {
      if (layer >= NumLayers)
        return inputs;
      var numNodes = NodeCounts[layer];
      var outputs = new double[numNodes];
      for (int node=0; node<numNodes; node++)
      {
        bool isOutputLayer = layer == NumLayers - 1;
        var inputsPlusRegisters = isOutputLayer ? inputs : AppendRegisters(inputs, layer, numNodes);
        var activation = CalculateActivation(inputsPlusRegisters, layer, node, isOutputLayer);
        var output = isOutputLayer ? activation : LogisticSigmoid(activation);
        if (!isOutputLayer)
          Registers[layer, node] = output;
        outputs[node] = output;
      }
      return Propagate(outputs, layer + 1);
    }

    double CalculateActivation(double[] inputsPlusRegisters, int layer, int node, bool isOutputLayer)
    {
      double result = 0;
      var inputLen = inputsPlusRegisters.Length;
      for (int i = 0; i < inputLen; i++)
        result += Weights[layer, node, i] * inputsPlusRegisters[i];
      return result - (isOutputLayer ? 0 : Biases[layer, node]);
    }

    double[] AppendRegisters(double[] inputs, int layer, int numNodes)
    {
      var inputsLen = inputs.Length;
      var result = new double[inputsLen + numNodes];
      Array.Copy(inputs, 0, result, 0, inputsLen);
      for (int nd = 0; nd < numNodes; nd++)
        result[inputsLen + nd] = Registers[layer, nd];
      return result;
    }

    public double[] GetWeightVector()
    {
      var weights = new List<double>();
      WalkWeights(get: w => weights.Add(w));
      return weights.ToArray();
    }

    public void SetWeightVector(double[] w)
    {
      var q = new Queue<double>(w);
      WalkWeights(set: () => q.Dequeue());
    }

    int _WeightVectorLength = -1;
    public int WeightVectorLength
    {
      get
      {
        if (_WeightVectorLength == -1)
          _WeightVectorLength = GetWeightVector().Length;
        return _WeightVectorLength;
      }
    }

    public void WalkWeights(Action<double> get = null, Func<double> set = null)
    {
      List.Repeat(NumLayers, layer => {
        List.Repeat(NodeCounts[layer], node => {
          bool isOutputLayer = layer == NumLayers - 1;
          if (!isOutputLayer)
          {
            if (get != null)
              get(Biases[layer, node]);
            else
              Biases[layer, node] = set();
          }

          int numInputs = layer == 0 ? NumInputs : NodeCounts[layer - 1];
          for (int input = 0; input < numInputs; input++)
            if (get != null)
              get(Weights[layer, node, input]);
            else
              Weights[layer, node, input] = set();
        });
      });
    }

    static double Linear(double x)
    {
      return x;
    }

    static double LogisticSigmoid(double x)
    {
      return 1 / (1 + Math.Exp(-x));
    }

    public static List<double> Train(ElmanNet net, Matrix trainingData, Vector outputData)
    {
      var result = Optimizer.Anneal(net.WeightVectorLength, 5, w => {
        ((IPredictor)net).Reset();
        net.SetWeightVector(w.ToArray());
        int correctCount = 0;
        double errorSum = 0;
        for (int i = 0; i < trainingData.ColumnCount; i++)
        {
          var output = net.Propagate(trainingData.Column(i).ToArray());
          errorSum += Math.Abs(output - outputData[i]);
          if (Math.Sign(output) == Math.Sign(outputData[i]))
            correctCount++;
        }
        //return (double)correctCount / trainingData.ColumnCount;
        return errorSum / trainingData.ColumnCount;
      });

      ((IPredictor)net).Reset();
      net.SetWeightVector(result.Params.ToArray());
      return result.CostHistory;
    }

    //public static void Train(ElmanNet net, Matrix trainingData, Vector outputData)
    //{
    //  var result = BCO.Optimize(Optimizer.RandomVector(net.WeightVectorLength, -5, 5).ToArray(), w => {
    //    ((IPredictor)net).Reset();
    //    net.SetWeightVector(w.ToArray());
    //    int correctCount = 0;
    //    double errorSum = 0;
    //    for (int i = 0; i < trainingData.ColumnCount; i++)
    //    {
    //      var output = net.Propagate(trainingData.Column(i).ToArray());
    //      errorSum += Math.Abs(output - outputData[i]);
    //      if (Math.Sign(output) == Math.Sign(outputData[i]))
    //        correctCount++;
    //    }
    //    //return (double)correctCount / trainingData.ColumnCount;
    //    var cost = errorSum / trainingData.ColumnCount;
    //    Trace.WriteLine("Accuracy: " + cost);
    //    return cost;
    //  }, 30000, Math.Pow(10, -3), 10, 10);

    //  ((IPredictor)net).Reset();
    //  net.SetWeightVector(result.MinimumLocation);
    //}
  }
}
