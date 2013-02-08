using System;
using System.Collections.Generic;
using System.Linq;

namespace Quqe
{
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
      var type = typeof(BasicStrategy).Assembly.GetType(className);
      var ctor = type.GetConstructor(new[] { typeof(IEnumerable<StrategyParameter>) });
      return (BasicStrategy)ctor.Invoke(new object[] { sParams });
    }
  }
}
