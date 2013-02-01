using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Diagnostics;
using YamlDotNet.RepresentationModel.Serialization;
using System.Xml.Serialization;
using System.Xml.Linq;
using System.Xml;
using System.Threading.Tasks;
using System.Runtime;
using System.Reflection;
using System.Threading;
using MathNet.Numerics.Statistics;
using System.Globalization;

namespace Quqe
{
  public static partial class Versace
  {
    static VersaceSettings _Settings;
    public static VersaceSettings Settings
    {
      get { return _Settings; }
      set
      {
        if (value == null)
        {
          TrainingInput = ValidationInput = TestingInput = null;
          TrainingOutput = ValidationOutput = TestingOutput = null;
        }
        else
        {
          _Settings = value;
          LoadPreprocessedValues();
        }
      }
    }

    static Versace()
    {
      Settings = new VersaceSettings();
    }

    public static Dictionary<PreprocessingType, int> DatabaseAInputLength = new Dictionary<PreprocessingType, int>();

    public static List<string> GetTickers(string predictedSymbol)
    {
      return List.Create(predictedSymbol, "^IXIC", "^GSPC", "^DJI", "^DJT", "^DJU", "^DJA", "^N225", "^BVSP",
        "^GDAX", "^FTSE", /*"^CJJ", "USDCHF"*/ "^TYX", "^TNX", "^FVX", "^IRX", /*"EUROD"*/ "^XAU");
    }

    public static Matrix TrainingInput { get; private set; }
    public static Matrix ValidationInput { get; private set; }
    public static Matrix TestingInput { get; private set; }
    public static Vector TrainingOutput { get; private set; }
    public static Vector ValidationOutput { get; private set; }
    public static Vector TestingOutput { get; private set; }

    public static Func<DataSeries<Bar>, double> GetIdealSignalFunc(PredictionType pt)
    {
      if (pt == PredictionType.NextClose)
        return s => s.Pos == 0 ? 0 : Math.Sign(s[0].Close - s[1].Close);
      else
        throw new Exception("Unexpected PredictionType: " + pt);
    }


    public static Matrix MatrixFromColumns(List<Vector> columns)
    {
      var m = columns.First().Count;
      var n = columns.Count;

      Matrix X = new DenseMatrix(m, n);

      for (int i = 0; i < m; i++)
        for (int j = 0; j < n; j++)
          X[i, j] = columns[j][i];

      return X;
    }

    public static List<Vector> Columns(this Matrix m)
    {
      return List.Repeat(m.ColumnCount, j => (Vector)m.Column(j));
    }

    public static List<Vector> Rows(this Matrix m)
    {
      return List.Repeat(m.RowCount, j => (Vector)m.Row(j));
    }

    public static Matrix SeriesToMatrix(List<DataSeries<Value>> series)
    {
      var m = series.Count;
      var n = series.First().Length;

      Matrix X = new DenseMatrix(m, n);
      for (int i = 0; i < m; i++)
        for (int j = 0; j < n; j++)
          X[i, j] = series[i][j];

      return X;
    }

    public static Vector ComplementCode(Vector input)
    {
      return new DenseVector(input.Concat(input.Select(x => 1.0 - x)).ToArray());
    }

    public static Matrix PrincipleComponents(Matrix data)
    {
      var rows = data.Rows();
      var meanAdjustedRows = rows.Select(x => (Vector)x.Subtract(x.Average())).ToList();
      var X = MatrixFromColumns(meanAdjustedRows); // we needed to transpose it anyway

      var svd = X.Svd(true);
      var V = (Matrix)svd.VT().Transpose();
      return V;
    }

    public static Vector NthPrincipleComponent(Matrix principleComponents, int n, Vector x)
    {
      var pc = principleComponents.Column(n);
      return (Vector)(x.DotProduct(pc) * pc);
    }

    public static void Train(Action<List<PopulationInfo>> historyChanged, Action<VersaceResult> whenDone)
    {
      SyncContext sync = SyncContext.Current;
      Thread t = new Thread(() => {
        if (Settings.TrainingMethod == TrainingMethod.Evolve)
        {
          var result = Versace.Evolve(historyChanged);
          sync.Post(() => whenDone(result));
        }
      });
      t.IsBackground = true;
      t.Start();
    }

