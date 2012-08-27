using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using System.Diagnostics;

namespace Quqe
{
  public abstract class ExitCriteria
  {
    public abstract double GetExit(PositionDirection pd, double entry, IEnumerable<Bar> dailyBarsFromNow, out DateTime exitTime);
    public abstract double StopLimit { get; }
  }

  public class ExitOnSessionClose : ExitCriteria
  {
    readonly double? _StopLimit;
    public override double StopLimit { get { return _StopLimit == null ? 0 : _StopLimit.Value; } }

    public ExitOnSessionClose()
    {
    }

    public ExitOnSessionClose(double stopLimit)
    {
      _StopLimit = stopLimit;
    }

    public ExitOnSessionClose(double? stopLimit)
    {
      if (stopLimit.HasValue)
        _StopLimit = stopLimit;
    }

    public override double GetExit(PositionDirection pd, double entry, IEnumerable<Bar> dailyBarsFromNow, out DateTime exitTime)
    {
      var todayBar = dailyBarsFromNow.First();
      exitTime = todayBar.Timestamp.AddHours(16);

      if (_StopLimit == null)
        return todayBar.Close;

      if (pd == PositionDirection.Long)
      {
        Debug.Assert(_StopLimit < entry);
        if (todayBar.Low <= _StopLimit)
          return _StopLimit.Value;
      }
      else if (pd == PositionDirection.Short)
      {
        Debug.Assert(_StopLimit > entry);
        if (todayBar.High >= _StopLimit)
          return _StopLimit.Value;
      }

      return todayBar.Close;
    }
  }

  public enum PositionDirection { Long, Short }

  public class Account
  {
    List<Position> Positions = new List<Position>();
    public Func<long, double> Commission = numShares => 4.95;

    double _Equity;
    public double Equity
    {
      get { return _Equity; }
      set
      {
        _Equity = value;
        if (double.IsNaN(_Equity))
          throw new Exception();
      }
    }
    //public double Equity { get; set; }
    public double MarginFactor { get; set; }
    public double TotalBorrowable { get { return Equity * MarginFactor; } }
    public double BuyingPower { get { return TotalBorrowable - AmountBorrowed; } }
    public double AccountValue { get { return Equity - AmountBorrowed + Positions.Sum(p => Math.Abs(p.NumShares * CurrentPrice(p.Symbol))); } }

    public double AmountBorrowed { get; private set; }

    public double Padding { get; set; }

    public event Action<TradeRecord> Traded;

    public bool IgnoreGains;
    public bool IgnoreLosses;

    public void EnterLong(string symbol, long numShares, ExitCriteria exitCriteria, IEnumerable<Bar> barsFromNow)
    {
      var p = GetPosition(symbol);
      var todayBar = barsFromNow.First();
      var entry = todayBar.Open;
      var valueBefore = AccountValue;
      if (IgnoreLosses || IgnoreGains)
      {
        DateTime exitTime2;
        var exit2 = exitCriteria.GetExit(PositionDirection.Long, entry, barsFromNow, out exitTime2);
        if ((IgnoreLosses && exit2 < entry) || (IgnoreGains && exit2 > entry))
        {
          FireTraded(new TradeRecord(symbol, PositionDirection.Long, entry, entry, entry, entry,
            todayBar.Timestamp.AddHours(9.5), exitTime2, numShares, 0, AccountValue, AccountValue));
          return;
        }
      }

      p.Open(numShares, entry);
      DateTime exitTime;
      var exit = exitCriteria.GetExit(PositionDirection.Long, entry, barsFromNow, out exitTime);
      p.Close(numShares, exit);
      FireTraded(new TradeRecord(symbol, PositionDirection.Long, entry, exitCriteria.StopLimit, exit, todayBar.Close,
        todayBar.Timestamp.AddHours(9.5), exitTime, numShares, AccountValue - valueBefore, valueBefore, AccountValue));
    }

    public void EnterShort(string symbol, long numShares, ExitCriteria exitCriteria, IEnumerable<Bar> barsFromNow)
    {
      var p = GetPosition(symbol);
      var todayBar = barsFromNow.First();
      var entry = todayBar.Open;
      var valueBefore = AccountValue;
      if (IgnoreLosses || IgnoreGains)
      {
        DateTime exitTime2;
        var exit2 = exitCriteria.GetExit(PositionDirection.Short, entry, barsFromNow, out exitTime2);
        if ((IgnoreLosses && exit2 > entry) || (IgnoreGains && exit2 < entry))
        {
          FireTraded(new TradeRecord(symbol, PositionDirection.Short, entry, entry, entry, entry,
            todayBar.Timestamp.AddHours(9.5), exitTime2, numShares, 0, AccountValue, AccountValue));
          return;
        }
      }
      p.Open(-numShares, entry);
      DateTime exitTime;
      var exit = exitCriteria.GetExit(PositionDirection.Short, entry, barsFromNow, out exitTime);
      p.Close(-numShares, exit);
      FireTraded(new TradeRecord(symbol, PositionDirection.Short, entry, exitCriteria.StopLimit, exit, todayBar.Close,
        todayBar.Timestamp.AddHours(9.5), exitTime, numShares, AccountValue - valueBefore, valueBefore, AccountValue));
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
      public long NumShares { get; private set; }
      public double AveragePrice { get; private set; }

      public Position(Account account, string symbol)
      {
        Account = account;
        Symbol = symbol;
      }

      public void Open(long size, double price)
      {
        Account.Equity = Account.Equity - Account.Commission(Math.Abs(size));
        double formerValue = NumShares * AveragePrice;
        double sizeValue = Math.Abs(size) * price;
        if (NumShares + size == 0)
          throw new Exception();
        NumShares += size;
        AveragePrice = Math.Abs((formerValue + sizeValue) / NumShares);
        if (sizeValue > Account.BuyingPower)
          throw new Exception("Insufficient buying power");
        Account.AmountBorrowed = Account.AmountBorrowed + sizeValue;
      }

      public void Close(long size, double price)
      {
        Account.Equity = Account.Equity - Account.Commission(Math.Abs(size));
        NumShares -= size;
        var profit = size * (price - AveragePrice);
        Account.Equity = Account.Equity + profit;
        double sizeValue = Math.Abs(size) * AveragePrice;
        Account.AmountBorrowed = Account.AmountBorrowed - sizeValue;
      }
    }
  }
}
