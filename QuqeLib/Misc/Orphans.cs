using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Quqe.Rabbit;

namespace Quqe
{
  public static class Orphans
  {
    public static void WriteTrades(List<TradeRecord> trades, DateTime now, string genomeName)
    {
      var dirName = "Trades";
      if (!Directory.Exists(dirName))
        Directory.CreateDirectory(dirName);

      var fn = Path.Combine(dirName, string.Format("{0:yyyy-MM-dd-hh-mm-ss} {1}.csv", now, genomeName));

      using (var op = new StreamWriter(fn))
      {
        Action<IEnumerable<object>> writeRow = list => op.WriteLine(list.Join(","));

        writeRow(Lists.Create("Symbol", "Size", "EntryTime", "ExitTime", "Position", "Entry", "StopLimit", "Exit",
          "Profit", "Loss", "PercentProfit", "PercentLoss"));

        foreach (var t in trades)
          writeRow(Lists.Create<object>(t.Symbol, t.Size, t.EntryTime, t.ExitTime, t.PositionDirection, t.Entry, t.StopLimit, t.Exit,
            t.Profit, t.Loss, t.PercentProfit, t.PercentLoss));
      }
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

  public static class OTPD
  {
    public static List<TradeRecord> GetTrades(bool forceEntryToOpen = false)
    {
      var qqq = DataImport.Get("QQQ");
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
          var timestamp = DateTime.ParseExact(toks[0], "M/d/yyyy", null, DateTimeStyles.AdjustToUniversal);
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
      var qqq = DataImport.Get("QQQ");
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
          var timestamp = DateTime.ParseExact(toks[0], "M/d/yyyy", null, DateTimeStyles.AdjustToUniversal);
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
}
