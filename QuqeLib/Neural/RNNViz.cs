namespace Quqe
{
  public static class RNNViz
  {
//    public static void ToPng(RNNSpec spec, string fn)
//    {
//      var baseName = Path.GetFileNameWithoutExtension(fn);
//      ToDot(spec, baseName + ".dot");
//      var psi = new ProcessStartInfo(@"C:\Program Files (x86)\Graphviz 2.28\bin\dot.exe",
//        string.Format("-Tpng -o dots\\{0}.png {0}.dot", baseName));
//      psi.CreateNoWindow = true;
//      psi.WindowStyle = ProcessWindowStyle.Hidden;
//      var p = Process.Start(psi);
//      p.EnableRaisingEvents = true;
//      p.WaitForExit();
//    }

//    public static void ToDot(RNNSpec spec, string fn)
//    {
//      using (var op = new StreamWriter(fn))
//      {
//        op.WriteLine(@"
//digraph {
//rankdir=LR;
//nodesep=0.5;
//ranksep=5;
//ordering=out;
//edge [ arrowsize=0.5 ];
//node [ shape=circle ];");

//        op.WriteLine("rootnode [ style=invis ];");

//        var inputs = new List<string>();
//        op.WriteLine(@"
//subgraph {
//rank=same;");
//        for (int i = 0; i < spec.NumInputs; i++)
//        {
//          var name = "input" + i;
//          inputs.Add(name);
//          op.WriteLine("input{0} [ shape=square, label=\"{0}\" ];", i);
//        }
//        op.WriteLine("}");
//        for (int i = 0; i < spec.NumInputs; i++)
//          op.WriteLine("rootnode -> {0} [ style=invis ];", inputs[i]);

//        for (int l = 0; l < Layers.Count; l++)
//        {
//          var newInputs = new List<string>();
//          op.WriteLine("subgraph {\r\nrank=same;\r\n");
//          for (int i = 0; i < Layers[l].NodeCount; i++)
//          {
//            var name = "L" + l + "_" + i;
//            newInputs.Add(name);
//            op.WriteLine(name + ";");
//          }
//          op.WriteLine("}");

//          for (int i = 0; i < Layers[l].NodeCount; i++)
//            for (int j = 0; j < inputs.Count; j++)
//              op.WriteLine("{0} -> {1} [ color={2}, penwidth={3:N2} ];",
//                inputs[j], newInputs[i], Layers[l].W[i, j] > 0 ? "black" : "red", Math.Abs(5 * Layers[l].W[i, j]));

//          if (Layers[l].IsRecurrent)
//            for (int i = 0; i < Layers[l].NodeCount; i++)
//              for (int j = 0; j < Layers[l].NodeCount; j++)
//                op.WriteLine("{0} -> {1} [ constraint=false, color={2}, penwidth={3} ];",
//                  newInputs[j], newInputs[i], Layers[l].Wr[i, j] > 0 ? "black" : "red", Math.Abs(5 * Layers[l].Wr[i, j]));

//          inputs = newInputs;
//        }

//        op.WriteLine("}");
//      }
//    }
  }
}
