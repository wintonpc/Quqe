using Quqe.Rabbit;

namespace Quqe.NewVersace
{
  public class TrainRequest : RabbitMessage
  {
    public string MixtureId { get; private set; }
    public Chromosome Chromosome { get; private set; }

    public TrainRequest(string mixtureId, Chromosome chromosome)
    {
      MixtureId = mixtureId;
      Chromosome = chromosome;
    }
  }

  public class TrainNotification : RabbitMessage
  {
    public TrainRequest OriginalRequest { get; private set; }

    public TrainNotification(TrainRequest originalRequest)
    {
      OriginalRequest = originalRequest;
    }
  }
}