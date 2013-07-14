namespace Quqe.Rabbit
{
  public class BroadcastInfo
  {
    public readonly RabbitHostInfo Host;
    public readonly string Channel;

    public BroadcastInfo(RabbitHostInfo host, string channel)
    {
      Host = host;
      Channel = channel;
    }
  }
}
