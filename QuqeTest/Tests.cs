using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quqe;
using PCW;
using System.Diagnostics;

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

    enum TipSize { None, Small, Large }
    enum FoodGood { Yes, No }
    enum ServiceGood { Yes, No }
    enum DayOfWeek { Mon, Tue, Wed, Thu }

    [TestMethod]
    public void DecisionTree1()
    {
      var examples = List.Create(
        new DtExample(TipSize.None, FoodGood.No, ServiceGood.No, DayOfWeek.Mon),
        new DtExample(TipSize.Small, FoodGood.Yes, ServiceGood.No, DayOfWeek.Tue),
        new DtExample(TipSize.Small, FoodGood.No, ServiceGood.Yes, DayOfWeek.Wed),
        new DtExample(TipSize.Large, FoodGood.Yes, ServiceGood.Yes, DayOfWeek.Thu),
        new DtExample(TipSize.Large, FoodGood.Yes, ServiceGood.No, DayOfWeek.Thu));

      var dt = DecisionTree.Learn(examples, TipSize.Small, 0);

      DecisionTree.WriteDot(@"c:\Users\Wintonpc\git\Quqe\Share\dt.dot", dt);

    }

    [TestMethod]
    public void DecisionTree3()
    {
      Func<string, string, IEnumerable<DtExample>> makeExamples = (start, end) => {
        return DtSignals.MakeExamples(Data.Get("TQQQ").From(start).To(end),
        smallMax: 0.65,
        mediumMax: 1.21,
        gapPadding: 0,
        superGapPadding: 0.4,
        emaPeriod: 3
        );
      };

      //var teachingSet = makeExamples("02/12/2010", "12/31/2011");
      //var validationSet = makeExamples("01/01/2012", "07/15/2012");
      //var teachingSet = makeExamples("02/11/2010", "07/18/2012");
      //var validationSet = makeExamples("02/11/2010", "07/18/2012");
      var teachingSet = makeExamples("02/11/2010", "12/31/2011");
      var validationSet = makeExamples("01/01/2012", "07/17/2012");

      var dt = DecisionTree.Learn(teachingSet, DtSignals.Prediction.Green, 0.56);
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
        var altAccuracy = 0.57;
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
        new OptimizerParameter("MinMajority", 0.56, 0.56, 0.02),
        new OptimizerParameter("SmallMax", 0.65, 0.65, 0.01),
        new OptimizerParameter("MediumMax", 1.21, 1.21, 0.01),
        new OptimizerParameter("SmallMaxPct", -0.09, -0.09, 0.01),
        new OptimizerParameter("LargeMinPct", 0.3, 0.3, 0.01),
        new OptimizerParameter("EnableBarSizeAveraging", 0, 0, 1),
        new OptimizerParameter("GapPadding", 0.0, 0.0, 0.03),
        new OptimizerParameter("SuperGapPadding", 0.4, 0.4, 0.02),
        new OptimizerParameter("EnableEma", 0, 0, 1),
        new OptimizerParameter("EmaPeriod", 3, 3, 1),
        new OptimizerParameter("EnableMomentum", 0, 0, 1),
        new OptimizerParameter("MomentumPeriod", 19, 19, 1)
        );

      var reports = Optimizer.OptimizeStrategyParameters(oParams, sParams => {
        //var learningSet = DtSignals.MakeExamples(Data.Get("TQQQ").To("12/31/2011"));
        //var validationSet = DtSignals.MakeExamples(Data.Get("TQQQ").From("01/01/2012"));
        var teachingBars = Data.Get("TQQQ").To("12/31/2011");
        var validationBars = Data.Get("TQQQ").From("01/01/2012");

        Func<DataSeries<Bar>, IEnumerable<DtExample>> makeExamples = bars =>
          DtSignals.MakeExamples(bars,
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
          momentumPeriod: sParams.Get<int>("MomentumPeriod")
          );

        var teachingSet = makeExamples(teachingBars);
        var validationSet = makeExamples(validationBars);

        var dt = DecisionTree.Learn(teachingSet, DtSignals.Prediction.Green, sParams.Get<double>("MinMajority"));

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
              GenomeFitness = accuracy * confidence + altAccuracy * (1 - confidence)
            };
        }
        throw new Exception();
      });

      Strategy.PrintStrategyOptimizerReports(reports.OrderByDescending(x => x.GenomeFitness));
    }
  }
}
