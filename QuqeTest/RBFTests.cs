using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Machine.Specifications;
using Quqe;
using Quqe.NewVersace;
using List = PCW.List;
using System.Diagnostics;

namespace QuqeTest
{
  [TestFixture]
  class RBFTests
  {
    [Test]
    public void RBFTrainingIsReproducible()
    {
      var data = NNTestUtils.GetData("2004-01-01", "2004-05-01");
      var fitnesses = List.Repeat(5, i => {
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
