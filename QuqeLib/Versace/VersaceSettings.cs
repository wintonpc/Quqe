using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using PCW;

namespace Quqe
{
  public enum TrainingMethod { Evolve }
  public enum PredictionType { NextClose }
  public enum PreprocessingType { Enhanced }

  public class VersaceSettings
  {
    public string Path { get; private set; }

    public string PredictedSymbol = "DIA";
    public int RnnExpertsPerMixture = 5;
    public int RbfExpertsPerMixture = 5;
    public int TotalExpertsPerMixture { get { return RnnExpertsPerMixture + RbfExpertsPerMixture; } }
    public int PopulationSize = 10;
    public int SelectionSize = 4;
    public int EpochCount = 2;
    public double MutationRate = 0.05;
    public double MutationDamping = 0;
    public TrainingMethod TrainingMethod = TrainingMethod.Evolve;
    public PredictionType PredictionType = PredictionType.NextClose;
    public PreprocessingType PreprocessingType = PreprocessingType.Enhanced;

    public DateTime StartDate = DateTime.Parse("11/11/2001");
    public DateTime EndDate = DateTime.Parse("02/12/2003");
    public int SplitPct = 78;

    public List<VGene> ProtoChromosome = new List<VGene> {
        new VGene<int>("ElmanTrainingEpochs", 20, 20, 1),
        new VGene<int>("DatabaseType", 1, 1, 1),
        new VGene<double>("TrainingOffsetPct", 0, 1, 0.00001),
        new VGene<double>("TrainingSizePct", 0, 1, 0.00001),
        new VGene<int>("UseComplementCoding", 0, 1, 1),
        new VGene<int>("UsePrincipalComponentAnalysis", 0, 1, 1),
        new VGene<int>("PrincipalComponent", 0, 100, 1),
        new VGene<double>("RbfNetTolerance", 0, 1, 0.001),
        new VGene<double>("RbfGaussianSpread", 0.1, 10, 0.01),
        new VGene<int>("ElmanHidden1NodeCount", 3, 40, 1),
        new VGene<int>("ElmanHidden2NodeCount", 3, 20, 1)
      };

    public override string ToString()
    {
      var sb = new StringBuilder();
      foreach (var fi in this.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).Where(fi => fi.Name != "ProtoChromosome"))
        sb.AppendFormat("{0} : {1}\n", fi.Name, fi.GetValue(this));
      sb.AppendFormat("--------------------------\n");
      foreach (var g in ProtoChromosome)
        sb.AppendFormat("{0} : {1}\n", g.Name, g.RangeString);
      return sb.ToString().TrimEnd('\n');
    }

    public DateTime TrainingStart { get { return StartDate; } }
    public DateTime TrainingEnd { get { return ValidationStart.AddDays(-1); } }
    public DateTime ValidationStart { get { return StartDate.AddMilliseconds(EndDate.Subtract(StartDate).TotalMilliseconds * (double)SplitPct / 100).Date; } }
    public DateTime ValidationEnd { get { return EndDate; } }
  }
}
