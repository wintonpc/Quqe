using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe.NewVersace
{
  public interface IPredictorWithInputs : IDisposable
  {
    double Predict(int t);
  }

  class ExpertPredictor : IPredictorWithInputs
  {
    readonly Mat TailoredInputs;
    readonly IPredictor Predictor;

    public ExpertPredictor(Expert expert, Mat inputs, int databaseAInputLength)
    {
      TailoredInputs = DataTailoring.TailorInputs(inputs, databaseAInputLength, expert.Chromosome);
      Predictor = MakePredictor(expert);
    }

    static IPredictor MakePredictor(Expert expert)
    {
      if (expert is RnnTrainRec)
      {
        var trainRec = (RnnTrainRec)expert;
        return new RNN(trainRec.RnnSpec.ToRnnSpec());
      }
      else if (expert is RbfTrainRec)
      {
        var trainRec = (RbfTrainRec)expert;
        return new RBFNet(trainRec.Bases.Select(b => b.ToRadialBasis()), trainRec.OutputBias, trainRec.Spread, trainRec.IsDegenerate);
      }
      else
        throw new Exception("Unexpected expert type");
    }

    public double Predict(int t)
    {
      return Predictor.Predict(TailoredInputs.Column(t));
    }

    public void Dispose()
    {
      Predictor.Dispose();
    }
  }

  class MixturePredictor : IPredictorWithInputs
  {
    List<ExpertPredictor> ExpertPredictors;

    public MixturePredictor(IEnumerable<ExpertPredictor> expertPredictors)
    {
      ExpertPredictors = expertPredictors.ToList();
    }

    public MixturePredictor(Mixture mixture, DataSet data)
    {
      ExpertPredictors = mixture.Experts.Select(x => new ExpertPredictor(x, data.Input, data.DatabaseAInputLength)).ToList();
    }

    public double Predict(int t)
    {
      return Math.Sign(ExpertPredictors.Select(ep => ep.Predict(t)).Average());
    }

    public void Dispose()
    {
      foreach (var ep in ExpertPredictors)
        ep.Dispose();
    }
  }

  class PredictorWithInputs : IPredictorWithInputs
  {
    readonly Mat Inputs;
    readonly IPredictor Predictor;

    public PredictorWithInputs(IPredictor predictor, Mat inputs)
    {
      Predictor = predictor;
      Inputs = inputs;
    }

    public double Predict(int t)
    {
      return Predictor.Predict(Inputs.Column(t));
    }

    public void Dispose()
    {
      Predictor.Dispose();
    }
  }
}