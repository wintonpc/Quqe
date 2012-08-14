using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using System.IO;

namespace Quqe
{
  public class DtNode
  {
    public readonly object DecidingAttribute;
    public readonly List<DtChild> Children;
    public readonly double InformationProvided;

    public DtNode(object decidingAttribute, double information, IEnumerable<DtChild> children)
    {
      DecidingAttribute = decidingAttribute;
      Children = children.ToList();
      InformationProvided = information;
    }

    public bool IsLeaf { get { return !Children.Any(); } }
  }

  public class DtChild
  {
    public readonly object DecidedAttribute;
    public readonly object Child;
    public DtChild(object decidedAttribute, object child)
    {
      DecidedAttribute = decidedAttribute;
      Child = child;
    }
  }

  public class OutcomeValue
  {
    public readonly object Value;
    public readonly double Strength;
    public readonly int Count;
    public OutcomeValue(object value, double strength, int count)
    {
      Value = value;
      Strength = strength;
      Count = count;
    }
  }

  public class DtExample
  {
    public DateTime? Timestamp;
    public List<object> AttributesValues;
    public object Goal;
    public DtExample(DateTime? timestamp, object goal, params object[] attributeValues)
    {
      Timestamp = timestamp;
      Goal = goal;
      AttributesValues = attributeValues.ToList();
    }
    public DtExample(DateTime? timestamp, object goal, List<object> attributeValues)
    {
      Timestamp = timestamp;
      Goal = goal;
      AttributesValues = attributeValues;
    }
  }

  public static class DecisionTree
  {
    public static object Learn(IEnumerable<DtExample> examples, object defaultValue, double chiSquareThreshold)
    {
      return Learn(examples.ToList(), examples.First().AttributesValues.Select(x => x.GetType()).ToList(), defaultValue, chiSquareThreshold);
    }

    static object UnsureValue = "Unsure";

    static object Learn(List<DtExample> examples, List<Type> attribs, object defaultValue, double chiSquareThreshold)
    {
      if (!examples.Any())
        return new OutcomeValue(UnsureValue, 0, examples.Count);
      else if (examples.GroupBy(x => x.Goal).Count() == 1)
        return new OutcomeValue(examples[0].Goal, 1, examples.Count);
      else
      {
        double info;
        Type best;
        if (!attribs.Any() || (best = ChooseAttribute(attribs, examples, chiSquareThreshold, out info)) == null)
        {
          double strength;
          var majorityValue = MajorityValue(examples, out strength);
          return new OutcomeValue(majorityValue, strength, examples.Count);
        }
        else
        {
          return new DtNode(best, info, Values(best).Select(v => {
            var subset = ExamplesWithAttributeValue(examples, v);
            return new DtChild(v, Learn(
            subset,
            attribs.Except(List.Create(best)).ToList(),
            MajorityValue(examples),
            chiSquareThreshold));
          }));
        }
      }
    }

    public static object Decide(List<object> attributeValues, object tree)
    {
      if (tree is OutcomeValue)
        return ((OutcomeValue)tree).Value;

      var path = ((DtNode)tree).Children.Single(c => attributeValues.Contains(c.DecidedAttribute));
      return Decide(attributeValues, path.Child);
    }

    static List<DtExample> ExamplesWithGoal(List<DtExample> examples, object goal)
    {
      return examples.Where(x => x.Goal.Equals(goal)).ToList();
    }

    static List<DtExample> ExamplesWithAttributeValue(List<DtExample> examples, object value)
    {
      return examples.Where(x => x.AttributesValues.Contains(value)).ToList();
    }

    static List<object> Values(Type attr)
    {
      return EnumCache.GetValues(attr);
    }

    static object MajorityValue(List<DtExample> examples)
    {
      return examples.GroupBy(x => x.Goal).OrderByDescending(g => g.Count()).First().Key;
    }

    static object MajorityValue(List<DtExample> examples, out double strength)
    {
      var majorityGroup = examples.GroupBy(x => x.Goal).Select(g => new { Goal = g.Key, Count = g.Count() }).OrderByDescending(z => z.Count).First();
      strength = (double)majorityGroup.Count / examples.Count;
      return majorityGroup.Goal;
    }

    static Type ChooseAttribute(List<Type> attribs, List<DtExample> examples, double chiSquareThreshold, out double information)
    {
      var q = attribs.Select(attr => {
        var infoRequiredHere = Information(GoalProbabilities(examples));
        var attrValues = Values(attr);
        var remainingInformationRequired = attrValues.Sum(v => {
          var subset = ExamplesWithAttributeValue(examples, v);
          var sc = subset.Count;
          if (sc == 0)
            return 0;
          return (double)sc / examples.Count * Information(GoalProbabilities(subset));
        });
        var infoProvidedByAttribute = Information(AttributeValueProbabilities(attr, examples));
        var gain = infoRequiredHere - remainingInformationRequired;
        return new {
          Attr = attr,
          AttrValues = attrValues,
          InformationGain = gain,
          GainRatio = infoProvidedByAttribute == 0 ? 0 : gain / infoProvidedByAttribute,
          ChiSquare = ChiSquare(attr, examples)
        };
      }).ToList();
      var averageGain = q.Average(x => x.InformationGain);
      var usable = q.Where(x => x.InformationGain >= averageGain && CheckChiSquared10(x.ChiSquare, x.AttrValues.Count - 1)).ToList();
      if (!usable.Any())
      {
        information = 0;
        return null;
      }
      var bestRoot = usable.OrderByDescending(x => x.GainRatio).ToList()[0];
      information = bestRoot.GainRatio;
      return bestRoot.Attr;
    }