    static Random Random = new Random();
    public static VersaceResult Evolve(Action<List<PopulationInfo>> historyChanged = null)
    {
      GCSettings.LatencyMode = GCLatencyMode.Batch;
      var history = new List<PopulationInfo>();
      var population = List.Repeat(Settings.PopulationSize, n => new VMixture());
      VMixture bestMixture = null;
      for (int epoch = 0; epoch < Settings.EpochCount; epoch++)
      {
        Trace.WriteLine(string.Format("Epoch {0} started {1}", epoch, DateTime.Now));

        int numTrained = 0;
        object trainLock = new object();
        Action trainedOne = () => {
          lock (trainLock)
          {
            numTrained++;
            Trace.WriteLine(string.Format("Epoch {0}, trained {1} / {2}", epoch, numTrained, Settings.ExpertsPerMixture * Settings.PopulationSize));
            if (numTrained % 10 == 0)
              GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
          }
        };

        bool parallelize = true;

        if (!parallelize)
        {
          RBFNet.ShouldTrace = true;
          RNN.ShouldTrace = true;
          foreach (var expert in population.Where(mixture => mixture.Fitness == 0)
            .SelectMany(mixture => mixture.Members).Select(m => m.Expert))
          {
            expert.Train();
            trainedOne();
          }
        }
        else
        {
          RBFNet.ShouldTrace = false;
          RNN.ShouldTrace = false;
          // optimize training order to keep the most load on the CPUs
          var allUntrainedExperts = population.Where(mixture => mixture.Fitness == 0).SelectMany(mixture => mixture.Members).ToList();
          var rnnExperts = allUntrainedExperts.Where(x => x.NetworkType == NetworkType.RNN).OrderByDescending(x => {
            var inputFactor = (x.UseComplementCoding ? 2 : 1) * (x.DatabaseType == DatabaseType.A ? DatabaseAInputLength[Settings.PreprocessingType] : TrainingInput.RowCount);
            return x.ElmanHidden1NodeCount * x.ElmanHidden2NodeCount * x.ElmanTrainingEpochs * inputFactor;
          }).ToList();
          var rbfExperts = allUntrainedExperts.Where(x => x.NetworkType == NetworkType.RBF).OrderByDescending(x => {
            var inputFactor = (x.UseComplementCoding ? 2 : 1) * (x.DatabaseType == DatabaseType.A ? DatabaseAInputLength[Settings.PreprocessingType] : TrainingInput.RowCount);
            return x.TrainingSizePct * inputFactor;
          }).ToList();
          var reordered = rnnExperts.Concat(rbfExperts).ToList();
          Parallel.Invoke(new ParallelOptions { MaxDegreeOfParallelism = 8 },
            reordered
            .Select(m => m.Expert).Select(expert => new Action(() => {
              expert.Train();
              //expert.TrainEx(rnnTrialCount: 3);
              trainedOne();
            })).ToArray());
        }

        foreach (var mixture in population.Where(mixture => mixture.Fitness == 0))
          mixture.ComputeAndSetFitness();
        var oldPopulation = population.ToList();
        var rankedPopulation = population.OrderByDescending(m => m.Fitness).ToList();
        var selected = rankedPopulation.Take(Settings.SelectionSize).Shuffle().ToList();
        population = rankedPopulation.Take(Settings.PopulationSize - Settings.SelectionSize).ToList();
        //population = new List<VMixture>();

        Debug.Assert(selected.Count % 2 == 0);
        for (int i = 0; i < selected.Count; i += 2)
          population.AddRange(selected[i].Crossover(selected[i + 1]));

        //for (int i = 0; i < Settings.PopulationSize / 2; i++)
        //{
        //  var a = selected.RandomItem();
        //  var b = selected.Except(List.Create(a)).RandomItem();
        //  population.AddRange(a.CrossoverAndMutate(b));
        //}

        //// mutate the very best just a little bit for some variation
        //for (int i = 0; i < SELECTION_SIZE / 2; i++)
        //  population[i] = population[i].Mutate(mutationRate: 0.5, dampingFactor: 4);
        //// mutate the rest entirely randomly
        //for (int i = SELECTION_SIZE / 2; i < population.Count; i++)
        //  population[i] = population[i].Mutate(dampingFactor: 0);

        population = population.Select(x => x.Mutate(dampingFactor: 0)).ToList();

        if ((epoch + 1) % 4 == 0)
        {
          foreach (var mixture in population.ToList())
          {
            var otherGood = oldPopulation.Except(List.Create(mixture)).SelectMany(mix => mix.Members.Where(m => !m.Expert.IsDegenerate)).ToList();
            foreach (var member in mixture.Members.ToList())
            {
              if (member.Expert.IsDegenerate)
              {
                mixture.Members.Remove(member);
                VChromosome otherGoodClone;
                if (!otherGood.Any())
                  otherGoodClone = new VMixture().Members.First();
                else
                  otherGoodClone = XSer.Read<VChromosome>(XSer.Write(otherGood.RandomItem()));
                mixture.Members.Add(otherGoodClone);
              }
            }
          }
        }

        var bestThisEpoch = rankedPopulation.First();
        if (bestMixture == null || bestThisEpoch.Fitness > bestMixture.Fitness)
          bestMixture = bestThisEpoch;
        var diversity = Diversity(oldPopulation);
        history.Add(new PopulationInfo { Fitness = bestThisEpoch.Fitness, Diversity = diversity });
        if (historyChanged != null)
          historyChanged(history);
        Trace.WriteLine(string.Format("Epoch {0} fitness:  {1:N1}%   (Best: {2:N1}%)   Diversity: {3:N4}", epoch, bestThisEpoch.Fitness * 100.0, bestMixture.Fitness * 100.0, diversity));
        var oldChromosomes = oldPopulation.SelectMany(m => m.Members).ToList();
        Trace.WriteLine(string.Format("Epoch {0} composition:   Elman {1:N1}%   RBF {2:N1}%", epoch,
          (double)oldChromosomes.Count(x => x.NetworkType == NetworkType.RNN) / oldChromosomes.Count * 100,
          (double)oldChromosomes.Count(x => x.NetworkType == NetworkType.RBF) / oldChromosomes.Count * 100));
        Trace.WriteLine(string.Format("Epoch {0} ended {1}", epoch, DateTime.Now));
        GC.Collect();
        Trace.WriteLine("===========================================================================");
      }
      var result = new VersaceResult(bestMixture, history, Versace.Settings);
      result.Save();
      return result;
    }

