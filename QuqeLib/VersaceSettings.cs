using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Reflection;

namespace Quqe
{

  public enum TrainingMethod { Evolve }
  public enum PredictionType { NextClose }
  public enum PreprocessingType { Enhanced }

  public class VersaceSettings
  {
    public string Path { get; private set; }

    public string PredictedSymbol = "DIA";
    public int ExpertsPerMixture = 10;
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
    public int TestingSplitPct = 78;
    public bool UseValidationSet = false;
    public int ValidationSplitPct = 0;

    public List<VGene> ProtoChromosome = new List<VGene> {
        new VGene<int>("NetworkType", 0, 1, 1),
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

    public XElement ToXml()
    {
      return new XElement("VersaceSettings",
        new XAttribute("PredictedSymbol", PredictedSymbol),
        new XAttribute("ExpertsPerMixture", ExpertsPerMixture),
        new XAttribute("PopulationSize", PopulationSize),
        new XAttribute("SelectionSize", SelectionSize),
        new XAttribute("EpochCount", EpochCount),
        new XAttribute("MutationRate", MutationRate),
        new XAttribute("MutationDamping", MutationDamping),
        new XAttribute("StartDate", StartDate),
        new XAttribute("EndDate", EndDate),
        new XAttribute("TestingSplitPct", TestingSplitPct),
        new XAttribute("UseValidationSet", UseValidationSet),
        new XAttribute("ValidationSplitPct", ValidationSplitPct),
        new XAttribute("TrainingMethod", TrainingMethod),
        new XAttribute("PredictionType", PredictionType),
        new XAttribute("PreprocessingType", PreprocessingType),
        new XElement("ProtoChromosome", ProtoChromosome.Select(x => x.ToXml()).ToArray()));
    }

    public static VersaceSettings Load(string fn)
    {
      var vs = Load(XElement.Load(fn));
      vs.Path = fn;
      return vs;
    }

    public static VersaceSettings Load(XElement eSettings)
    {
      return new VersaceSettings {
        PredictedSymbol = eSettings.Attribute("PredictedSymbol").Value,
        ExpertsPerMixture = int.Parse(eSettings.Attribute("ExpertsPerMixture").Value),
        PopulationSize = int.Parse(eSettings.Attribute("PopulationSize").Value),
        SelectionSize = int.Parse(eSettings.Attribute("SelectionSize").Value),
        EpochCount = int.Parse(eSettings.Attribute("EpochCount").Value),
        MutationRate = double.Parse(eSettings.Attribute("MutationRate").Value),
        MutationDamping = double.Parse(eSettings.Attribute("MutationDamping").Value),
        StartDate = DateTime.Parse(eSettings.Attribute("StartDate").Value),
        EndDate = DateTime.Parse(eSettings.Attribute("EndDate").Value),
        TestingSplitPct = int.Parse(eSettings.Attribute("TestingSplitPct").Value),
        UseValidationSet = bool.Parse(eSettings.Attribute("UseValidationSet").Value),
        ValidationSplitPct = int.Parse(eSettings.Attribute("ValidationSplitPct").Value),
        TrainingMethod = (TrainingMethod)Enum.Parse(typeof(TrainingMethod), eSettings.Attribute("TrainingMethod").Value),
        PredictionType = (PredictionType)Enum.Parse(typeof(PredictionType), eSettings.Attribute("PredictionType").Value),
        PreprocessingType = (PreprocessingType)Enum.Parse(typeof(PreprocessingType), eSettings.Attribute("PreprocessingType").Value),
        ProtoChromosome = eSettings.Element("ProtoChromosome").Elements("Gene").Select(x => VGene.Load(x)).ToList()
      };
    }

    public DateTime TrainingStart { get { return StartDate; } }
    public DateTime TrainingEnd { get { return ValidationStart.AddDays(-1); } }
    public DateTime ValidationStart { get { return !UseValidationSet ? TestingStart : StartDate.AddMilliseconds(TestingStart.Subtract(StartDate).TotalMilliseconds * (double)ValidationSplitPct / 100).Date; } }
    public DateTime ValidationEnd { get { return TestingStart.AddDays(-1); } }
    public DateTime TestingStart { get { return StartDate.AddMilliseconds(EndDate.Subtract(StartDate).TotalMilliseconds * (double)TestingSplitPct / 100).Date; } }
    public DateTime TestingEnd { get { return EndDate; } }

    public VersaceSettings Clone()
    {
      return Load(ToXml());
    }
  }
}
