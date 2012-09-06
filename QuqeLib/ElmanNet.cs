using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;

namespace Quqe
{
  public class ElmanNet
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
      for (int layer = 0; layer < numLayers - 1; layer++)
        for (int node = 0; node < maxNodesPerLayer; node++)
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
      var outputs = NeuralNet.Repeat(numNodes, node => {
        bool isOutputLayer = layer == NumLayers - 1;
        var inputsPlusRegisters = isOutputLayer ? inputs :
          inputs.Concat(NeuralNet.Repeat(numNodes, nd => Registers[layer, nd])).ToArray();
        var weights = NeuralNet.Repeat(inputsPlusRegisters.Length, input => Weights[layer, node, input]);
        var activation = NeuralNet.DotProduct(inputsPlusRegisters, weights) - (isOutputLayer ? 0 : Biases[layer, node]);
        var activationFunc = isOutputLayer ? (Func<double, double>)Linear : (Func<double, double>)LogisticSigmoid;
        var output = activationFunc(activation);
        if (!isOutputLayer)
          Registers[layer, node] = output;
        return output;
      });
      return Propagate(outputs, layer + 1);
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
  }
}
