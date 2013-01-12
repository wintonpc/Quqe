using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace Quqe
{
  public abstract class Strategy
  {
    public static BacktestReport BacktestSignal(DataSeries<Bar> bars, DataSeries<Value> signal, Account account, int lookback, double? maxLossPct)
    {
      return BacktestSignal(bars, signal.ToSignal(), account, lookback, maxLossPct);
    }

    public static BacktestReport BacktestSignal(DataSeries<Bar> bars, DataSeries<SignalValue> signal, Account account, int lookback, double? maxLossPct)
    {
      var bs = bars.From(signal.First().Timestamp).To(signal.Last().Timestamp);
      var helper = BacktestHelper.Start(bs, account);
      var lossDamper = 1.0;
      DataSeries.Walk(bs, signal, pos => {
        if (pos + 1 < lookback)
          return;

        if (signal[0].Bias == SignalBias.None)
          return;

        var shouldBuy = signal[0].Bias == SignalBias.Buy;
        //Trace.WriteLine(bs[0].Timestamp.ToString("yyyy-MM-dd") + " signal: " + signal[0].Val);

        double? stopLimit = null;
        if (signal[0].Stop.HasValue)
          stopLimit = signal[0].Stop;
        else if (maxLossPct.HasValue)
        {
          if (shouldBuy)
            stopLimit = (1 - maxLossPct) * bs[0].Open;
          else
            stopLimit = (1 + maxLossPct) * bs[0].Open;
        }

        var maxSize = (long)((account.BuyingPower - account.Padding) / bs[0].Open);
        var idealSize = (long)((account.BuyingPower - account.Padding) * signal[0].SizePct * lossDamper);
        var size70 = (long)((account.BuyingPower - account.Padding) / 70.00);
        //var size = size70;
        var size = Math.Min(maxSize, idealSize);
        if (size > 0)
        {
          if (shouldBuy)
            account.EnterLong(bs.Symbol, size, new ExitOnSessionClose(stopLimit), bs.FromHere());
          else
            account.EnterShort(bs.Symbol, size, new ExitOnSessionClose(stopLimit), bs.FromHere());
        }
        //if (bs[0].IsGreen != (signal[0].Bias == SignalBias.Buy) && bs[1].IsGreen != (signal[1].Bias == SignalBias.Buy))
        //  lossDamper = Math.Max(0.10, lossDamper - 0.20);
        //else
        //  lossDamper = Math.Min(1.0, lossDamper + 0.30);
      });
      return helper.Stop();
    }

    public static void WriteTrades(List<TradeRecord> trades, DateTime now, string genomeName)
    {
      var dirName = "Trades";
      if (!Directory.Exists(dirName))
        Directory.CreateDirectory(dirName);

      var fn = Path.Combine(dirName, string.Format("{0:yyyy-MM-dd-hh-mm-ss} {1}.csv", now, genomeName));

      using (var op = new StreamWriter(fn))
      {
        Action<IEnumerable<object>> writeRow = list => op.WriteLine(list.Join(","));

        writeRow(List.Create("Symbol", "Size", "EntryTime", "ExitTime", "Position", "Entry", "StopLimit", "Exit",
          "Profit", "Loss", "PercentProfit", "PercentLoss"));

        foreach (var t in trades)
          writeRow(List.Create<object>(t.Symbol, t.Size, t.EntryTime, t.ExitTime, t.PositionDirection, t.Entry, t.StopLimit, t.Exit,
            t.Profit, t.Loss, t.PercentProfit, t.PercentLoss));
      }
    }

    protected static List<DataSeries<T>> TrimInputs<T>(IEnumerable<DataSeries<T>> inputs)
      where T : DataSeriesElement
    {
      var firstDate = inputs.Select(x => x.Elements.First().Timestamp).Max();
      return inputs.Select(x => x.From(firstDate)).ToList();
    }

    protected static List<DataSeries> TrimInputs(params DataSeries[] inputs)
    {
      var firstDate = inputs.Select(x => x.Elements.First().Timestamp).Max();
      return inputs.Select(x => x.FromDate(firstDate)).ToList();
    }

    public static void PrintStrategyOptimizerReports(IEnumerable<StrategyOptimizerReport> reports)
    {
      var best = reports.First();
      Trace.WriteLine("-- " + best.StrategyName + " --------------");

      Trace.WriteLine("=== BEST ===");
      Trace.WriteLine(best.ToString());
      Trace.WriteLine("(Saved as " + best.Save(best.StrategyName) + ")");
      Trace.WriteLine("");

      Trace.WriteLine("=== ALL ===");
      Action<IEnumerable<StrategyOptimizerReport>> printReports = list => {
        foreach (var r in list)
          Trace.WriteLine(r.ToString() + "\r\n");
      };

      printReports(reports);
      Trace.WriteLine("--------------------------------");
    }
  }

  public abstract class BasicStrategy
  {
    public readonly List<StrategyParameter> SParams;

    protected BasicStrategy(IEnumerable<StrategyParameter> sParams)
    {
      SParams = sParams.ToList();
    }

    public abstract DataSeries<Value> MakeSignal(DateTime trainingStart, DataSeries<Bar> trainingBars, DateTime validationStart, DataSeries<Bar> validationBars);
    public virtual DataSeries<SignalValue> MakeSignal(DataSeries<Bar> bars) { throw new NotImplementedException(); }

    public static BasicStrategy Make(string strategyName, IEnumerable<StrategyParameter> sParams)
    {
      var className = "Quqe." + strategyName + "Strategy";
      var type = typeof(Strategy).Assembly.GetType(className);
      var ctor = type.GetConstructor(new[] { typeof(IEnumerable<StrategyParameter>) });
      return (BasicStrategy)ctor.Invoke(new object[] { sParams });
    }
  }

  public class FooStrategy : BasicStrategy
  {
    public FooStrategy(IEnumerable<StrategyParameter> sParams) : base(sParams) { }

    public override DataSeries<Value> MakeSignal(DateTime trainingStart, DataSeries<Bar> trainingBars, DateTime validationStart, DataSeries<Bar> validationBars)
    {
      var carefulBars = validationBars.From(validationStart);
      var midSlope = carefulBars.Midpoint(x => x.WaxBottom, x => x.WaxTop).LinRegSlope(2).Delay(1);
      var topForecast = carefulBars.MapElements<Value>((s, v) => s[0].WaxTop).LinReg(2, 1).Delay(1);
      var bottomForecast = carefulBars.MapElements<Value>((s, v) => s[0].WaxBottom).LinReg(2, 1).Delay(1);
      var bs = carefulBars.From(midSlope.First().Timestamp);

      var newElements = new List<Value>();
      DataSeries.Walk(bs, midSlope, topForecast, bottomForecast, pos => {
        //double sig;
        //if (midSlope[0] < 0)
        //{
        //  //if (bs[0].Open < bottomForecast[0])
        //  //  sig = 1;
        //  //else
        //    sig = -1;
        //}
        //else
        //{
        //  //if (bs[0].Open > topForecast[0])
        //  //  sig = -1;
        //  //else
        //    sig = 1;
        //}
        //newElements.Add(new Value(bs[0].Timestamp, sig));
        if (bs.Pos == 0)
          return;
        newElements.Add(new Value(bs[0].Timestamp, bs[1].IsGreen ? 1 : -1));
      });
      return new DataSeries<Value>(bs.Symbol, newElements);
    }
  }

  public class OTPDStrategy : BasicStrategy
  {
    public OTPDStrategy(IEnumerable<StrategyParameter> sParams) : base(sParams) { }

    public override DataSeries<Value> MakeSignal(DateTime trainingStart, DataSeries<Bar> trainingBars, DateTime validationStart, DataSeries<Bar> validationBars)
    {
      throw new Exception();
      //var tqqq = Data.Get("QQQ");
      //var newElements = new List<Value>();
      //using (var ip = new StreamReader("c.txt"))
      //{
      //  string line;
      //  while ((line = ip.ReadLine()) != null)
      //  {
      //    var toks = Regex.Split(line, @"\s+");
      //    if (toks[0] == "")
      //      continue;
      //    var timestamp = DateTime.ParseExact(toks[0], "M/d/yyyy", null);
      //    var numShares = int.Parse(toks[1]);
      //    var profit = double.Parse(toks[2]);

      //    var profitPerShare = profit / numShares;
      //    var bar = tqqq.FirstOrDefault(x => x.Timestamp == timestamp);
      //    if (bar == null)
      //      continue;
      //    var exitPrice = bar.Open + profitPerShare;
      //    if (bar.IsGreen && profit > 0)
      //    {
      //      Trace.WriteLine(timestamp.ToString("MM/dd/yyyy") + " " + Math.Round(bar.WaxHeight(), 2).ToString("N2") + " "
      //        + Math.Round(profitPerShare, 2).ToString("N2") + " " + (profitPerShare / bar.WaxHeight()));
      //    }
      //  }
      //}
      //return new DataSeries<Value>(tqqq.Symbol, newElements);
    }

    public static List<TradeRecord> GetTrades(bool forceEntryToOpen = false)
    {
      var qqq = Data.Get("QQQ");
      var trades = new List<TradeRecord>();
      double accountValue = 50000;
      using (var ip = new StreamReader("c.txt"))
      {
        string line;
        while ((line = ip.ReadLine()) != null)
        {
          var toks = Regex.Split(line, @"\s+");
          if (toks[0] == "")
            continue;
          var timestamp = DateTime.ParseExact(toks[0], "M/d/yyyy", null);
          var qqqGain = double.Parse(toks[1]);
          int numQqqShares;
          double profitWith12TimesMargin;
          if (forceEntryToOpen)
          {
            numQqqShares = (int)(accountValue / 70);
            profitWith12TimesMargin = numQqqShares * qqqGain * 12;
          }
          else
          {
            numQqqShares = int.Parse(toks[2]);
            profitWith12TimesMargin = double.Parse(toks[3]);
          }

          var bar = qqq.FirstOrDefault(x => x.Timestamp == timestamp);
          if (bar == null)
            continue;
          bool gained = qqqGain > 0;
          var positionDirection = gained ^ bar.IsGreen ? PositionDirection.Short : PositionDirection.Long;
          var exitPrice = Math.Round(bar.Open + (positionDirection == PositionDirection.Long ? 1 : -1) * qqqGain, 2);
          var entryPrice = bar.Open;
          if (!Between(exitPrice, bar.WaxBottom, bar.WaxTop) && profitWith12TimesMargin >= 0)
          {
            if (positionDirection == PositionDirection.Long)
            {
              entryPrice -= exitPrice - bar.Close;
              exitPrice = bar.Close;
            }
            else
            {
              entryPrice += bar.Close - exitPrice;
              exitPrice = bar.Close;
            }
          }
          if (forceEntryToOpen)
          {
            entryPrice = bar.Open;
            if (qqqGain >= 0)
              exitPrice = bar.Close;
            profitWith12TimesMargin = (positionDirection == PositionDirection.Long ? 1 : -1) * (exitPrice - entryPrice) * numQqqShares * 12;
          }
          var prevAccountValue = accountValue;
          accountValue += profitWith12TimesMargin;
          double stopPrice;
          if (Math.Abs(exitPrice - bar.Close) < 0.03)
          {
            if (positionDirection == PositionDirection.Long)
              stopPrice = bar.Low - 0.10;
            else
              stopPrice = bar.High + 0.10;
          }
          else
            stopPrice = exitPrice;
          trades.Add(new TradeRecord("QQQ", positionDirection, entryPrice, stopPrice, exitPrice, bar.Close, bar.Timestamp.AddHours(9.5),
            bar.Timestamp.AddHours(16), numQqqShares, profitWith12TimesMargin, prevAccountValue, accountValue));
          //if (exitPrice < bar.Low - 0.02 || exitPrice > bar.High + 0.02)
          if (Math.Abs(exitPrice - bar.Open) > bar.High - bar.Low)
            Trace.WriteLine(string.Format("{0:MM/dd/yyyy} exit price of {1:N2} doesn't make sense", timestamp, exitPrice));
          //Trace.WriteLine(string.Format("{0:MM/dd/yyyy} {1} {2} {3:N2} -> {4:N2}",
          //  timestamp,
          //  positionDirection,
          //  gained ? "won" : "lost",
          //  bar.Open, exitPrice
          //  ));
        }
      }

      var revisedTrades = (from q in qqq.From(trades.First().EntryTime.Date).To(trades.Last().EntryTime.Date)
                           join t in trades on q.Timestamp equals t.EntryTime.Date into z
                           from t2 in z.DefaultIfEmpty()
                           select t2 ?? new TradeRecord("QQQ", PositionDirection.Long, q.Open, 0, q.Open, 0,
                             q.Timestamp.AddHours(9.5), q.Timestamp.AddHours(16), 1, 0, 0, 0)).ToList();
      for (int i = 1; i < revisedTrades.Count; i++)
      {
        var current = revisedTrades[i];
        if (current.Size == 1)
        {
          var last = revisedTrades[i - 1];
          revisedTrades[i] = new TradeRecord(last.Symbol, last.PositionDirection, last.Entry, last.StopLimit,
            last.Exit, last.UnstoppedExitPrice, last.EntryTime, last.ExitTime, 1, 0, last.AccountValueAfterTrade, last.AccountValueAfterTrade);
        }
      }
      return revisedTrades;
    }


    public static List<TradeRecord> GetTrades(bool includeGains, bool includeLosses, bool fixSlippage, bool assume70,
      double initialAccountValue = 50000, int margin = 12)
    {
      var qqq = Data.Get("QQQ");
      var trades = new List<TradeRecord>();
      double accountValue = initialAccountValue;
      using (var ip = new StreamReader("c.txt"))
      {
        string line;
        while ((line = ip.ReadLine()) != null)
        {
          var toks = Regex.Split(line, @"\s+");
          if (toks[0] == "")
            continue;
          var timestamp = DateTime.ParseExact(toks[0], "M/d/yyyy", null);
          var qqqGain = double.Parse(toks[1]);
          var bar = qqq.FirstOrDefault(x => x.Timestamp == timestamp);
          if (bar == null)
            continue;
          var size = (long)(accountValue / (assume70 ? 70 : bar.Open));

          TradeRecord trade = null;
          if (includeGains && qqqGain >= 0)
          {
            var entry = bar.Open;
            var exit = fixSlippage ? bar.Close : (bar.IsGreen ? entry + qqqGain : entry - qqqGain);
            var stop = bar.IsGreen ? bar.Low - 0.10 : bar.High + 0.10;
            var profit = (bar.IsGreen ? (exit - entry) : (entry - exit)) * size * margin;
            var previousAccountValue = accountValue;
            accountValue += profit;
            trade = new TradeRecord(qqq.Symbol, bar.IsGreen ? PositionDirection.Long : PositionDirection.Short, entry, stop, exit,
              bar.Close, bar.Timestamp.AddHours(9.5), bar.Timestamp.AddHours(16), size, profit, previousAccountValue, accountValue);
          }
          else if (includeLosses && qqqGain < 0)
          {
            var entry = bar.Open;
            var pd = bar.IsGreen ? PositionDirection.Short : PositionDirection.Long;
            var exit = pd == PositionDirection.Long ? (entry + qqqGain) : (entry - qqqGain);
            var stop = exit;
            var profit = (pd == PositionDirection.Long ? (exit - entry) : (entry - exit)) * size * margin;
            var previousAccountValue = accountValue;
            accountValue += profit;
            trade = new TradeRecord(qqq.Symbol, pd, entry, stop, exit, bar.Close, bar.Timestamp.AddHours(9.5), bar.Timestamp.AddHours(16),
              size, profit, previousAccountValue, accountValue);
          }
          else
          {
            trade = new TradeRecord(qqq.Symbol, PositionDirection.Long, bar.Open, bar.Open, bar.Open, bar.Close,
              bar.Timestamp.AddHours(9.5), bar.Timestamp.AddHours(16), size, 0, accountValue, accountValue);
          }

          trades.Add(trade);
        }

        var revisedTrades = (from q in qqq.From(trades.First().EntryTime.Date).To(trades.Last().EntryTime.Date)
                             join t in trades on q.Timestamp equals t.EntryTime.Date into z
                             from t2 in z.DefaultIfEmpty()
                             select t2 ?? new TradeRecord("QQQ", PositionDirection.Long, q.Open, 0, q.Open, 0,
                               q.Timestamp.AddHours(9.5), q.Timestamp.AddHours(16), 1, 0, 0, 0)).ToList();
        for (int i = 1; i < revisedTrades.Count; i++)
        {
          var current = revisedTrades[i];
          if (current.Size == 1)
          {
            var last = revisedTrades[i - 1];
            revisedTrades[i] = new TradeRecord(last.Symbol, last.PositionDirection, last.UnstoppedExitPrice, last.UnstoppedExitPrice,
              last.UnstoppedExitPrice, last.UnstoppedExitPrice, current.EntryTime, current.ExitTime, last.Size, 0,
              last.AccountValueAfterTrade, last.AccountValueAfterTrade);
          }
        }
        return revisedTrades;
      }
    }

    static bool Between(double v, double low, double high)
    {
      return low <= v && v <= high;
    }
  }

  public class Trending1Strategy : BasicStrategy
  {
    public Trending1Strategy(IEnumerable<StrategyParameter> sParams) : base(sParams) { }

    public override DataSeries<Value> MakeSignal(DateTime trainingStart, DataSeries<Bar> trainingBars, DateTime validationStart, DataSeries<Bar> validationBars)
    {
      return this.MakeSignal(validationBars.From(validationStart)).ToSimpleSignal();
    }

    public override DataSeries<SignalValue> MakeSignal(DataSeries<Bar> bars)
    {
      var wo = SParams.Get<double>("WO");
      var wl = SParams.Get<double>("WL");
      var wh = SParams.Get<double>("WH");
      var wc = SParams.Get<double>("WC");

      var fastRegPeriod = SParams.Get<int>("FastRegPeriod");
      var slowRegPeriod = SParams.Get<int>("SlowRegPeriod");

      var rSquaredPeriod = SParams.Get<int>("RSquaredPeriod");
      var rSquaredThresh = SParams.Get<double>("RSquaredThresh");
      var linRegSlopePeriod = SParams.Get<int>("LinRegSlopePeriod");

      var wickStopPeriod = SParams.Get<int>("WickStopPeriod");
      var wickStopCutoff = SParams.Get<int>("WickStopCutoff");
      var wickStopSmoothing = SParams.Get<int>("WickStopSmoothing");

      var stopRegPeriod = SParams.Get<int>("StopRegPeriod");
      var riskATRPeriod = SParams.Get<int>("RiskATRPeriod");
      var maxAccountLossPct = SParams.Get<double>("MaxAccountLossPct");
      var km = SParams.Get<double>("M");
      var ks = SParams.Get<double>("S");

      var vs = bars.Weighted(wo, wl, wh, wc);
      var fastReg = vs.LinReg(fastRegPeriod, 1).Delay(1);
      var slowReg = vs.LinReg(slowRegPeriod, 1).Delay(1);
      var rSquared = vs.RSquared(rSquaredPeriod).Delay(1);
      var linRegSlope = vs.LinRegSlope(linRegSlopePeriod).Delay(1);
      //var wickStops = bars.WickStops(wickStopPeriod, wickStopCutoff, wickStopSmoothing).Delay(1);
      var perShareRisk = bars.OpeningWickHeight().EMA(riskATRPeriod).ZipElements<Value, Value>(bars.ATR(riskATRPeriod),
        (w, a, v) => /*km * w[0]*/    km * (ks * w[0] + (1 - ks) * a[0])).Delay(1);
      var bs = bars.From(fastReg.First().Timestamp);

      var newElements = new List<SignalValue>();
      DataSeries.Walk(
        List.Create<DataSeries>(bs, fastReg, slowReg, rSquared, linRegSlope/*, wickStops*/, perShareRisk),
        pos => {
          double sig;
          if (rSquaredPeriod > 0 && rSquared[0] > rSquaredThresh)
          {
            var slope = Math.Sign(linRegSlope[0]);
            sig = slope == 0 ? 1 : slope;
          }
          else
            sig = fastReg[0] >= slowReg[0] ? 1 : -1;

          var bias = sig >= 0 ? SignalBias.Buy : SignalBias.Sell;

          // wick-based stops
          //double relativeStop = wickStops[0];
          //double minStop = 0.10;
          //double maxLossPct = 0.025;
          //double absoluteStop;
          //if (bias == SignalBias.Buy)
          //  absoluteStop = Math.Max((1 - maxLossPct) * bs[0].Open, bs[0].Open - Math.Max(minStop, relativeStop));
          //else
          //  absoluteStop = Math.Min((1 + maxLossPct) * bs[0].Open, bs[0].Open + Math.Max(minStop, relativeStop));

          // ATR/wick stops
          var sizePct = maxAccountLossPct / perShareRisk[0];
          double absoluteStop;
          if (bias == SignalBias.Buy)
            absoluteStop = bs[0].Open - perShareRisk[0];
          else
            absoluteStop = bs[0].Open + perShareRisk[0];
          newElements.Add(new SignalValue(bs[0].Timestamp, bias, SignalTimeOfDay.Open, sizePct, absoluteStop, null));

          //newElements.Add(new SignalValue(bs[0].Timestamp, bias, null, null, null));
        });

      return new DataSeries<SignalValue>(bars.Symbol, newElements);
    }
  }
}