    static double Diversity(List<VMixture> population)
    {
      var d = new DenseMatrix(Settings.ExpertsPerMixture * population.First().Members.First().Chromosome.Count, Settings.PopulationSize);
      for (int m = 0; m < population.Count; m++)
        d.SetColumn(m, population[m].Members.SelectMany(mem => mem.Chromosome.Select(x => (x.GetDoubleValue() - x.GetDoubleMin()) / (x.GetDoubleMax() - x.GetDoubleMin()))).ToArray());
      return d.Rows().Sum(r => Statistics.Variance(r));
    }
  }

  public class PopulationInfo
  {
    public double Fitness;
    public double Diversity;
  }

  public class PreprocessedData
  {
    public DataSeries<Bar> Predicted;
    public Matrix Inputs;
    public Vector Outputs;
  }

  public class VChromosome
  {
    static readonly Random Random = new Random();
    
    public List<VGene> Chromosome;

    public VChromosome(Func<string, VGene> makeGene)
    {
      Chromosome = new List<VGene> {
        makeGene("NetworkType"),
        makeGene("ElmanTrainingEpochs"),
        makeGene("DatabaseType"),
        makeGene("TrainingOffsetPct"),
        makeGene("TrainingSizePct"),
        makeGene("UseComplementCoding"),
        makeGene("UsePrincipalComponentAnalysis"),
        makeGene("PrincipalComponent"),
        makeGene("RbfNetTolerance"),
        makeGene("RbfGaussianSpread"),
        makeGene("ElmanHidden1NodeCount"),
        makeGene("ElmanHidden2NodeCount")
      };
    }

    VChromosome(List<VGene> genes)
    {
      Chromosome = genes;
    }

    public List<VChromosome> Crossover(VChromosome other)
    {
      var children = Crossover(Chromosome, other.Chromosome);
      var a = children[0];
      var b = children[1];
      return List.Create(
        new VChromosome(children[0]),
        new VChromosome(children[1]));
    }

    static List<List<VGene>> Crossover(IEnumerable<VGene> x, IEnumerable<VGene> y)
    {
      var a = x.ToList();
      var b = y.ToList();
      for (int i = 0; i < a.Count; i++)
        if (Optimizer.WithProb(0.5))
        {
          var t = a[i];
          a[i] = b[i];
          b[i] = t;
        }
      return List.Create(a, b);
    }

