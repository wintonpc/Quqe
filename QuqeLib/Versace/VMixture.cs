using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PCW;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public class VMixture : IPredictor
  {
    public readonly List<RnnExpert> RnnExperts;
    public readonly List<RbfExpert> RbfExperts;
    public readonly VersaceContext Context;
    public List<VExpert> AllExperts { get { return RnnExperts.Concat<VExpert>(RbfExperts).ToList(); } }
    List<IPredictor> Predictors;

    public static VMixture CreateRandom(VersaceContext context)
    {
      var s = context.Settings;
      return new VMixture(context,
        List.Repeat(s.RnnExpertsPerMixture, i => new RnnExpert(context, VChromosome.CreateRandom(context), s.PreprocessingType)),
        List.Repeat(s.RbfExpertsPerMixture, i => new RbfExpert(context, VChromosome.CreateRandom(context), s.PreprocessingType)));
    }

    VMixture(VersaceContext context, List<RnnExpert> rnnExperts, List<RbfExpert> rbfExperts)
    {
      RnnExperts = rnnExperts;
      RbfExperts = rbfExperts;
      Context = context;
    }

    public List<VMixture> Crossover(VMixture other)
    {
      var qRnn = RnnExperts.Zip(other.RnnExperts, (a, b) => a.Crossover(b)).ToList();
      var qRbf = RbfExperts.Zip(other.RbfExperts, (a, b) => a.Crossover(b)).ToList();
      return List.Create(
        new VMixture(Context, qRnn.Select(x => x[0]).ToList(), qRbf.Select(x => x[0]).ToList()),
        new VMixture(Context, qRnn.Select(x => x[1]).ToList(), qRbf.Select(x => x[1]).ToList()));
    }

    public VMixture Mutate()
    {
      return new VMixture(Context, RnnExperts.Select(x => x.Mutate()).ToList(), RbfExperts.Select(x => x.Mutate()).ToList());
    }

    public double Fitness { get; private set; }
    public static double ComputePredictorFitness(IPredictor predictor, Mat input, Vec output)
    {
      int correctCount = 0;
      predictor = predictor.Reset();
      for (int j = 0; j < output.Count; j++)
      {
        var r = input.Column(j);
        var s = predictor.Predict(r);
        var prediction = Math.Sign(s);
        Debug.Assert(prediction != 0);
        if (output[j] == prediction)
          correctCount++;
      }
      return (double)correctCount / output.Count;
    }

    public double ComputeFitness()
    {
      return Fitness = ComputePredictorFitness(this, Context.Validation.Input, Context.Validation.Output);
    }

    public double Predict(Vec input)
    {
      EnsurePredictors();
      return Math.Sign(Predictors.Average(x => {
        double prediction = x.Predict(input);
        return double.IsNaN(prediction) ? 0 : prediction;
      }));
    }

    void EnsurePredictors()
    {
      Predictors = Predictors ?? AllExperts.Select(x => x.MakePredictor()).ToList();
    }

    public IPredictor Reset()
    {
      return new VMixture(Context, RnnExperts, RbfExperts);
    }

    bool IsDisposed;
    public void Dispose()
    {
      if (IsDisposed) return;
      IsDisposed = true;
      foreach (var p in Predictors)
        p.Dispose();
    }
  }
}
