namespace Quqe.Rabbit
{
  public class WorkQueueInfo
  {
    public readonly RabbitHostInfo Host;
    public readonly string Name;
    public readonly bool IsPersistent;

    public WorkQueueInfo(RabbitHostInfo host, string name, bool isPersistent)
    {
      Host = host;
      Name = name;
      IsPersistent = isPersistent;
    }
  }
}
