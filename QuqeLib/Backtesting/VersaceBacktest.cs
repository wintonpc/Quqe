using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public static class VersaceBacktest
  {
    public static BacktestReport Backtest(PredictionType predictionType, IPredictor predictor, Account account, Mat preInputs, Mat inputs, DataSeries<Bar> bars)
    {
      double maxAccountLossPct = 0.025;

      // run through the preTesting values so the RNNs can build up state
      predictor.Reset();
      foreach (var input in preInputs.Columns())
        predictor.Predict(input);

      List<SignalValue> signal = null;
      if (predictionType == PredictionType.NextClose)
        signal = MakeSignalNextClose(predictor, inputs.Columns(), bars, maxAccountLossPct);
      BacktestReport report = PlaybackSignal(signal, bars, account);
      return report;
    }

    static List<SignalValue> MakeSignalNextClose(IPredictor predictor, List<Vec> inputs, DataSeries<Bar> bars, double maxAccountLossPct)
    {
      var signal = new List<SignalValue>();
      DataSeries<Value> buySell = inputs.Select(x => (double)Math.Sign(predictor.Predict(x))).ToDataSeries(bars);
      int riskATRPeriod = 7;
      double riskScale = 1.2;
      double riskRatio = 0.65;
      DataSeries<Value> perShareRisk =
        bars.OpeningWickHeight().EMA(riskATRPeriod).ZipElements<Value, Value>(
        bars.ATR(riskATRPeriod), (w, a, v) =>
          riskScale * (riskRatio * w[0] + (1 - riskRatio) * a[0]));

      for (int i = 0; i < bars.Length; i++)
      {
        var bias = buySell[i].Val > 0 ? SignalBias.Buy : SignalBias.Sell;
        var sizePct = maxAccountLossPct / perShareRisk[i];
        double absoluteStop;
        if (bias == SignalBias.Buy)
          absoluteStop = bars[i].Open - perShareRisk[0];
        else
          absoluteStop = bars[i].Open + perShareRisk[0];
        signal.Add(new SignalValue(bars[i].Timestamp, bias, SignalTimeOfDay.Close, sizePct, absoluteStop, null));
      }
      return signal;
    }

    private static BacktestReport PlaybackSignal(List<SignalValue> signal, DataSeries<Bar> bars, Account account)
    {
      throw new NotImplementedException();
    }
  }
}
