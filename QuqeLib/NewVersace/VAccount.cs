using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quqe.NewVersace
{
  class VAccount
  {
    public double Equity { get; private set; }
    public double Invested { get; private set; }
    public double MarginFactor { get; private set; }
    public double TradeCost { get; private set; }
    public double BuyingPower { get { return Equity * MarginFactor - Invested; } }

    readonly Dictionary<string, Position> Positions = new Dictionary<string, Position>(); 

    public VAccount(double initialEquity, double marginFactor, double tradeCost)
    {
      Equity = initialEquity;
      MarginFactor = marginFactor;
      TradeCost = tradeCost;
    }

    public void Buy(string s, int numShares, double pricePerShare)
    {
      var existingNumShares = GetPosition(s).NumShares;
      if (existingNumShares < 0)
        throw new Exception(string.Format("Can't buy when already short {0} shares", existingNumShares));

      Equity -= TradeCost;

      if (numShares * pricePerShare > BuyingPower)
        throw new Exception("Insufficient buying power");

      Invested += numShares * pricePerShare;
      IncreasePosition(s, numShares, pricePerShare);
    }

    public void Sell(string s, int numShares, double pricePerShare)
    {
      var existingNumShares = GetPosition(s).NumShares;
      if (existingNumShares < numShares)
        throw new Exception(string.Format("Can't sell {0} when you only have {1} shares", numShares, existingNumShares));

      var pos = GetPosition(s);
      var profit = numShares * (pricePerShare - pos.AveragePrice);

      Equity += profit;
      Equity -= TradeCost;
      Invested -= numShares * pos.AveragePrice;
      DecreasePosition(s, numShares);
    }

    public void Short(string s, int numShares, double pricePerShare)
    {
      var existingNumShares = GetPosition(s).NumShares;
      if (existingNumShares > 0)
        throw new Exception(string.Format("Can't short when already long {0} shares", existingNumShares));

      Equity -= TradeCost;

      if (numShares * pricePerShare > BuyingPower)
        throw new Exception("Insufficient buying power");

      Invested += numShares * pricePerShare;
      IncreasePosition(s, -numShares, pricePerShare);
    }

    public void Cover(string s, int numShares, double pricePerShare)
    {
      var existingNumShares = GetPosition(s).NumShares;
      if (Math.Abs(existingNumShares) < numShares)
        throw new Exception(string.Format("Can't cover {0} when you only have {1} shares", numShares, existingNumShares));

      var pos = GetPosition(s);
      var profit = numShares * (pos.AveragePrice - pricePerShare);

      Equity += profit;
      Equity -= TradeCost;
      Invested -= numShares * pos.AveragePrice;
      DecreasePosition(s, -numShares);
    }

    void IncreasePosition(string s, int numShares, double pricePerShare)
    {
      var pos = GetPosition(s);
      var newNumShares = pos.NumShares + numShares;
      var avgPrice = (pos.NumShares * pos.AveragePrice + numShares * pricePerShare) / newNumShares;
      Positions[s] = new Position(newNumShares, avgPrice);
    }

    void DecreasePosition(string s, int numShares)
    {
      var pos = GetPosition(s);
      Positions[s] = new Position(pos.NumShares - numShares, pos.AveragePrice);
    }

    Position GetPosition(string symbol)
    {
      Position p;
      return Positions.TryGetValue(symbol, out p) ? p : new Position(0, 0);
    }

    class Position
    {
      public readonly int NumShares;
      public readonly double AveragePrice;

      public Position(int numShares, double averagePrice)
      {
        NumShares = numShares;
        AveragePrice = averagePrice;
      }
    }
  }
}
