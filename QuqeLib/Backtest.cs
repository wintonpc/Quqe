using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Quqe
{
  public class Backtester
  {
    Account Account;
    DataSeries<Bar> Bars;
    public Backtester(string symbol, DateTime start, DateTime end, Account account)
    {
      Bars = Data.Get(symbol).From(start).To(end);
      Account = account;
    }

    List<TradeRecord> Trades;
    List<double> AccountValues;
    public void StartRun()
    {
      AccountValues = new List<double>();
      Trades = new List<TradeRecord>();
      Account.Traded += Trades.Add;
    }

    public void UpdateAccountValue(double value)
    {
      AccountValues.Add(value);
    }

    public BacktestReport EndRun()
    {
      var accountValue = new DataSeries<Value>(Bars.Symbol, AccountValues.Select(x => (Value)x));

      return new BacktestReport {
        InputSet = Bars,
        AccountValue = accountValue,
        Trades = Trades,
        MaxDrawdownPercent = CalcMaxDrawdownPercent(accountValue)
      };
    }

    public static double CalcMaxDrawdownPercent(DataSeries<Value> accountValue)
    {
      bool inDrawdown = false;
      var last = (double)accountValue.First();
      var lastHigh = last;
      double worstInDrawdown = double.PositiveInfinity;
      double maxDrawdownPercent = 0;
      foreach (var v in accountValue.Skip(1))
      {
        inDrawdown = inDrawdown || v < last;
        if (!inDrawdown)
          lastHigh = Math.Max(lastHigh, v);
        else
        {
          worstInDrawdown = Math.Min(worstInDrawdown, v);
          if (v >= lastHigh)
          {
            inDrawdown = false;
            maxDrawdownPercent = Math.Max(maxDrawdownPercent, (lastHigh - worstInDrawdown) / lastHigh);
            lastHigh = v;
            worstInDrawdown = double.PositiveInfinity;
          }
        }
        last = v;
      }

      return maxDrawdownPercent;
    }
  }

  public delegate DataSeries<Value> IndicatorTransformer(DataSeries<Bar> ds);

  public class TradeRecord
  {
    public readonly string Symbol;
    public readonly double Entry;
    public readonly double Exit;
    public readonly DateTime EntryTime;
    public readonly DateTime ExitTime;
    public readonly int Size;
    public readonly double Profit;
    public double Loss { get { return -Profit; } }
    public double PercentProfit { get { return Profit / (Entry * Size); } }
    public double PercentLoss { get { return -Profit / (Entry * Size); } }
    public bool IsWin { get { return Profit > 0; } }

    public TradeRecord(string symbol, double entry, double exit, DateTime entryTime, DateTime exitTime, int size, double profit)
    {
      Symbol = symbol;
      Entry = entry;
      Exit = exit;
      EntryTime = entryTime;
      ExitTime = exitTime;
      Size = size;
      Profit = profit;
    }
  }

  public class BacktestReport
  {
    //public Dictionary<string, double> Parameters;
    public DataSeries<Bar> InputSet;
    public DataSeries<Value> AccountValue;
    public List<TradeRecord> Trades;
    public double ProfitFactor { get { return (AccountValue.Last() - AccountValue.First()) / AccountValue.First(); } }
    public double MaxDrawdownPercent;
    //public List<double> ProfitFactorHistory;
    public int NumWinningTrades { get { return Trades.Where(x => x.IsWin).Count(); } }
    public int NumLosingTrades { get { return Trades.Where(x => !x.IsWin).Count(); } }
    public double AverageWin { get { return Trades.Where(x => x.IsWin).Average(x => x.Profit); } }
    public double AverageLoss { get { return Trades.Where(x => !x.IsWin).Average(x => x.Loss); } }
    public double WinningTradeFraction
    {
      get { return (double)NumWinningTrades / (NumWinningTrades + NumLosingTrades); }
    }
    public double AverageWinLossRatio
    {
      get { return AverageWin / AverageLoss; }
    }
    public double CPC { get { return ProfitFactor * WinningTradeFraction * AverageWinLossRatio; } }

    //public override string ToString()
    //{
    //  var sb = new StringBuilder();
    //  sb.AppendLine("---------------");
    //  sb.AppendLine("Profit Factor: " + ProfitFactor);
    //  sb.AppendLine("Max Drawdown %: " + MaxDrawdownPercent);
    //  foreach (var kv in Parameters)
    //    sb.AppendLine(kv.Key + ": " + kv.Value);
    //  return sb.ToString();
    //}
  }
}