    public VChromosome Mutate(double? mutationRate = null, double? dampingFactor = null)
    {
      mutationRate = mutationRate ?? Versace.Settings.MutationRate;
      dampingFactor = dampingFactor ?? Versace.Settings.MutationDamping;
      object trainingInit = this.Expert.TrainingInit;
      if (trainingInit != null && NetworkType == NetworkType.RNN && Expert.Network != null)
        if (Optimizer.WithProb(mutationRate.Value))
          trainingInit = Optimizer.RandomVector(((RNN)Expert.Network).GetWeightVector().Count, -1, 1);
      return new VChromosome(Mutate(Chromosome, mutationRate.Value, dampingFactor.Value), trainingInit);
    }

    static List<VGene> Mutate(IEnumerable<VGene> genes, double? mutationRate = null, double? dampingFactor = null)
    {
      mutationRate = mutationRate ?? Versace.Settings.MutationRate;
      dampingFactor = dampingFactor ?? Versace.Settings.MutationDamping;
      return genes.Select(g => Random.NextDouble() < mutationRate.Value ? g.Mutate(dampingFactor.Value) : g).ToList();
    }

    TValue GetGeneValue<TValue>(string name) where TValue : struct
    {
      return ((VGene<TValue>)Chromosome.First(g => g.Name == name)).Value;
    }

    public NetworkType NetworkType { get { return GetGeneValue<int>("NetworkType") == 0 ? NetworkType.RNN : NetworkType.RBF; } }
    public int ElmanTrainingEpochs { get { return GetGeneValue<int>("ElmanTrainingEpochs"); } }
    public DatabaseType DatabaseType { get { return GetGeneValue<int>("DatabaseType") == 0 ? DatabaseType.A : DatabaseType.B; } }
    public double TrainingOffsetPct { get { return GetGeneValue<double>("TrainingOffsetPct"); } }
    public double TrainingSizePct { get { return GetGeneValue<double>("TrainingSizePct"); } }
    public bool UseComplementCoding { get { return GetGeneValue<int>("UseComplementCoding") == 1; } }
    public bool UsePrincipalComponentAnalysis { get { return GetGeneValue<int>("UsePrincipalComponentAnalysis") == 1; } }
    public int PrincipalComponent { get { return GetGeneValue<int>("PrincipalComponent"); } }
    public double RbfNetTolerance { get { return GetGeneValue<double>("RbfNetTolerance"); } }
    public double RbfGaussianSpread { get { return GetGeneValue<double>("RbfGaussianSpread"); } }
    public int ElmanHidden1NodeCount { get { return GetGeneValue<int>("ElmanHidden1NodeCount"); } }
    public int ElmanHidden2NodeCount { get { return GetGeneValue<int>("ElmanHidden2NodeCount"); } }
  }

  public enum NetworkType { RNN, RBF }
  public enum DatabaseType { A, B }

  public class VMixture : IPredictor
  {
    public List<VChromosome> Members { get; private set; }

    public VMixture()
    {
      Members = List.Repeat(Versace.Settings.ExpertsPerMixture, n =>
        new VChromosome(name => Versace.Settings.ProtoChromosome.First(x => x.Name == name).CloneAndRandomize()));
    }

    VMixture(List<VChromosome> members)
    {
      Members = members;
    }

    public List<VMixture> Crossover(VMixture other)
    {
      var q = Members.Zip(other.Members, (a, b) => a.Crossover(b));
      return List.Create(
        new VMixture(q.Select(x => x[0]).ToList()),
        new VMixture(q.Select(x => x[1]).ToList()));
    }

    public VMixture Mutate(double? mutationRate = null, double? dampingFactor = null)
    {
      mutationRate = mutationRate ?? Versace.Settings.MutationRate;
      dampingFactor = dampingFactor ?? Versace.Settings.MutationDamping;
      return new VMixture(Members.Select(x => x.Mutate(mutationRate, dampingFactor)).ToList());
    }

    public double Fitness { get; private set; }
    public static double ComputeFitness(IPredictor predictor)
    {
      int correctCount = 0;
      predictor.Reset();
      for (int j = 0; j < Versace.ValidationOutput.Count; j++)
      {
        var vote = Math.Sign(predictor.Predict(Versace.ValidationInput.Column(j)));
        Debug.Assert(vote != 0);
        if (Versace.ValidationOutput[j] == vote)
          correctCount++;
      }
      return (double)correctCount / Versace.ValidationOutput.Count;
    }

