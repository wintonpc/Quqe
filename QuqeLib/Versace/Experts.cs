using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using PCW;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public abstract class VExpert
  {
    public readonly VChromosome Chromosome;
    public readonly PreprocessingType PreprocessingType;
    public readonly VersaceContext Context;
    Mat PrincipalComponents;

    protected VExpert(VersaceContext context, VChromosome chromosome, PreprocessingType preprocessType)
    {
      Chromosome = chromosome;
      PreprocessingType = preprocessType;
      Context = context;
    }

    public void Train()
    {
      int offset = Math.Min((int)(Chromosome.TrainingOffsetPct * Context.Training.Input.ColumnCount), Context.Training.Input.ColumnCount - 1);
      // change next line to use MathEx.Clamp()
      int size = Math.Max(1, Math.Min((int)(Chromosome.TrainingSizePct * Context.Training.Input.ColumnCount), Context.Training.Input.ColumnCount - offset));
      var outputs = Context.Training.Output.SubVector(offset, size);
      var inputs = Context.Training.Input.Columns().Skip(offset).Take(size).ToList(); // TODO: don't call Columns
      var preprocessedInputs = Preprocess(inputs, true);

      Train(preprocessedInputs, outputs);
    }

    protected abstract void Train(List<Vec> preprocessedInputs, Vec outputs);

    List<Vec> Preprocess(List<Vec> inputs, bool recalculatePrincipalComponents)
    {
      // database selection
      if (Chromosome.DatabaseType == DatabaseType.A)
        inputs = inputs.Select(x => x.SubVector(0, Context.Training.DatabaseAInputLength)).ToList();

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

    public IPredictor MakePredictor()
    {
      return new PredictorWithPreprocessing(MakePredictorInternal(), input => Preprocess(List.Create(input), false).First());
    }

    protected abstract IPredictor MakePredictorInternal();

    class PredictorWithPreprocessing : IPredictor
    {
      readonly IPredictor Predictor;
      readonly Func<Vec, Vec> Preprocess;
      public PredictorWithPreprocessing(IPredictor predictor, Func<Vec, Vec> preprocess)
      {
        Predictor = predictor;
        Preprocess = preprocess;
      }

      public double Predict(Vec input)
      {
        return Predictor.Predict(Preprocess(input));
      }

      public IPredictor Reset()
      {
        return new PredictorWithPreprocessing(Predictor.Reset(), Preprocess);
      }

      public void Dispose()
      {
        Predictor.Dispose();
      }
    }

    protected static double GetRelativeComplexityInputFactor(VersaceContext context, VChromosome c)
    {
      return (c.UseComplementCoding ? 2 : 1) * (c.DatabaseType == DatabaseType.A ? context.DatabaseAInputLength : context.DatabaseBInputLength);
    }
    public abstract double RelativeComplexity { get; }
  }

  public class RnnExpert : VExpert
  {
    readonly int TrialCount;
    Vec InitialWeights;

    RNNSpec RNNSpec;

    public RnnExpert(VersaceContext context, VChromosome chromosome, PreprocessingType preprocessType, Vec initialWeights = null, int trialCount = 1)
      : base(context, chromosome, preprocessType)
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
        var rnnWeightCount = RNN.GetWeightCount(layers, inputs.First().Count);
        if (InitialWeights == null || InitialWeights.Count != rnnWeightCount)
          InitialWeights = RNN.MakeRandomWeights(rnnWeightCount);
        trainResult = RNN.TrainSCG(layers, InitialWeights, epochMax, trainingData, outputs);
      }

      RNNSpec = trainResult.RNNSpec;
      IsTrained = true;
    }

    public override double RelativeComplexity
    {
      get
      {
        var x = Chromosome;
        return GetRelativeComplexityInputFactor(Context, x) * x.ElmanHidden1NodeCount * x.ElmanHidden2NodeCount * x.ElmanTrainingEpochs;
      }
    }

    public List<RnnExpert> Crossover(RnnExpert other)
    {
      var crossedChromosomes = this.Chromosome.Crossover(other.Chromosome);
      var a = crossedChromosomes[0];
      var b = crossedChromosomes[1];
      Debug.Assert(this.PreprocessingType == other.PreprocessingType);
      return List.Create(
        new RnnExpert(Context, a, this.PreprocessingType, this.InitialWeights, TrialCount),
        new RnnExpert(Context, b, this.PreprocessingType, other.InitialWeights, TrialCount));
    }

    public RnnExpert Mutate()
    {
      return new RnnExpert(Context, Chromosome.Mutate(), PreprocessingType, InitialWeights, TrialCount);
    }

    public RnnExpert ReinitializeWeights()
    {
      return new RnnExpert(Context, Chromosome, PreprocessingType, null, TrialCount);
    }

    protected override IPredictor MakePredictorInternal()
    {
      return new RNN(RNNSpec);
    }

    public override string ToString()
    {
      return string.Format("{0}-{1}-1:{2}", Chromosome.ElmanHidden1NodeCount, Chromosome.ElmanHidden2NodeCount, Chromosome.ElmanTrainingEpochs);
    }
  }

  public class RbfExpert : VExpert
  {
    RBFNet RBFNetwork;

    public RbfExpert(VersaceContext context, VChromosome chromosome, PreprocessingType preprocessType)
      : base(context, chromosome, preprocessType)
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
        return GetRelativeComplexityInputFactor(Context, x) * x.TrainingSizePct;
      }
    }

    public List<RbfExpert> Crossover(RbfExpert other)
    {
      var crossedChromosomes = this.Chromosome.Crossover(other.Chromosome);
      var a = crossedChromosomes[0];
      var b = crossedChromosomes[1];
      Debug.Assert(this.PreprocessingType == other.PreprocessingType);
      return List.Create(
        new RbfExpert(Context, a, this.PreprocessingType),
        new RbfExpert(Context, b, this.PreprocessingType));
    }

    public RbfExpert Mutate()
    {
      return new RbfExpert(Context, Chromosome.Mutate(), PreprocessingType);
    }

    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.AppendFormat("{0} centers, spread = {1}", RBFNetwork.NumCenters, RBFNetwork.Spread);
      if (RBFNetwork.IsDegenerate)
        sb.Append(" !! DEGENERATE !!");
      return sb.ToString();
    }

    protected override IPredictor MakePredictorInternal()
    {
      return RBFNetwork;
    }
  }
}
