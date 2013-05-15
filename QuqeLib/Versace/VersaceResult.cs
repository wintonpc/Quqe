using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using PCW;

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
  }
}