    static List<double> GoalProbabilities(List<DtExample> examples)
    {
      var goalType = examples.First().Goal.GetType();
      return Values(goalType).Select(v => (double)ExamplesWithGoal(examples, v).Count / examples.Count).ToList();
    }

    static List<double> AttributeValueProbabilities(Type attr, List<DtExample> examples)
    {
      return Values(attr).Select(v => (double)ExamplesWithAttributeValue(examples, v).Count / examples.Count).ToList();
    }

    static double ChiSquare(Type attr, List<DtExample> examples)
    {
      var goalValues = Values(examples.First().Goal.GetType());
      var expectedCounts = goalValues.Select(gv => new { GoalValue = gv, ExpectedCount = examples.Count(x => x.Goal.Equals(gv)) }).ToList();
      return Values(attr).Sum(av => {
        var subset = ExamplesWithAttributeValue(examples, av);
        return expectedCounts.Sum(ec => {
          var expected = (double)subset.Count / examples.Count * ec.ExpectedCount;
          var actual = ExamplesWithGoal(subset, ec.GoalValue).Count;
          return Math.Pow(expected - actual, 2) / expected;
        });
      });
    }

    static bool CheckChiSquared20(double chiSquared, int degreesOfFreedom)
    {
      switch (degreesOfFreedom)
      {
        case 1: return chiSquared >= 1.642;
        case 2: return chiSquared >= 3.219;
        case 3: return chiSquared >= 4.642;
        case 4: return chiSquared >= 5.989;
        case 5: return chiSquared >= 7.289;
        case 6: return chiSquared >= 8.558;
        case 7: return chiSquared >= 9.803;
        case 8: return chiSquared >= 11.030;
        default: throw new Exception("table is insufficient");
      }
    }

    static bool CheckChiSquared10(double chiSquared, int degreesOfFreedom)
    {
      switch (degreesOfFreedom)
      {
        case 1: return chiSquared >= 2.706;
        case 2: return chiSquared >= 4.605;
        case 3: return chiSquared >= 6.251;
        case 4: return chiSquared >= 7.779;
        case 5: return chiSquared >= 9.236;
        case 6: return chiSquared >= 10.645;
        case 7: return chiSquared >= 12.017;
        case 8: return chiSquared >= 13.362;
        default: throw new Exception("table is insufficient");
      }
    }

    static double Log2(double x)
    {
      return Math.Log(x) / Math.Log(2);
    }

    static double Information(List<double> ps)
    {
      return ps.Sum(p => p == 0 ? 0 : -p * Log2(p));
    }

    public static void WriteDot(string fn, object tree)
    {
      Func<DtNode, string> dName = n => "I: " + n.InformationProvided.ToString("N3") + "\\n" + ((Type)n.DecidingAttribute).Name + "?";
      Func<object, string> lName = n => n.ToString();
      using (var op = new StreamWriter(fn))
      {
        op.WriteLine("digraph G {");
        op.WriteLine("node [ shape = rect ];");
        Let.Rec((write, node) => {
          if (node is DtNode)
          {
            var dtn = (DtNode)node;
            var gs = dtn.Children.Where(c => !(c.Child is DtNode)).Select(c => c.Child).GroupBy(x => x).ToList();
            op.WriteLine(string.Format("n{0} [ label = \"{1}\", fillcolor={2}, style=filled ]", dtn.GetHashCode(), dName(dtn), dtn.InformationProvided == 0 ? "yellow" : "white"));
            foreach (var c in dtn.Children)
            {
              string dest;
              if (c.Child is DtNode)
              {
                dest = ((DtNode)c.Child).GetHashCode().ToString();
                op.WriteLine(string.Format("n{0} -> n{1} [ label = \"{2}\" ];", dtn.GetHashCode(), dest, lName(c.DecidedAttribute)));
                write(c.Child);
              }
              else
              {
                var ov = ((OutcomeValue)c.Child);
                dest = new object().GetHashCode().ToString();
                op.WriteLine(string.Format("n{0} [ label = \"{1}\", fillcolor={2}, style=filled ]",
                  dest, lName(ov.Value) + "\\n" + (ov.Strength * 100).ToString("N1") + "% (" + ov.Count + ")",
                  ov.Value.Equals(Prediction.Green) ? "green" :
                  ov.Value.Equals(Prediction.Red) ? "tomato" :
                  ov.Count == 0 ? "gray25" :
                  "lightgray"));
                op.WriteLine(string.Format("n{0} -> n{1} [ label = \"{2}\" ];", dtn.GetHashCode(), dest, lName(c.DecidedAttribute)));
              }
            }
          }
        }, tree);
        op.WriteLine("}");
      }
    }
  }

  public static class EnumCache
  {
    [ThreadStatic]
    static Dictionary<Type, List<object>> Values;

    public static List<object> GetValues(Type t)
    {
      if (Values == null)
        Values = new Dictionary<Type, List<object>>();
      List<object> result;
      if (!Values.TryGetValue(t, out result))
      {
        result = Enum.GetValues(t).Cast<object>().ToList();
        Values.Add(t, result);
      }
      return result;
    }
  }
}
