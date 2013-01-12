﻿using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quqe;
using PCW;
using System.Diagnostics;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Threading.Tasks;

namespace QuqeTest
{
  [TestClass]
  public class Tests
  {
    [TestMethod]
    public void SMA1()
    {
      var ls = List.Create(1, 4, 6, 2, 4, 7, 2, 4);
      //var sma = new DataSeries<Value>("", ls.Select(x => new Value(DateTime.MinValue, x))).SMA(3);
      //Assert.IsTrue(List.Equal(sma.Select(x => x.Val), new double[] { 1, 5.0 / 2.0, 11.0 / 3.0, 4, 4, 13.0 / 3.0, 13.0 / 3.0, 13.0 / 3.0 }));
      //Trace.WriteLine("in: " + ls.ToLisp());
      //Trace.WriteLine("out: " + sma.Select(x => x.Val).ToLisp());


      var zlema = new DataSeries<Value>("", ls.Select(x => new Value(DateTime.MinValue, x))).ZLEMA(3);
      Trace.WriteLine("in: " + ls.ToLisp());
      Trace.WriteLine("out: " + zlema.Select(x => x.Val).ToLisp());
    }

    [TestMethod]
    public void Extrapolate1()
    {
      var ls = List.Create<double>(1, 2, 4, 9, 16);
      var ds = new DataSeries<Value>("", ls.Select(x => new Value(DateTime.MinValue, x)));
      var ex = ds.Extrapolate().Select(x => x.Val).ToLisp();
    }

    [TestMethod]
    public void Drawdown()
    {
      var dd = BacktestHelper.CalcMaxDrawdownPercent(new DataSeries<Value>("Foo", List.Create(1.0, 2, 3, 4, 7, 5, 4, 5, 2, 10, 12, 11, 15).Select(x => new Value(DateTime.MinValue, x))));
      Assert.IsTrue(dd == (7 - 2) / 7.0);
    }

    [TestMethod]
    public void Accounting1()
    {
      var a = new Account { Equity = 10000, MarginFactor = 1 };
      var com = 5;
      a.Commission = size => com;
      var bars = new DataSeries<Bar>("ABCD", List.Create(
        new Bar(DateTime.Parse("12/10/2010"), 21.63, 21.50, 23.01, 22.90, 10000), // long profit
        new Bar(DateTime.Parse("12/11/2010"), 23.50, 22.00, 24.01, 24.00, 10000), // long stop loss, green bar
        new Bar(DateTime.Parse("12/12/2010"), 25.00, 24.00, 25.01, 24.01, 10000), // long loss, red bar
        new Bar(DateTime.Parse("12/13/2010"), 23.90, 23.10, 23.95, 23.15, 10000), // short profit
        new Bar(DateTime.Parse("12/14/2010"), 23.10, 22.50, 23.50, 22.60, 10000), // short stop loss, red bar
        new Bar(DateTime.Parse("12/15/2010"), 22.39, 22.20, 24.00, 23.50, 10000))); // short loss, green bar
      var actions = new Queue<Action<DataSeries<Bar>>>(List.Create<Action<DataSeries<Bar>>>(
        s => a.EnterLong("ABCD", (int)Math.Floor((a.BuyingPower - com) / s[0].Open), new ExitOnSessionClose(21.40), s.FromHere()),  // new Equity: 10576.74
        s => a.EnterLong("ABCD", (int)Math.Floor((a.BuyingPower - com) / s[0].Open), new ExitOnSessionClose(23.00), s.FromHere()),  // new Equity: 10342.24
        s => a.EnterLong("ABCD", (int)Math.Floor((a.BuyingPower - com) / s[0].Open), new ExitOnSessionClose(23.90), s.FromHere()),  // new Equity: 9923.37
        s => a.EnterShort("ABCD", (int)Math.Floor((a.BuyingPower - com) / s[0].Open), new ExitOnSessionClose(24.00), s.FromHere()), // new Equity: 10223.87
        s => a.EnterShort("ABCD", (int)Math.Floor((a.BuyingPower - com) / s[0].Open), new ExitOnSessionClose(23.25), s.FromHere()), // new Equity: 10147.57
        s => a.EnterShort("ABCD", (int)Math.Floor((a.BuyingPower - com) / s[0].Open), new ExitOnSessionClose(24.10), s.FromHere())  // new Equity: 9635.85
        ));

      var expectedEquity = List.Create<double>(10576.74, 10342.24, 9923.37, 10223.87, 10147.57, 9635.85);
      var actualEquity = new List<double>();
      DataSeries.Walk(bars, pos => {
        actions.Dequeue()(bars);
        actualEquity.Add(a.Equity);
      });

      Assert.IsTrue(List.Equal(expectedEquity, actualEquity.Select(x => Math.Round(x, 2))));
    }

