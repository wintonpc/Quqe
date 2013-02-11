﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra.Generic;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;
using PCW;

namespace Quqe
{
  public abstract class Expert : IPredictor
  {
    public readonly VChromosome Chromosome;
    public readonly PreprocessingType PreprocessingType;
    Mat PrincipalComponents;

    protected abstract IPredictor Network { get; }
    public virtual bool IsDegenerate { get { return false; } }

    protected Expert(VChromosome chromosome, PreprocessingType preprocessType)
    {
      Chromosome = chromosome;
      PreprocessingType = preprocessType;
    }

    public void Train()
    {
      int offset = Math.Min((int)(Chromosome.TrainingOffsetPct * Versace.TrainingInput.ColumnCount), Versace.TrainingInput.ColumnCount - 1);
      // change next line to use MathEx.Clamp()
      int size = Math.Max(1, Math.Min((int)(Chromosome.TrainingSizePct * Versace.TrainingInput.ColumnCount), Versace.TrainingInput.ColumnCount - offset));
      var outputs = Versace.TrainingOutput.SubVector(offset, size);
      var inputs = Versace.TrainingInput.Columns().Skip(offset).Take(size).ToList(); // TODO: don't call Columns
      var preprocessedInputs = Preprocess(inputs, true);

      Train(preprocessedInputs, outputs);
    }

    protected abstract void Train(List<Vec> preprocessedInputs, Vec outputs);

    List<Vec> Preprocess(List<Vec> inputs, bool recalculatePrincipalComponents = false)
    {
      // database selection
      if (Chromosome.DatabaseType == DatabaseType.A)
        inputs = inputs.Select(x => x.SubVector(0, Versace.DatabaseAInputLength[PreprocessingType])).ToList();

      // complement coding
      if (Chromosome.UseComplementCoding)
        inputs = inputs.Select(x => Versace.ComplementCode(x)).ToList();

      // PCA
      if (Chromosome.UsePrincipalComponentAnalysis)
      {
        if (recalculatePrincipalComponents)
          PrincipalComponents = Versace.PrincipleComponents(inputs.ColumnsToMatrix());
        var pcNumber = Math.Min(Chromosome.PrincipalComponent, PrincipalComponents.ColumnCount - 1);
        inputs = inputs.Select(x => Versace.NthPrincipleComponent(PrincipalComponents, pcNumber, x)).ToList();
      }

      return inputs;
    }

    public abstract double RelativeComplexity { get; }

    public virtual double Predict(Vec input)
    {
      return Network.Predict(Preprocess(List.Create(input)).Single());
    }

    public abstract IPredictor Reset();

    public void Dispose()
    {
      if (Network != null)
        Network.Dispose();
    }
  }

  public class RnnExpert : Expert
  {
    readonly int TrialCount;
    readonly bool PreserveTrainingInit;
    Vec InitialWeights;

    RNN RNNNetwork;
    protected override IPredictor Network { get { return RNNNetwork; } }

    public RnnExpert(VChromosome chromosome, PreprocessingType preprocessType, Vec initialWeights = null, int trialCount = 1)
      : base(chromosome, preprocessType)
    {
      TrialCount = trialCount;
      InitialWeights = initialWeights;
    }

    bool IsTrained;
    protected override void Train(List<Vec> inputs, Vec outputs)
    {
      if (IsTrained)
        throw new Exception("Expert has already been trained.");

      Func<int, LayerSpec> logisticSigmoidRecurrent = nodeCount =>
        new LayerSpec { NodeCount = nodeCount, ActivationType = ActivationType.LogisticSigmoid, IsRecurrent = true };

      var layers = new List<LayerSpec> {
        logisticSigmoidRecurrent(Chromosome.ElmanHidden1NodeCount),
        logisticSigmoidRecurrent(Chromosome.ElmanHidden2NodeCount),
        new LayerSpec { NodeCount = 1, ActivationType = ActivationType.Linear, IsRecurrent = false }
      };
      var trainingData = inputs.ColumnsToMatrix();
      var epochMax = Chromosome.ElmanTrainingEpochs;

      RnnTrainResult trainResult;
      if (TrialCount > 1)
        trainResult = RNN.TrainSCGMulti(layers, epochMax, trainingData, outputs, TrialCount);
      else
      {
        var initialWeights = InitialWeights ?? RNN.MakeRandomWeights(RNN.GetWeightCount(layers, inputs.First().Count));
        trainResult = RNN.TrainSCG(layers, initialWeights, epochMax, trainingData, outputs);
      }

      InitialWeights = trainResult.InitialWeights;
      RNNNetwork = new RNN(trainResult.RNNSpec);
      IsTrained = true;
    }

