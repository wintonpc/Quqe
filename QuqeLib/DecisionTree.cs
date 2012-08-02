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

    public DtNode(object decidingAttribute, IEnumerable<DtChild> children)
    {
      DecidingAttribute = decidingAttribute;
      Children = children.ToList();
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

    static object Learn(IEnumerable<DtExample> examples, IEnumerable<Type> attribs, object defaultValue, double minimumMajority)
    {
      if (!examples.Any())
        return defaultValue;
      else if (examples.GroupBy(x => x.Goal).Count() == 1)
        return examples.First().Goal;
      else if (!attribs.Any())
      {
        double strength;
        var majorityValue = MajorityValue(examples, out strength);
        if (strength > minimumMajority)
          return majorityValue;
        else
          return "Unsure";
      }
      else
      {
        Type best = ChooseAttribute(attribs, examples);
        return new DtNode(best, Values(best).Select(v => new DtChild(v, Learn(
          ExamplesWithAttributeValue(examples, v),
          attribs.Except(List.Create(best)),
          MajorityValue(examples),
          minimumMajority))));
      }
    }

    public static object Decide(IEnumerable<object> attributeValues, object tree)
    {
      if (!(tree is DtNode))
        return tree;

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

    static Type ChooseAttribute(IEnumerable<Type> attribs, IEnumerable<DtExample> examples)
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
        return new { Attr = attr, Information = infoHere - remainder };
      }).OrderByDescending(x => x.Information).ToList();
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
      Func<DtNode, string> dName = n => ((Type)n.DecidingAttribute).Name + "?";
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
            op.WriteLine(string.Format("n{0} [ label = \"{1}\" ]", dtn.GetHashCode(), dName(dtn)));
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
                dest = gs.First(g => g.Key.ToString() == c.Child.ToString()).GetHashCode().ToString();
                op.WriteLine(string.Format("n{0} [ label = \"{1}\", fillcolor=lightgray, style=filled ]", dest, lName(c.Child)));
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
