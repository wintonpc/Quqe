using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quqe.NewVersace;
using Quqe.Rabbit;
using System.Diagnostics;

namespace Quqe
{
  public class Day
  {
    public DbBar Bar { get; private set; }
    public double Profit { get; private set; }

    void foo()
    {
    }
  }

  public static class Backtesting
  {
    public static List<Day> Backtest(Database db, Mixture mixture, string symbol, DateTime startDate, DateTime endDate,
                                     double initialEquity, double marginFactor, double tradeCost)
    {
      var preloadStart = startDate.AddYears(-1);
      var allBars = new DataSeries<Bar>(symbol, db.QueryAll<DbBar>(x => x.Symbol == symbol, "Timestamp")
                                                  .Select(DataPreprocessing.DbBarToBar));
      var bars = DataPreprocessing.TrimToWindow(allBars, DataPreprocessing.GetWindow(preloadStart, endDate, allBars));
      var data = DataPreprocessing.LoadTrainingSet(db, symbol, preloadStart, endDate, Signals.Null);
      var predictor = new MixturePredictor(mixture, data);

      Debug.Assert(bars.Length == data.Input.ColumnCount);
      var backtestStartIndex = bars.ToList().FindIndex(x => x.Timestamp >= startDate);

      // preload
      for (int t = 0; t < backtestStartIndex; t++)
        predictor.Predict(t);

      int totalPredictions = 0;
      int correctPredictions = 0;

      // backtest
      var account = new VAccount(initialEquity, marginFactor, tradeCost);
      for (int t = backtestStartIndex + 1; t < bars.Length; t++)
      {
        var prediction = predictor.Predict(t - 1);
        Debug.Assert(prediction != 0);
        var wasCorrect = ActOnSignal(bars[t-1], bars[t], prediction, account, symbol);
        totalPredictions++;
        if (wasCorrect)
          correctPredictions++;
      }

      double correctPct = (double)correctPredictions / totalPredictions;

      return null;
    }

    static bool ActOnSignal(Bar yesterdayBar, Bar bar, double prediction, VAccount account, string symbol)
    {
      var shouldBuy = prediction > 0;
      if (shouldBuy)
      {
        var numShares = account.MaxSharesAtPrice(yesterdayBar.Close);
        account.Buy(symbol, numShares, yesterdayBar.Close);
        account.Sell(symbol, numShares, bar.Close);
        return yesterdayBar.Close < bar.Close;
      }
      else
      {
        var numShares = account.MaxSharesAtPrice(yesterdayBar.Close);
        account.Short(symbol, numShares, yesterdayBar.Close);
        account.Cover(symbol, numShares, bar.Close);
        return yesterdayBar.Close > bar.Close;
      }
    }
  }
}