    public override double RelativeComplexity
    {
      get
      {
        var x = Chromosome;
        var inputFactor = (x.UseComplementCoding ? 2 : 1)
          * (x.DatabaseType == DatabaseType.A ? Versace.DatabaseAInputLength[Versace.Settings.PreprocessingType] : Versace.TrainingInput.RowCount);
        return x.ElmanHidden1NodeCount * x.ElmanHidden2NodeCount * x.ElmanTrainingEpochs * inputFactor;
      }
    }

    public List<RnnExpert> Crossover(RnnExpert other)
    {
      var crossedChromosomes = this.Chromosome.Crossover(other.Chromosome);
      var a = crossedChromosomes[0];
      var b = crossedChromosomes[1];
      Debug.Assert(this.PreprocessingType == other.PreprocessingType);
      return List.Create(
        new RnnExpert(a, this.PreprocessingType, this.InitialWeights, TrialCount),
        new RnnExpert(b, this.PreprocessingType, other.InitialWeights, TrialCount));
    }

    public RnnExpert Mutate()
    {
      return new RnnExpert(Chromosome.Mutate(), PreprocessingType, InitialWeights, TrialCount);
    }

    public RnnExpert ReinitializeWeights()
    {
      return new RnnExpert(Chromosome, PreprocessingType, null, TrialCount);
    }

    public override IPredictor Reset()
    {
      return new RnnExpert((RNN)RNNNetwork.Reset(), Chromosome, PreprocessingType, InitialWeights, TrialCount);
    }

    RnnExpert(RNN net, VChromosome chromosome, PreprocessingType preprocessType, Vec initialWeights = null, int trialCount = 1)
      : base(chromosome, preprocessType)
    {
      RNNNetwork = net;
      IsTrained = true;
      TrialCount = trialCount;
      InitialWeights = initialWeights;
    }

    public override string ToString()
    {
      return string.Format("{0}-{1}-1:{2}", Chromosome.ElmanHidden1NodeCount, Chromosome.ElmanHidden2NodeCount, Chromosome.ElmanTrainingEpochs);
    }
  }

  public class RbfExpert : Expert
  {
    RBFNet RBFNetwork;
    protected override IPredictor Network { get { return RBFNetwork; } }

    public RbfExpert(VChromosome chromosome, PreprocessingType preprocessType)
      : base(chromosome, preprocessType)
    {
    }

    protected override void Train(List<Vec> inputs, Vec outputs)
    {
      RBFNetwork = RBFNet.Train(inputs.ColumnsToMatrix(), outputs, Chromosome.RbfNetTolerance, Chromosome.RbfGaussianSpread);
    }

    public override double RelativeComplexity
    {
      get
      {
        var x = Chromosome;
        var inputFactor = (x.UseComplementCoding ? 2 : 1)
          * (x.DatabaseType == DatabaseType.A ? Versace.DatabaseAInputLength[Versace.Settings.PreprocessingType] : Versace.TrainingInput.RowCount);
        return x.TrainingSizePct * inputFactor;
      }
    }

    public override double Predict(Vec input)
    {
      return RBFNetwork.IsDegenerate ? 0 : base.Predict(input);
    }

    public List<RbfExpert> Crossover(RbfExpert other)
    {
      var crossedChromosomes = this.Chromosome.Crossover(other.Chromosome);
      var a = crossedChromosomes[0];
      var b = crossedChromosomes[1];
      Debug.Assert(this.PreprocessingType == other.PreprocessingType);
      return List.Create(
        new RbfExpert(a, this.PreprocessingType),
        new RbfExpert(b, this.PreprocessingType));
    }

    public RbfExpert Mutate()
    {
      return new RbfExpert(Chromosome.Mutate(), PreprocessingType);
    }

    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.AppendFormat("{0} centers, spread = {1}", RBFNetwork.NumCenters, RBFNetwork.Spread);
      if (RBFNetwork.IsDegenerate)
        sb.Append(" !! DEGENERATE !!");
      return sb.ToString();
    }

    public override bool IsDegenerate
    {
      get { return RBFNetwork.IsDegenerate; }
    }

    public override IPredictor Reset()
    {
      return new RbfExpert((RBFNet)RBFNetwork.Reset(), Chromosome, PreprocessingType);
    }

    RbfExpert(RBFNet net, VChromosome chromosome, PreprocessingType preprocessType)
      : base(chromosome, preprocessType)
    {
      RBFNetwork = net;
    }
  }
}