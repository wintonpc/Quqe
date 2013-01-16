using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;

namespace Quqe
{
  public class VersaceResult
  {
    public string Path { get; private set; }
    public VMixture BestMixture;
    public List<double> FitnessHistory;
    public List<double> DiversityHistory;
    public VersaceSettings VersaceSettings;

    public VersaceResult(VMixture bestMixture, List<double> fitnessHistory, List<double> diversityHistory,
      VersaceSettings settings, string path = null)
    {
      BestMixture = bestMixture;
      FitnessHistory = fitnessHistory;
      DiversityHistory = diversityHistory;
      VersaceSettings = settings;
      Path = path;
    }

    public VersaceResult(VMixture bestMixture, List<PopulationInfo> history, VersaceSettings settings, string path = null)
      : this(bestMixture, history.Select(x => x.Fitness).ToList(), history.Select(x => x.Diversity).ToList(), settings, path)
    {
    }

    public void Save()
    {
      if (!Directory.Exists("VersaceResults"))
        Directory.CreateDirectory("VersaceResults");
      Path = string.Format("VersaceResults\\VersaceResult-{0:yyyyMMdd}-{0:HHmmss}.xml", DateTime.Now);
      new XElement("VersaceResult",
        new XElement("FitnessHistory", QUtil.DoublesToBase64(FitnessHistory)),
        new XElement("DiversityHistory", QUtil.DoublesToBase64(DiversityHistory)),
        VersaceSettings.ToXml(),
        BestMixture.ToXml())
        .Save(Path);
    }

    public static VersaceResult Load(string fn)
    {
      var vr = XElement.Load(fn);
      return new VersaceResult(
        VMixture.Load(vr.Element("Mixture")),
        QUtil.DoublesFromBase64(vr.Element("FitnessHistory").Value).ToList(),
        QUtil.DoublesFromBase64(vr.Element("DiversityHistory").Value).ToList(),
        VersaceSettings.Load(vr.Element("VersaceSettings")),
        fn);
    }
  }
}
