using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quqe.Rabbit;

namespace VersaceExe
{
  class HostStartEvolution : RabbitMessage
  {
    public MasterRequest MasterRequest { get; private set; }

    public HostStartEvolution(MasterRequest masterRequest)
    {
      MasterRequest = masterRequest;
    }
  }

  class HostStopEvolution : RabbitMessage
  {
  }

  class HostShutdown : RabbitMessage
  {
  }

  public enum SignalType
  {
    NextClose
  }

  public class MasterRequest : RabbitMessage
  {
    public string ProtoRunName { get; private set; }
    public string Symbol { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public double ValidationPct { get; private set; }
    public SignalType SignalType { get; private set; }

    public MasterRequest(string protoRunName, string symbol, DateTime startDate, DateTime endDate, double validationPct, SignalType signalType)
    {
      ProtoRunName = protoRunName;
      Symbol = symbol;
      StartDate = startDate;
      EndDate = endDate;
      ValidationPct = validationPct;
      SignalType = signalType;
    }
  }
}
