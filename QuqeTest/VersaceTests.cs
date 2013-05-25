using NUnit.Framework;
using Quqe;
using Quqe.NewVersace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using List = PCW.List;

namespace QuqeTest
{
  [TestFixture]
  public class VersaceTests
  {
    [Test]
    public void IdealSignalNextClose()
    {
      var bars = new DataSeries<Bar>("", new[] {
        new Bar(5, 0, 0, 10, 0),
        new Bar(6, 0, 0, 11, 0),
        new Bar(5, 0, 0, 12, 0),
        new Bar(4, 0, 0, 11, 0),
        new Bar(3, 0, 0, 10, 0),
        new Bar(1, 0, 0, 12, 0),
        new Bar(10, 0, 0, 12, 0)
      });

      var signal = bars.MapElements<Value>((s, _) => Signals.NextClose(s)).Select(x => x.Val).ToList();
      signal.ShouldEnumerateLike(List.Create<double>(0, 1, 1, -1, -1, 1, 1)); 
    }

    [Test]
    public void TestComputePredictorFitness()
    {
      Assert.Inconclusive();
    }
  }
}
