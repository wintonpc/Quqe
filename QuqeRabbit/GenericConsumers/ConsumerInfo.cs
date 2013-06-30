namespace Quqe.Rabbit
{
  class ConsumerInfo
  {
    public readonly string Host;
    public readonly string QueueName;
    public readonly bool RequireAck;
    public readonly bool IsPersistent;
    public readonly ushort PrefetchCount;

    public ConsumerInfo(string host, string queueName, bool requireAck, bool isPersistent, ushort prefetchCount)
    {
      Host = host;
      QueueName = queueName;
      RequireAck = requireAck;
      IsPersistent = isPersistent;
      PrefetchCount = prefetchCount;
    }
  }
}
