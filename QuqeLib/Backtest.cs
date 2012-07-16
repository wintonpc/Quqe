using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Quqe
{
  public class BacktestHelper
  {
    Account Account;
    DataSeries<Bar> Bars;
    BacktestHelper(DataSeries<Bar> bars, Account account)
    {
      Bars = bars;
      Account = account;
    }

    List<TradeRecord> Trades;
    //List<Value> AccountValues;
    public static BacktestHelper Start(DataSeries<Bar> bars, Account account)
    {
      var b = new BacktestHelper(bars, account);
      b.StartInternal();
      return b;
    }

    void StartInternal()
    {
      //AccountValues = new List<Value>();
      Trades = new List<TradeRecord>();
      Account.Traded += Trades.Add;
    }

    //public void UpdateAccountValue(double value)
    //{
    //  AccountValues.Add(new Value(Bars[0].Timestamp, value));
    //}

    public BacktestReport Stop()
    {
      var accountValue = new DataSeries<Value>(Bars.Symbol, Trades.Select(t => new Value(t.ExitTime.Date, t.AccountValueAfterTrade)));

      return new BacktestReport {
        InputSet = Bars,
        //AccountValue = accountValue,
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
    public readonly PositionDirection PositionDirection;
    public readonly double Entry;
    public readonly double StopLimit;
    public readonly double Exit;
    public readonly DateTime EntryTime;
    public readonly DateTime ExitTime;
    public readonly int Size;
    public readonly double Profit;
    public readonly double AccountValueBeforeTrade;
    public readonly double AccountValueAfterTrade;
    public double Loss { get { return -Profit; } }
    public double PercentProfit { get { return Profit / (Entry * Size); } }
    public double PercentLoss { get { return -Profit / (Entry * Size); } }
    public bool IsWin { get { return Profit > 0; } }

    public TradeRecord(string symbol, PositionDirection direction, double entry, double stopLimit, double exit, DateTime entryTime, DateTime exitTime, int size, double profit, double accountValueBeforeTrade, double accountValueAfterTrade)
    {
      Symbol = symbol;
      PositionDirection = direction;
      Entry = entry;
      StopLimit = stopLimit;
      Exit = exit;
      EntryTime = entryTime;
      ExitTime = exitTime;
      Size = size;
      Profit = profit;
      AccountValueBeforeTrade = accountValueBeforeTrade;
      AccountValueAfterTrade = accountValueAfterTrade;
    }
  }

  public class BacktestReport
  {
    //public Dictionary<string, double> Parameters;
    public DataSeries<Bar> InputSet;
    //public DataSeries<Value> AccountValue;
    public List<TradeRecord> Trades;
    public double TotalReturn { get { return 1 + (Trades.Last().AccountValueAfterTrade - Trades.First().AccountValueBeforeTrade) / Trades.First().AccountValueBeforeTrade; } }
    public double ProfitFactor { get { return Trades.Where(t => t.IsWin).Sum(t => t.Profit) / (Trades.Where(t => !t.IsWin).Sum(t => t.Loss) + Trades.Count * 9.90) /* 9.90 = hack! */; } }
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

    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.AppendLine("---------------");
      sb.AppendLine("CPC: " + CPC);
      sb.AppendLine("Profit Factor: " + ProfitFactor);
      sb.AppendLine("Max Drawdown %: " + MaxDrawdownPercent * 100);
      sb.AppendLine("Total Return: " + TotalReturn);
      sb.AppendLine("");
      sb.AppendLine("NumWinningTrades: " + NumWinningTrades);
      sb.AppendLine("NumLosingTrades: " + NumLosingTrades);
      sb.AppendLine("WinningTradeFraction: " + WinningTradeFraction);
      sb.AppendLine("");
      sb.AppendLine("AverageWin: " + AverageWin);
      sb.AppendLine("AverageLoss: " + AverageLoss);
      sb.AppendLine("AverageWinLossRatio: " + AverageWinLossRatio);
      return sb.ToString();
    }
  }
}
