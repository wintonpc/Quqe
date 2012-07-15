using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;

namespace Quqe
{
  public abstract class Strategy
  {
    public abstract IEnumerable<string> InputNames { get; }
    public abstract IEnumerable<string> OutputNames { get; }
    public abstract BacktestReport Backtest(NeuralNet net); // needs to be threadsafe
  }

  public class OnePerDayStrategy1 : Strategy
  {
    public override IEnumerable<string> InputNames { get { return List.Create("O1", "L1", "H1", "C1", "O0", "ZLEMASlope"); } }
    public override IEnumerable<string> OutputNames { get { return List.Create("BuySignal", "StopLimit"); } }

    DataSeries<Bar> Bars;
    DataSeries<Value> ZLEMASlope;

    public OnePerDayStrategy1(IEnumerable<StrategyParameter> sParams, DataSeries<Bar> bars)
    {
      Func<string, double> param = name => sParams.First(sp => sp.Name == name).Value;
      Bars = bars;
      ZLEMASlope = bars.ZLEMA((int)param("ZLEMAPeriod"), bar => (int)param("ZLEMAOpenOrClose") == 0 ? bar.Open : bar.Close).Derivative();
    }

    public override BacktestReport Backtest(NeuralNet net)
    {
      var s = Bars.Clone();
      var zlemaSlope = ZLEMASlope.Clone();

      double accountPadding = 20.0;

      var account = new Account { Equity = 10000, MarginFactor = 1 };
      var backtester = Backtester.Start(s, account);

      DataSeries.Walk(s, zlemaSlope, pos => {
        if (pos == 0)
          return;
        var normal = s[1].Close;
        var normalizedPrices = List.Create(s[1].Open, s[1].Low, s[1].High, s[1].Close, s[0].Open).Select(x => x / normal).ToList();
        var inputs = normalizedPrices.Concat(List.Create(zlemaSlope[0].Val));
        var shouldBuy = net.Propagate(inputs)[0] >= 0;
        var stopLimit = net.Propagate(inputs)[1] * normal;
        var size = (int)((account.BuyingPower - accountPadding) / s[0].Open);
        if (size > 0)
        {
          if (shouldBuy)
            account.EnterLong(Bars.Symbol, size, new ExitOnSessionClose(Math.Max(0, stopLimit)), s.FromHere());
          else
            account.EnterShort(Bars.Symbol, size, new ExitOnSessionClose(Math.Min(100000, stopLimit)), s.FromHere());
        }
        backtester.UpdateAccountValue(account.Equity);
      });
      return backtester.Stop();
    }
  }
}
