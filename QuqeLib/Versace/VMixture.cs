﻿using System;
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
    public List<Expert> AllExperts { get { return RnnExperts.Concat<Expert>(RbfExperts).ToList(); } }
    List<IPredictor> Predictors;

    public static VMixture CreateRandom()
    {
      return new VMixture(
        List.Repeat(Versace.Settings.RnnExpertsPerMixture, i => new RnnExpert(VChromosome.CreateRandom(), Versace.Settings.PreprocessingType)),
        List.Repeat(Versace.Settings.RbfExpertsPerMixture, i => new RbfExpert(VChromosome.CreateRandom(), Versace.Settings.PreprocessingType)));
    }

    VMixture(List<RnnExpert> rnnExperts, List<RbfExpert> rbfExperts)
    {
      RnnExperts = rnnExperts;
      RbfExperts = rbfExperts;
    }

    public List<VMixture> Crossover(VMixture other)
    {
      var qRnn = RnnExperts.Zip(other.RnnExperts, (a, b) => a.Crossover(b)).ToList();
      var qRbf = RbfExperts.Zip(other.RbfExperts, (a, b) => a.Crossover(b)).ToList();
      return List.Create(
        new VMixture(qRnn.Select(x => x[0]).ToList(), qRbf.Select(x => x[0]).ToList()),
        new VMixture(qRnn.Select(x => x[1]).ToList(), qRbf.Select(x => x[1]).ToList()));
    }

    public VMixture Mutate()
    {
      return new VMixture(RnnExperts.Select(x => x.Mutate()).ToList(), RbfExperts.Select(x => x.Mutate()).ToList());
    }

    public double Fitness { get; private set; }
    public static double ComputePredictorFitness(IPredictor predictor)
    {
      int correctCount = 0;
      predictor = predictor.Reset();
      for (int j = 0; j < Versace.ValidationOutput.Count; j++)
      {
        var prediction = Math.Sign(predictor.Predict(Versace.ValidationInput.Column(j)));
        Debug.Assert(prediction != 0);
        if (Versace.ValidationOutput[j] == prediction)
          correctCount++;
      }
      return (double)correctCount / Versace.ValidationOutput.Count;
    }

    public double ComputeFitness()
    {
      return Fitness = ComputePredictorFitness(this);
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
      return new VMixture(RnnExperts, RbfExperts);
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
