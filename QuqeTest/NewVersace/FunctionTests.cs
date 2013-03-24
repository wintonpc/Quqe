using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Machine.Specifications;
using NUnit.Framework;
using Quqe;
using List = PCW.List;

namespace QuqeTest
{
  [TestFixture]
  class FunctionTests
  {
    [Test]
    public void GenericCrossOver()
    {
      var a = new[] { 1, 2, 3, 4, 5 };
      var b = new[] { 10, 20, 30, 40, 50 };

      var z = Functions.CrossOver(a, b, (x, y) => Tuple2.Create(y, x), x => x.ToList());
      z.Item1.ShouldBeOfType<List<int>>();
      z.Item2.ShouldBeOfType<List<int>>();
      z.Item1.ShouldEnumerateLike(b);
      z.Item2.ShouldEnumerateLike(a);

      var q = Functions.CrossOver(a, b, (x, y) => x % 2 == 1 ? Tuple2.Create(x, y) : Tuple2.Create(y, x), x => x.ToList());
      q.Item1.ShouldEnumerateLike(PCW.List.Create(1, 20, 3, 40, 5));
      q.Item2.ShouldEnumerateLike(PCW.List.Create(10, 2, 30, 4, 50));
    }

    [Test]
    public void SelectOneOnQuality()
    {
      QuqeUtil.Random = new Random(42);
      var stuff = new[] {
        new { A = "p", B = 1.5 },
        new { A = "q", B = 8.0 },
        new { A = "r", B = 0.5 }
      };

      var picks = new Dictionary<string, int>();
      picks["p"] = 0;
      picks["q"] = 0;
      picks["r"] = 0;

      PCW.List.Repeat(10000, _ => picks[Functions.SelectOneAccordingToQuality(stuff, x => x.B).A]++);

      picks["p"].ShouldBeGreaterThan(1400).ShouldBeLessThan(1600);
      picks["q"].ShouldBeGreaterThan(7900).ShouldBeLessThan(8100);
      picks["r"].ShouldBeGreaterThan(400).ShouldBeLessThan(600);
    }

    [Test]
    public void SelectTwoOnQuality()
    {
      var stuff = new[] {
        new { A = "p", B = 1.5 },
        new { A = "q", B = 8.0 },
        new { A = "r", B = 0.5 }
      };

      List.Repeat(10000, _ => {
        var z = Functions.SelectTwoAccordingToQuality(stuff, x => x.B);
        z.Item1.ShouldNotEqual(z.Item2);
      });
    }

    [Test]
    public void Quantization()
    {
      Functions.Quantize(2.49, 0, 1).ShouldEqual(2);
      Functions.Quantize(2.51, 0, 1).ShouldEqual(3);
      Functions.Quantize(2.51, 0.5, 1).ShouldEqual(2.5);
      Functions.Quantize(3.141592, 0, 0.001).ShouldEqual(3.142);
      Functions.Quantize(3.141492, 0, 0.001).ShouldEqual(3.141);
    }

    [Test]
    public void ChromosomeInitialization()
    {
      var protoChrom = Initialization.MakeProtoChromosome();
      var a = Initialization.RandomChromosome(NetworkType.Rnn, protoChrom);
      var b = Initialization.RandomChromosome(NetworkType.Rnn, protoChrom);
      var c = Initialization.RandomChromosome(NetworkType.Rbf, protoChrom);
      new Action(() => a.Genes.ShouldEnumerateLike(b.Genes)).ShouldThrow<SpecificationException>();
      a.NetworkType.ShouldEqual(NetworkType.Rnn);
      c.NetworkType.ShouldEqual(NetworkType.Rbf);
    }
  }
}
