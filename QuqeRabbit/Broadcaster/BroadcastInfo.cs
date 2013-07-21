namespace Quqe.Rabbit
{
  public class BroadcastInfo
  {
    public readonly RabbitHostInfo Host;
    public readonly string Channel;
    public readonly bool SendOnly;

    public BroadcastInfo(RabbitHostInfo host, string channel, bool sendOnly = false)
    {
      Host = host;
      Channel = channel;
      SendOnly = sendOnly;
    }
  }
}
