using System;
using System.Diagnostics;
using System.Linq;
using Machine.Specifications;
using NUnit.Framework;
using Quqe;
using Quqe.NewVersace;

namespace QuqeTest
{
  [TestFixture]
  class RBFTests
  {
    [Test]
    public void RBFTrainingIsReproducible()
    {
      var data = NNTestUtils.GetData("2004-01-01", "2004-05-01");
      var fitnesses = Lists.Repeat(5, i => {
        var rbfNet = RBFNet.Train(data.Input, data.Output, 0.1, 1);
        rbfNet.IsDegenerate.ShouldBeFalse();
        return Functions.ComputeFitness(new PredictorWithInputs(rbfNet, data.Input), data);
      });
      fitnesses.Distinct().Count().ShouldEqual(1);
    }

    [Test]
    public void RBFPredictsWell()
    {
      var data = NNTestUtils.GetData("2004-01-01", "2004-05-01");

      Func<double, double> fitnessForTolerance = tolerance => {
        var rbfNet = RBFNet.Train(data.Input, data.Output, tolerance, 1);
        rbfNet.IsDegenerate.ShouldBeFalse();
        var fitness = Functions.ComputeFitness(new PredictorWithInputs(rbfNet, data.Input), data);
        Trace.WriteLine("RBF fitness: " + fitness);
        return fitness;
      };

      fitnessForTolerance(0.2).ShouldBeGreaterThan(0.9);
      fitnessForTolerance(0.01).ShouldEqual(1.0);
    }
  }
}
