namespace Quqe.Rabbit
{
  public class BroadcastInfo
  {
    public readonly string Host;
    public readonly string Channel;

    public BroadcastInfo(string host, string channel)
    {
      Host = host;
      Channel = channel;
    }
  }
}