    [TestMethod]
    public void Accounting2()
    {
      var a = new Account { Equity = 10000, MarginFactor = 4 };
      Assert.IsTrue(a.BuyingPower == 40000);
      var com = 5;
      a.Commission = size => com;
      var bars = new DataSeries<Bar>("ABCD", List.Create(
        new Bar(DateTime.Parse("12/10/2010"), 21.63, 21.50, 23.01, 22.90, 10000), // long profit
        new Bar(DateTime.Parse("12/13/2010"), 23.90, 23.10, 23.95, 23.15, 10000))); // short profit
      var actions = new Queue<Action<DataSeries<Bar>>>(List.Create<Action<DataSeries<Bar>>>(
        s => a.EnterLong("ABCD", (int)Math.Floor((a.BuyingPower - com * 4) / s[0].Open), new ExitOnSessionClose(21.00), s.FromHere()),  // new Equity: 12336.96
        s => a.EnterShort("ABCD", (int)Math.Floor((a.BuyingPower - com * 4) / s[0].Open), new ExitOnSessionClose(24.00), s.FromHere())  // new Equity: 13874.21
        ));

      var expectedEquity = List.Create<double>(12336.96, 13874.21);
      var actualEquity = new List<double>();
      DataSeries.Walk(bars, pos => {
        actions.Dequeue()(bars);
        actualEquity.Add(a.Equity);
      });

      Assert.IsTrue(List.Equal(expectedEquity, actualEquity.Select(x => Math.Round(x, 2))));
    }

    [TestMethod]
    public void Accounting3()
    {
      var a = new Account { Equity = 10000, MarginFactor = 4 };
      var com = 5;
      a.Commission = size => com;
      var bars = new DataSeries<Bar>("ABCD", List.Create(
        new Bar(DateTime.Parse("12/10/2010"), 21.63, 21.50, 23.01, 22.90, 10000), // long profit
        new Bar(DateTime.Parse("12/13/2010"), 23.90, 23.10, 23.95, 23.15, 10000))); // short profit

      bool threw = false;
      try
      {
        a.EnterLong("ABCD", 1850, new ExitOnSessionClose(20.00), bars.FromHere());
      }
      catch (InvalidOperationException)
      {
        threw = true;
      }

      Assert.IsTrue(threw);
    }

    [TestMethod]
    public void Mesh1()
    {
      var a = new ListHolder<double> { List = List.Create(1.0, 2, 4, 5, 6, 7, 8, 9) };
      var b = new ListHolder<double> { List = List.Create(1.1, 2.1, 3.1) };
      var c = new ListHolder<double> { List = List.Create(3.2, 4.2, 5.2, 6.2) };
      var d = new ListHolder<double> { List = List.Create(4.3, 5.3) };
      var e = new ListHolder<double> { List = List.Create(8.4, 9.4) };
      //var a = List.Create(1.0, 2, 3, 4, 5, 6, 7, 8, 9);
      //var b = List.Create(1.1, 2.1, 3.1);
      //var c = List.Create(3.2, 4.2, 5.2, 6.2);
      //var d = List.Create(4.3, 5.3);
      //var e = List.Create(8.4, 9.4);

      var m = List.Mesh(List.Create(a, b, c, d, e), x => x.List, n => (int)n, i => i + 1, (dummy, xs) => new List<double>(xs)).ToList();
      var m2 = List.Mesh(List.Create(b, e), x => x.List, n => (int)n, i => i + 1, (dummy, xs) => new List<double>(xs)).ToList();
    }

    class ListHolder<T> : IEquatable<ListHolder<T>>
    {
      public IEnumerable<T> List;

      public bool Equals(ListHolder<T> other)
      {
        return this == other;
      }
    }

    [TestMethod]
    public void MakeSMATest1()
    {
      var sma = Optimizer.MakeSMA(3);
      var n = sma(1);
      Assert.AreEqual(n, 1);
      n = sma(3);
      Assert.AreEqual(n, 2);
      n = sma(8);
      Assert.AreEqual(n, 4);
      n = sma(4);
      Assert.AreEqual(n, 5);
    }

    [TestMethod]
    public void QuantizeTest()
    {
      Assert.IsTrue(Optimizer.Quantize(7, 2, 1) == 7);
      Assert.IsTrue(Optimizer.Quantize(7, 0, 2) == 8);
      Assert.IsTrue(Optimizer.Quantize(1.4, 1.3, 0.3) == 1.3);
      Assert.IsTrue(Optimizer.Quantize(1.5, 1.3, 0.3) == 1.6);
    }