    public void ComputeAndSetFitness()
    {
      Fitness = ComputeFitness(this);
    }

    public double Predict(Vector<double> input)
    {
      return Math.Sign(Members.Select(x => x.Expert).Average(x => {
        double prediction = x.Predict(input);
        return double.IsNaN(prediction) ? 0 : prediction;
      }));
    }

    public void Reset()
    {
      foreach (var m in Members)
        m.Expert.Reset();
    }

    public void Dump()
    {
      Trace.WriteLine("Mixture, fitness: " + Fitness);
      var members = Members.OrderBy(x => x.NetworkType).ToList();
      for (int i = 0; i < members.Count; i++)
      {
        var mi = members[i];
        var ss = new List<string>();
        ss.Add(mi.NetworkType.ToString());
        ss.Add(mi.Expert.ToString());
        ss.Add("Training set size = " + (int)(mi.TrainingSizePct * Versace.TrainingOutput.Count));
        if (mi.UseComplementCoding)
          ss.Add("CC");
        if (mi.UsePrincipalComponentAnalysis)
          ss.Add("PC=" + mi.PrincipalComponent);
        ss.Add("DB=" + mi.DatabaseType);
        Trace.WriteLine("Expert " + i + ": " + ss.Join(", "));
      }
    }
  }

  public interface IPredictor
  {
    double Predict(Vector<double> input);
    void Reset();
  }

  public abstract class Expert : IPredictor
  {
    public readonly VChromosome Chromosome;
    protected PreprocessingType PreprocessingType { get; private set; }
    Matrix PrincipalComponents;

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

    protected abstract void Train(List<Vector> preprocessedInputs, Vector<double> outputs);

    List<Vector> Preprocess(List<Vector> inputs, bool recalculatePrincipalComponents = false)
    {
      // database selection
      if (Chromosome.DatabaseType == DatabaseType.A)
        inputs = inputs.Select(x => (Vector)x.SubVector(0, Versace.DatabaseAInputLength[PreprocessingType])).ToList();

      // complement coding
      if (Chromosome.UseComplementCoding)
        inputs = inputs.Select(x => Versace.ComplementCode(x)).ToList();

      // PCA
      if (Chromosome.UsePrincipalComponentAnalysis)
      {
        if (recalculatePrincipalComponents)
          PrincipalComponents = Versace.PrincipleComponents(Versace.MatrixFromColumns(inputs));
        var pcNumber = Math.Min(Chromosome.PrincipalComponent, PrincipalComponents.ColumnCount - 1);
        inputs = inputs.Select(x => Versace.NthPrincipleComponent(PrincipalComponents, pcNumber, x)).ToList();
      }

      return inputs;
    }

    public abstract List<Expert> Crossover(Expert other);

    public static Expert Create(VChromosome chromosome, PreprocessingType preprocessType)
    {
      if (chromosome.NetworkType == NetworkType.RNN)
        return new RnnExpert(chromosome, preprocessType);
      else if (chromosome.NetworkType == NetworkType.RBF)
        return new RbfExpert(chromosome, preprocessType);
      else
        throw new Exception();
    }

    public virtual double Predict(Vector<double> input)
    {
      return Network.Predict(Preprocess(List.Create((Vector)input)).Single());
    }

    public void Reset()
    {
      Network.Reset();
    }
  }

  public class RnnExpert : Expert
  {
    readonly int TrialCount;
    readonly bool PreserveTrainingInit;
    readonly Vector<double> TrainingInit;

    RNN RNNNetwork;
    protected override IPredictor Network { get { return RNNNetwork; } }

    public RnnExpert(VChromosome chromosome, PreprocessingType preprocessType, Vector<double> trainingInit = null, int trialCount = 1)
      : base(chromosome, preprocessType)
    {
      TrialCount = trialCount;
      TrainingInit = trainingInit;
    }

