using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;

namespace Quqe
{
  public abstract class ExitCriteria
  {
    public abstract double GetExit(PositionDirection pd, IEnumerable<Bar> dailyBarsFromNow, out DateTime exitTime);
  }

  public class ExitOnSessionClose : ExitCriteria
  {
    readonly double StopLimit;
    public ExitOnSessionClose(double stopLimit)
    {
      StopLimit = stopLimit;
    }

    public override double GetExit(PositionDirection pd, IEnumerable<Bar> dailyBarsFromNow, out DateTime exitTime)
    {
      var todayBar = dailyBarsFromNow.First();
      exitTime = todayBar.Timestamp.AddHours(16);
      if ((pd == PositionDirection.Long && todayBar.Low <= StopLimit)
        || (pd == PositionDirection.Short && todayBar.High >= StopLimit))
        return StopLimit;
      else
        return todayBar.Close;
    }
  }

  public enum PositionDirection { Long, Short }

  public class Account
  {
    List<Position> Positions = new List<Position>();
    public Func<int, double> Commission = numShares => 4.95;

    public double Equity { get; set; }
    public double MarginFactor { get; set; }
    public double TotalBorrowable { get { return Math.Round(Equity * MarginFactor); } }
    public double BuyingPower { get { return TotalBorrowable - AmountBorrowed; } }
    public double AccountValue { get { return Equity - AmountBorrowed + Positions.Sum(p => Math.Abs(p.NumShares * CurrentPrice(p.Symbol))); } }

    public double AmountBorrowed { get; private set; }

    public event Action<TradeRecord> Traded;

    public void EnterLong(string symbol, int numShares, ExitCriteria exitCriteria, IEnumerable<Bar> barsFromNow)
    {
      var p = GetPosition(symbol);
      var todayBar = barsFromNow.First();
      var entry = todayBar.Open;
      var valueBefore = AccountValue;
      p.Open(numShares, entry);
      DateTime exitTime;
      var exit = exitCriteria.GetExit(PositionDirection.Long, barsFromNow, out exitTime);
      p.Close(numShares, exit);
      FireTraded(new TradeRecord(symbol, entry, exit, todayBar.Timestamp.AddHours(9.5), exitTime, numShares, AccountValue - valueBefore));
    }

    public void EnterShort(string symbol, int numShares, ExitCriteria exitCriteria, IEnumerable<Bar> barsFromNow)
    {
      var p = GetPosition(symbol);
      var todayBar = barsFromNow.First();
      var entry = todayBar.Open;
      var valueBefore = AccountValue;
      p.Open(-numShares, entry);
      DateTime exitTime;
      var exit = exitCriteria.GetExit(PositionDirection.Short, barsFromNow, out exitTime);
      p.Close(-numShares, exit);
      FireTraded(new TradeRecord(symbol, entry, exit, todayBar.Timestamp.AddHours(9.5), exitTime, numShares, AccountValue - valueBefore));
    }

    void FireTraded(TradeRecord record)
    {
      if (Traded != null)
        Traded(record);
    }

    // FIX ME!
    double CurrentPrice(string symbol)
    {
      return GetPosition(symbol).AveragePrice;
    }

    Position GetPosition(string symbol)
    {
      var p = Positions.FirstOrDefault(x => x.Symbol == symbol);
      if (p == null)
      {
        p = new Position(this, symbol);
        Positions.Add(p);
      }
      return p;
    }

    public class Position
    {
      readonly Account Account;
      public readonly string Symbol;
      public int NumShares { get; private set; }
      public double AveragePrice { get; private set; }

      public Position(Account account, string symbol)
      {
        Account = account;
        Symbol = symbol;
      }

      public void Open(int size, double price)
      {
        Account.Equity = Math.Round(Account.Equity - Account.Commission(Math.Abs(size)), 2);
        double formerValue = NumShares * AveragePrice;
        double sizeValue = Math.Abs(size) * price;
        NumShares += size;
        AveragePrice = Math.Abs((formerValue + sizeValue) / NumShares);
        if (sizeValue > Account.BuyingPower)
          throw new Exception("Insufficient buying power");
        Account.AmountBorrowed = Math.Round(Account.AmountBorrowed + sizeValue, 2);
      }

      public void Close(int size, double price)
      {
        Account.Equity = Math.Round(Account.Equity - Account.Commission(Math.Abs(size)), 2);
        NumShares -= size;
        var profit = size * (price - AveragePrice);
        Account.Equity = Math.Round(Account.Equity + profit, 2);
        double sizeValue = Math.Abs(size) * AveragePrice;
        Account.AmountBorrowed = Math.Round(Account.AmountBorrowed - sizeValue, 2);
      }
    }
  }
}
