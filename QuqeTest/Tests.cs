using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quqe;
using PCW;
using Backtest;

namespace QuqeTest
{
  [TestClass]
  public class Tests
  {
    [TestMethod]
    public void TestMethod1()
    {
      var nn = new NeuralNet(
        new[] { "Open0", "Open1", "Low1", "High1", "Close1" },
        new[] { 5, 3 },
        new[] { "Bias" });
      var r = nn.Propagate(new double[] { 0, 0, 0, 0, 0 });

    }

    [TestMethod]
    public void Serializing()
    {
      var nn1 = new NeuralNet(
        new[] { "Open0", "Open1", "Low1", "High1", "Close1" },
        new[] { 5, 3 },
        new[] { "Bias" });
      var s1 = nn1.ToString();
      var nn2 = NeuralNet.FromString(s1);
      var s2 = nn2.ToString();
      Assert.AreEqual(s1, s2);
    }

    [TestMethod]
    public void DotProd1()
    {
      Assert.AreEqual(NeuralNet.DotProduct(new double[0], new double[0]), 0);
      Assert.AreEqual(NeuralNet.DotProduct(new double[] { 1, 2, 3 }, new double[] { 1, 0.5, 3 }), 11);

      bool threw = false;
      try
      {
        NeuralNet.DotProduct(new double[] { 1, 2 }, new double[] { 1, 2, 3 });
      }
      catch (ArgumentException)
      {
        threw = true;
      }
      Assert.IsTrue(threw);

      threw = false;
      try
      {
        NeuralNet.DotProduct(new double[] { 1, 2, 3 }, new double[] { 1, 2 });
      }
      catch (ArgumentException)
      {
        threw = true;
      }
      Assert.IsTrue(threw);
    }

    //[TestMethod]
    //public void SMA1()
    //{
    //  var sma = Indicators.SMA(3, new double[] { 1, 4, 6, 2, 4, 7, 2, 4 });
    //  Assert.IsTrue(List.Equal(sma, new double[] { 1, 5.0 / 2.0, 11.0 / 3.0, 4, 4, 13.0 / 3.0, 13.0 / 3.0, 13.0 / 3.0 }));
    //}

    [TestMethod]
    public void Drawdown()
    {
      var dd = Backtester.CalcMaxDrawdownPercent(new DataSeries<Value>("Foo", List.Create(1.0, 2, 3, 4, 7, 5, 4, 5, 2, 10, 12, 11, 15).Select(x => new Value(DateTime.MinValue, x))));
      Assert.IsTrue(dd == (7 - 2) / 7.0);
    }
  }
}