    bool IsTrained;
    protected override void Train(List<Vector> inputs, Vector<double> outputs)
    {
      if (IsTrained)
        throw new Exception("Expert has already been trained.");

      Func<int, LayerSpec> logisticSigmoidRecurrent = nodeCount =>
        new LayerSpec { NodeCount = nodeCount, ActivationType = ActivationType.LogisticSigmoid, IsRecurrent = true };

      var rnn = new RNN(inputs.First().Count, new List<LayerSpec> {
        logisticSigmoidRecurrent(Chromosome.ElmanHidden1NodeCount),
        logisticSigmoidRecurrent(Chromosome.ElmanHidden2NodeCount),
        new LayerSpec { NodeCount = 1, ActivationType = ActivationType.Linear, IsRecurrent = false }
      });

      Vector<double> trainingInit = TrainingInit ?? Optimizer.RandomVector(rnn.GetWeightVector().Count, 
      RnnTrainResult trainResult;
      if (TrialCount > 1)
        trainResult = RNN.TrainSCGMulti((RNN)rnn, Chromosome.ElmanTrainingEpochs, Versace.MatrixFromColumns(inputs), outputs, TrialCount, trainingInit);
      else
        trainResult = RNN.TrainSCG((RNN)rnn, Chromosome.ElmanTrainingEpochs, Versace.MatrixFromColumns(inputs), outputs, trainingInit);
      TrainingInit = trainResult.TrainingInit;
      RNNNetwork = rnn;
    }

    public override List<Expert> Crossover(Expert other)
    {
      var crossedGenes = this.Chromosome.Crossover(other.Chromosome);
      var a = crossedGenes[0];
      var b = crossedGenes[1];
      Debug.Assert(this.PreprocessingType == other.PreprocessingType);
      return List.Create(
        new RnnExpert(a, this.PreprocessingType,
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

    protected override void Train(List<Vector> inputs, Vector<double> outputs)
    {
      RBFNetwork = RBFNet.Train(Versace.MatrixFromColumns(inputs), (Vector)outputs, Chromosome.RbfNetTolerance, Chromosome.RbfGaussianSpread);
    }

    public override double Predict(Vector<double> input)
    {
      return RBFNetwork.IsDegenerate ? 0 : base.Predict(input);
    }

    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.AppendFormat("{0} centers, spread = {1}", RBFNetwork.NumCenters, RBFNetwork.Spread);
      if (RBFNetwork.IsDegenerate)
        sb.Append(" !! DEGENERATE !!");
      return sb.ToString();
    }
  }

  public static class VersaceBacktest
  {
    public static BacktestReport Backtest(PredictionType predictionType, IPredictor predictor, Account account, Matrix preInputs, Matrix inputs, DataSeries<Bar> bars)
    {
      double maxAccountLossPct = 0.025;

      // run through the preTesting values so the RNNs can build up state
      predictor.Reset();
      foreach (var input in preInputs.Columns())
        predictor.Predict(input);

      List<SignalValue> signal = null;
      if (predictionType == PredictionType.NextClose)
        signal = MakeSignalNextClose(predictor, inputs.Columns(), bars, maxAccountLossPct);
      BacktestReport report = PlaybackSignal(signal, bars, account);
      return report;
    }

    static List<SignalValue> MakeSignalNextClose(IPredictor predictor, List<Vector> inputs, DataSeries<Bar> bars, double maxAccountLossPct)
    {
      var signal = new List<SignalValue>();
      DataSeries<Value> buySell = inputs.Select(x => (double)Math.Sign(predictor.Predict(x))).ToDataSeries(bars);
      int riskATRPeriod = 7;
      double riskScale = 1.2;
      double riskRatio = 0.65;
      DataSeries<Value> perShareRisk =
        bars.OpeningWickHeight().EMA(riskATRPeriod).ZipElements<Value, Value>(
        bars.ATR(riskATRPeriod), (w, a, v) =>
          riskScale * (riskRatio * w[0] + (1 - riskRatio) * a[0]));

      for (int i = 0; i < bars.Length; i++)
      {
        var bias = buySell[i].Val > 0 ? SignalBias.Buy : SignalBias.Sell;
        var sizePct = maxAccountLossPct / perShareRisk[i];
        double absoluteStop;
        if (bias == SignalBias.Buy)
          absoluteStop = bars[i].Open - perShareRisk[0];
        else
          absoluteStop = bars[i].Open + perShareRisk[0];
        signal.Add(new SignalValue(bars[i].Timestamp, bias, SignalTimeOfDay.Close, sizePct, absoluteStop, null));
      }
      return signal;
    }

    private static BacktestReport PlaybackSignal(List<SignalValue> signal, DataSeries<Bar> bars, Account account)
    {
      throw new NotImplementedException();
    }
  }
}
