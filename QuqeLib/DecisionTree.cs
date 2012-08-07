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
    public DtExample(DateTime? timestamp, object goal, IEnumerable<object> attributeValues)
    {
      Timestamp = timestamp;
      Goal = goal;
      AttributesValues = attributeValues.ToList();
    }
  }

  public static class DecisionTree
  {
    public static object Learn(IEnumerable<DtExample> examples, object defaultValue, double minimumMajority)
    {
      return Learn(examples, examples.First().AttributesValues.Select(x => x.GetType()), defaultValue, minimumMajority);
    }

    static object UnsureValue = "Unsure";

    static object Learn(IEnumerable<DtExample> examples, IEnumerable<Type> attribs, object defaultValue, double minimumMajority)
    {
      var exs = examples.ToList();
      var attrs = attribs.ToList();

      if (!exs.Any())
        return new OutcomeValue(UnsureValue, 0, exs.Count);
      else if (exs.GroupBy(x => x.Goal).Count() == 1)
        return new OutcomeValue(exs.First().Goal, 1, exs.Count);
      else if (!attrs.Any())
      {
        double strength;
        var majorityValue = MajorityValue(exs, out strength);
        if (strength > minimumMajority)
          return new OutcomeValue(majorityValue, strength, exs.Count);
        else
          return new OutcomeValue(UnsureValue, strength, exs.Count);
      }
      else
      {
        double info;
        Type best = ChooseAttribute(attrs, exs, out info);
        return new DtNode(best, info, Values(best).Select(v => {
          var subset = ExamplesWithAttributeValue(exs, v).ToList();
          return new DtChild(v, Learn(
          subset,
          attrs.Except(List.Create(best)),
          MajorityValue(exs),
          minimumMajority));
        }));
      }
    }

    public static object Decide(IEnumerable<object> attributeValues, object tree)
    {
      if (tree is OutcomeValue)
        return ((OutcomeValue)tree).Value;

      var path = ((DtNode)tree).Children.Single(c => attributeValues.Contains(c.DecidedAttribute));
      return Decide(attributeValues, path.Child);
    }

    static IEnumerable<DtExample> ExamplesWithAttributeValue(IEnumerable<DtExample> examples, object value)
    {
      return examples.Where(x => x.AttributesValues.Contains(value));
    }

    static IEnumerable<object> Values(Type attr)
    {
      return Enum.GetValues(attr).Cast<object>();
    }

    static object MajorityValue(IEnumerable<DtExample> examples)
    {
      return examples.GroupBy(x => x.Goal).OrderByDescending(g => g.Count()).First().Key;
    }

    static object MajorityValue(IEnumerable<DtExample> examples, out double strength)
    {
      var majorityGroup = examples.GroupBy(x => x.Goal).OrderByDescending(g => g.Count()).First();
      strength = (double)majorityGroup.Count() / examples.Count();
      return majorityGroup.Key;
    }

    static Type ChooseAttribute(IEnumerable<Type> attribs, IEnumerable<DtExample> examples, out double information)
    {
      var sortedAttribs = attribs.Select(attr => {
        var infoHere = Information(AttributeValueProbabilities(attr, examples));
        var remainder = Values(attr).Sum(v => {
          var subset = ExamplesWithAttributeValue(examples, v);
          var sc = subset.Count();
          if (sc == 0)
            return 0;
          return (double)sc / examples.Count() * Information(AttributeValueProbabilities(attr, subset));
        });
        return new { Attr = attr, InformationGain = infoHere - remainder };
      }).OrderByDescending(x => x.InformationGain).ToList();
      information = sortedAttribs.First().InformationGain;
      return sortedAttribs.First().Attr;
    }

    static IEnumerable<double> AttributeValueProbabilities(Type attr, IEnumerable<DtExample> examples)
    {
      return Values(attr).Select(v => (double)ExamplesWithAttributeValue(examples, v).Count() / examples.Count());
    }

    static double Log2(double x)
    {
      return Math.Log(x) / Math.Log(2);
    }

    static double Information(IEnumerable<double> ps)
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
                //dest = gs.First(g => g.Key.ToString() == c.Child.ToString()).GetHashCode().ToString();
                dest = new object().GetHashCode().ToString();
                op.WriteLine(string.Format("n{0} [ label = \"{1}\", fillcolor={2}, style=filled ]",
                  dest, lName(ov.Value) + "\\n" + (ov.Strength * 100).ToString("N1") + "% (" + ov.Count + ")",
                  //c.Count < 3 ? "powderblue" :
                  ov.Value.Equals(Quqe.DtSignals.Prediction.Green) ? "green" :
                  ov.Value.Equals(Quqe.DtSignals.Prediction.Red) ? "tomato" :
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
}