    [TestMethod]
    public void CookieBagTest()
    {
      var cb = new CookieBag<string>();
      var a = cb.Add("A");
      var b = cb.Add("B");
      var c = cb.Add("C");
      Assert.IsTrue(a == 0);
      Assert.IsTrue(b == 1);
      Assert.IsTrue(c == 2);
      Assert.IsTrue(cb.Get(a) == "A");
      Assert.IsTrue(cb.Get(b) == "B");
      Assert.IsTrue(cb.Get(c) == "C");
      cb.Remove(b);
      var d = cb.Add("D");
      Assert.IsTrue(d == 1);
      var e = cb.Add("E");
      Assert.IsTrue(e == 3);
    }

    [TestMethod]
    public void SvdTest()
    {
      var points = List.Create<Vector>(
        new DenseVector(new double[] { 3.1, 2.9 }),
        new DenseVector(new double[] { 1.0, 0.99 }),
        new DenseVector(new double[] { 2.2, 2.1 }),
        new DenseVector(new double[] { 3.8, 4.1 }),
        new DenseVector(new double[] { 4.4, 4.5 }),
        new DenseVector(new double[] { 6.1, 6.0 }));
      var X = new DenseMatrix(points.Count, 2);
      for (int i = 0; i < points.Count; i++)
        X.SetRow(i, points[i]);
      var svd = X.Svd(true);
      var V = svd.VT().Transpose();
      Trace.WriteLine(V);

      var a = points[3];
      var p1 = V.Column(0);
      var b = a.DotProduct(p1) * p1;
      var p2 = V.Column(1);
      var c = a.DotProduct(p2) * p2;
    }

    [TestMethod]
    public void TestBase64Doubles()
    {
      var ds1 = Optimizer.RandomVector(100, double.MinValue, double.MaxValue).ToList();
      var s1 = VersaceResult.DoublesToBase64(ds1);
      var ds2 = VersaceResult.DoublesFromBase64(s1);
      var s2 = VersaceResult.DoublesToBase64(ds2);
      Assert.IsTrue(s1 == s2);
    }

    [TestMethod]
    public void OrthoTest()
    {
      var w = RBFNet.Orthogonalize(new DenseVector(new double[] { 1, 1, 0 }), new List<Vector> {
        new DenseVector(new double[] { 1, 0, 0 })
      });
      var w2 = RBFNet.Orthogonalize(new DenseVector(new double[] { 1, 1, 1 }), new List<Vector> {
        new DenseVector(new double[] { 1, 0, 0 }),
        w
      });
    }

    [TestMethod]
    public void DirWatch()
    {
      var dw = new DirectoryWatcher<object>(@"c:\users\wintonpc", "*", fn => fn);
      SyncContext.Current.Run();
    }

    [TestMethod]
    public void ListPartitioning()
    {
      var letters = "abcdefghijklmnopqrstuvwxyz".ToList();
      var subs = letters.PartitionByIndex(0, 6, 7, 9, letters.Count).Select(ls => new string(ls.ToArray())).ToList();
      Assert.AreEqual(subs[0], "abcdef");
      Assert.AreEqual(subs[1], "g");
      Assert.AreEqual(subs[2], "hi");
      Assert.AreEqual(subs[3], "jklmnopqrstuvwxyz");
    }

    [TestMethod]
    public void Retrain()
    {
      //var vr = VersaceResult.Load(@"C:\Users\wintonpc\git\Quqe\Share\VersaceResults\VersaceResult-20121030-213048.xml");
      var vr = VersaceResult.Load(@"C:\Users\wintonpc\git\Quqe\Share\VersaceResults\VersaceResult-20121107-174940.xml");
      Versace.Settings = vr.VersaceSettings;
      var mixture = vr.BestMixture;
      mixture.Dump();

      DumpFitnessDetail(mixture);
      RBFNet.ShouldTrace = false;
      RNN.ShouldTrace = false;
      while (true)
      {
        Parallel.ForEach(mixture.Members.Select(m => m.Expert), e => e.TrainEx(preserveTrainingInit: true));
        DumpFitnessDetail(mixture);
      }
    }

    static void DumpFitnessDetail(VMixture mixture)
    {
      var fitnesses = new List<double>();
      for (int i = 0; i < mixture.Members.Count; i++)
      {
        var fitness = VMixture.ComputeFitness(mixture.Members[i].Expert);
        fitnesses.Add(fitness);
      }
      fitnesses.Add(VMixture.ComputeFitness(mixture));
      Trace.WriteLine(fitnesses.Join("\t", f => (f * 100).ToString("N1")));
    }
  }
}
