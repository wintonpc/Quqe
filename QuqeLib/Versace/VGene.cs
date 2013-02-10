using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCW;

namespace Quqe
{
  public abstract class VGene
  {
    public readonly string Name;
    public VGene(string name) { Name = name; }
    public abstract string RangeString { get; }
    public abstract VGene Clone();
    public abstract VGene CloneAndRandomize();
    public abstract VGene Mutate(double dampingFactor);
    public abstract double GetDoubleMin();
    public abstract double GetDoubleMax();
    public abstract double GetDoubleValue();
  }

  public class VGene<TValue> : VGene
    where TValue : struct
  {
    public readonly double Min;
    public readonly double Max;
    public readonly double Granularity;
    public readonly TValue Value;

    public override double GetDoubleMin() { return Min; }
    public override double GetDoubleMax() { return Max; }
    public override double GetDoubleValue() { return Value.As<double>(); }

    public VGene(string name, double min, double max, double granularity, TValue? initialValue = null)
      : base(name)
    {
      Min = min;
      Max = max;
      Granularity = granularity;
      Value = initialValue ?? RandomValue();
    }

    public override string RangeString
    {
      get
      {
        if (Value is int)
        {
          if (Min == Max)
            return Min.ToString();
          else if (Min == 0 && Max == 1 && Granularity == 1)
            return "0/1";
          else
            return string.Format("{0} - {1}", Min, Max);
        }
        else
          return string.Format("{0:N1} - {1:N1}", Min, Max);
      }
    }

    TValue RandomValue()
    {
      return Optimizer.Quantize(Optimizer.RandomDouble(Min, Max), Min, Granularity).As<TValue>();
    }

    public override VGene Mutate(double dampingFactor)
    {
      var doubleValue = Value.As<double>();

      // handle "booleans" specially
      if (Min == 0 && Max == 1 && Value is int)
      {
        if (dampingFactor == 0 || Optimizer.WithProb(1 / dampingFactor))
          doubleValue = 1 - doubleValue;
        return new VGene<TValue>(Name, Min, Max, Granularity, doubleValue.As<TValue>());
      }

      var rand = Optimizer.RandomDouble(Min, Max);
      var weighted = (dampingFactor * doubleValue + rand) / (dampingFactor + 1);
      var quantized = Optimizer.Quantize(weighted, Min, Granularity).As<TValue>();
      return new VGene<TValue>(Name, Min, Max, Granularity, quantized);
    }

    public override VGene Clone()
    {
      return new VGene<TValue>(Name, Min, Max, Granularity, Value);
    }

    public override VGene CloneAndRandomize()
    {
      return new VGene<TValue>(Name, Min, Max, Granularity);
    }
  }
}
