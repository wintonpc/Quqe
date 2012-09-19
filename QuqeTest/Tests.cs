using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quqe;
using PCW;
using System.Diagnostics;
using MathNet.Numerics.LinearAlgebra.Double;

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

    enum TipSize { None, Small, Large }
    enum FoodGood { Yes, No }
    enum ServiceGood { Yes, No }
    enum DayOfWeek { Mon, Tue, Wed, Thu }

    [TestMethod]
    public void DecisionTree1()
    {
      var examples = List.Create(
        new DtExample(null, TipSize.None, FoodGood.No, ServiceGood.No, DayOfWeek.Mon),
        new DtExample(null, TipSize.Small, FoodGood.Yes, ServiceGood.No, DayOfWeek.Tue),
        new DtExample(null, TipSize.Small, FoodGood.No, ServiceGood.Yes, DayOfWeek.Wed),
        new DtExample(null, TipSize.Large, FoodGood.Yes, ServiceGood.Yes, DayOfWeek.Thu),
        new DtExample(null, TipSize.Large, FoodGood.Yes, ServiceGood.No, DayOfWeek.Thu));

      var dt = DecisionTree.Learn(examples, TipSize.Small, 0);

      DecisionTree.WriteDot(@"c:\Users\Wintonpc\git\Quqe\Share\dt.dot", dt);

    }

    [TestMethod]
    public void DecisionTree3()
    {
      Func<string, string, IEnumerable<DtExample>> makeExamples = (start, end) => {
        return DtSignals.MakeCandleExamples(Data.Get("TQQQ").From(start).To(end),
        smallMax: 0.65,
        mediumMax: 1.21,
        gapPadding: 0,
        superGapPadding: 0.4,
        enableEma: 1,
        emaPeriod: 3,
        enableMomentum: 0,
        momentumPeriod: 19,
        enableLrr2: 0,
        enableLinRegSlope: 0,
        linRegSlopePeriod: 14,
        enableRSquared: 0,
        rSquaredPeriod: 8,
        rSquaredThresh: 0.75
        );
      };

      //var teachingSet = makeExamples("02/12/2010", "12/31/2011");
      //var validationSet = makeExamples("01/01/2012", "07/15/2012");
      //var teachingSet = makeExamples("02/11/2010", "07/18/2012");
      //var validationSet = makeExamples("02/11/2010", "07/18/2012");
      var teachingSet = makeExamples("02/11/2010", "12/31/2011");
      var validationSet = makeExamples("01/01/2012", "07/18/2012");

      var dt = DecisionTree.Learn(teachingSet, Prediction.Green, 0.51);
      DecisionTree.WriteDot(@"c:\Users\Wintonpc\git\Quqe\Share\dt.dot", dt);

      foreach (var set in List.Create(teachingSet, validationSet))
      {
        var numCorrect = 0;
        var numIncorrect = 0;
        var numUnsure = 0;
        foreach (var e in set)
        {
          var decision = DecisionTree.Decide(e.AttributesValues, dt);
          if (decision.Equals(e.Goal))
            numCorrect++;
          else if (decision is string && (string)decision == "Unsure")
            numUnsure++;
          else
            numIncorrect++;
        }

        var accuracy = (double)numCorrect / (numCorrect + numIncorrect);
        var confidence = (double)(numCorrect + numIncorrect) / set.Count();
        var altAccuracy = 0.50;
        Trace.WriteLine("NumCorrect: " + numCorrect);
        Trace.WriteLine("NumIncorrect: " + numIncorrect);
        Trace.WriteLine("NumUnsure: " + numUnsure);
        Trace.WriteLine("Accuracy: " + accuracy);
        Trace.WriteLine("Confidence: " + confidence);
        Trace.WriteLine("Overall Quality: " + (accuracy * confidence + altAccuracy * (1 - confidence)));
        Trace.WriteLine("---");
      }
    }


    [TestMethod]
    public void DecisionTree2()
    {
      var oParams = List.Create(
        new OptimizerParameter("MinMajority", 0.50, 0.54, 0.01),
        new OptimizerParameter("SmallMax", 0.65, 0.65, 0.01),
        new OptimizerParameter("MediumMax", 1.21, 1.21, 0.01),
        new OptimizerParameter("SmallMaxPct", -0.09, -0.09, 0.01),
        new OptimizerParameter("LargeMinPct", 0.3, 0.3, 0.01),
        new OptimizerParameter("EnableBarSizeAveraging", 0, 0, 1),
        new OptimizerParameter("GapPadding", 0.0, 0.0, 0.03),
        new OptimizerParameter("SuperGapPadding", 0.4, 0.4, 0.02),
        new OptimizerParameter("EnableEma", 1, 1, 1),
        new OptimizerParameter("EmaPeriod", 3, 5, 1),
        new OptimizerParameter("EnableMomentum", 0, 0, 1),
        new OptimizerParameter("MomentumPeriod", 19, 19, 1),
        new OptimizerParameter("EnableLrr2", 0, 0, 1),
        new OptimizerParameter("EnableLinRegSlope", 0, 0, 1),
        new OptimizerParameter("LinRegSlopePeriod", 10, 10, 1),
        new OptimizerParameter("EnableRSquared", 0, 0, 1),
        new OptimizerParameter("RSquaredPeriod", 10, 10, 1),
        new OptimizerParameter("RSquaredThresh", 0.5, 0.5, 4)
        );

      var reports = Optimizer.OptimizeStrategyParameters(oParams, sParams => {
        //var learningSet = DtSignals.MakeExamples(Data.Get("TQQQ").To("12/31/2011"));
        //var validationSet = DtSignals.MakeExamples(Data.Get("TQQQ").From("01/01/2012"));
        var teachingBars = Data.Get("TQQQ").To("12/31/2011");
        var validationBars = Data.Get("TQQQ").From("01/01/2012");

        Func<DataSeries<Bar>, IEnumerable<DtExample>> makeExamples = bars =>
          DtSignals.MakeCandleExamples(bars,
          smallMax: sParams.Get<double>("SmallMax"),
          mediumMax: sParams.Get<double>("MediumMax"),
          enableBarSizeAveraging: sParams.Get<int>("EnableBarSizeAveraging"),
          smallMaxPct: sParams.Get<double>("SmallMaxPct"),
          largeMinPct: sParams.Get<double>("LargeMinPct"),
          gapPadding: sParams.Get<double>("GapPadding"),
          superGapPadding: sParams.Get<double>("SuperGapPadding"),
          enableEma: sParams.Get<int>("EnableEma"),
          emaPeriod: sParams.Get<int>("EmaPeriod"),
          enableMomentum: sParams.Get<int>("EnableMomentum"),
          momentumPeriod: sParams.Get<int>("MomentumPeriod"),
          enableLrr2: sParams.Get<int>("EnableLrr2"),
          enableLinRegSlope: sParams.Get<int>("EnableLinRegSlope"),
          linRegSlopePeriod: sParams.Get<int>("LinRegSlopePeriod"),
          enableRSquared: sParams.Get<int>("EnableRSquared"),
          rSquaredPeriod: sParams.Get<int>("RSquaredPeriod"),
          rSquaredThresh: sParams.Get<double>("RSquaredThresh")
          );

        var teachingSet = makeExamples(teachingBars);
        var validationSet = makeExamples(validationBars);

        var dt = DecisionTree.Learn(teachingSet, Prediction.Green, sParams.Get<double>("MinMajority"));

        //DecisionTree.WriteDot(@"c:\Users\Wintonpc\git\Quqe\Share\dt.dot", dt);

        foreach (var set in List.Create(/*teachingSet,*/ validationSet))
        {
          var numCorrect = 0;
          var numIncorrect = 0;
          var numUnsure = 0;
          foreach (var e in set)
          {
            var decision = DecisionTree.Decide(e.AttributesValues, dt);
            if (decision.Equals(e.Goal))
              numCorrect++;
            else if (decision is string && (string)decision == "Unsure")
              numUnsure++;
            else
              numIncorrect++;
          }

          var accuracy = (double)numCorrect / (numCorrect + numIncorrect);
          var confidence = (double)(numCorrect + numIncorrect) / set.Count();
          var quality = accuracy * confidence;
          var altAccuracy = 0.57;
          //Trace.WriteLine("NumCorrect: " + numCorrect);
          //Trace.WriteLine("NumIncorrect: " + numIncorrect);
          //Trace.WriteLine("NumUnsure: " + numUnsure);
          //Trace.WriteLine("Accuracy: " + accuracy);
          //Trace.WriteLine("Confidence: " + confidence);
          //Trace.WriteLine("Quality: " + quality);
          //Trace.WriteLine("---");
          //if (set == teachingSet)
          return new StrategyOptimizerReport {
            StrategyName = "DecisionTree",
            StrategyParams = sParams,
            Fitness = accuracy * confidence + altAccuracy * (1 - confidence)
          };
        }
        throw new Exception();
      });

      Strategy.PrintStrategyOptimizerReports(reports.OrderByDescending(x => x.Fitness));
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
    public void Elman1()
    {
      var net = new ElmanNet(4, List.Create(3, 2), 1);
      var output = net.Propagate(new double[] { 0.1, 0.2, 0.3, 0.4 });
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
    public void GramSchmidtTest()
    {
      var points = List.Create<Vector>(
        new DenseVector(new double[] { 1, 2 }),
        new DenseVector(new double[] { 3, 4 }),
        new DenseVector(new double[] { 5, 6 }));
      var P = new DenseMatrix(points.Count, points.First().Count);
      for (int i = 0; i < points.Count; i++)
        P.SetRow(i, points[i]);

      var gs = RBFNet.GramSchmidt(P);
      Trace.WriteLine("With Gram-Schmidt:");
      Trace.WriteLine("P = \r\n" + P);
      Trace.WriteLine("W = \r\n" + gs.W);
      Trace.WriteLine("A = \r\n" + gs.A);
      Trace.WriteLine("P' = \r\n" + (gs.W * gs.A));
    }

    [TestMethod]
    public void QRDecompTest()
    {
      var points = List.Create<Vector>(
        new DenseVector(new double[] { 1, 2 }),
        new DenseVector(new double[] { 3, 4 }),
        new DenseVector(new double[] { 5, 6 }));
      var P = new DenseMatrix(points.Count, points.First().Count);
      for (int i = 0; i < points.Count; i++)
        P.SetRow(i, points[i]);

      var qr = RBFNet.QRDecomposition(P);
      Trace.WriteLine("With Gram-Schmidt QR:");
      Trace.WriteLine("P = \r\n" + P);
      Trace.WriteLine("Q = \r\n" + qr.Q);
      Trace.WriteLine("R = \r\n" + qr.R);
      Trace.WriteLine("P' = \r\n" + (qr.Q * qr.R));

      var qr1 = RBFNet.QR2(P);
      Trace.WriteLine("With QR2:");
      Trace.WriteLine("P = \r\n" + P);
      Trace.WriteLine("Q = \r\n" + qr1.Q);
      Trace.WriteLine("R = \r\n" + qr1.R);
      Trace.WriteLine("P' = \r\n" + (qr1.Q * qr1.R));

      var qr2 = P.QR();
      Trace.WriteLine("With Math.NET QR:");
      Trace.WriteLine("P = \r\n" + P);
      Trace.WriteLine("Q = \r\n" + qr2.Q);
      Trace.WriteLine("R = \r\n" + qr2.R);
      Trace.WriteLine("P' = \r\n" + (qr2.Q * qr2.R));
    }

    [TestMethod]
    public void GaussianRandoms()
    {
      List.Repeat(100, n => {
        var x = BCO.RandomGaussian(0, 100);
        Trace.WriteLine(x);
      });
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
  }
}
