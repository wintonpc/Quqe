using System.Security.Permissions;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using Quqe.Rabbit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuqeTest
{
  static class RabbitTestHelper
  {
    public static void PurgeQueue(string name)
    {
      using (var conn = Helpers.MakeConnection("localhost"))
      using (var model = conn.CreateModel())
        model.QueuePurge(name);
    }

    public static void StartRabbitService()
    {
      RequireAdministrator();
      var rabbitService = ServiceController.GetServices().Single(x => x.ServiceName == "RabbitMQ");
      rabbitService.Start();
      rabbitService.WaitForStatus(ServiceControllerStatus.Running);
    }

    public static void StopRabbitService()
    {
      RequireAdministrator();
      var rabbitService = ServiceController.GetServices().Single(x => x.ServiceName == "RabbitMQ");
      rabbitService.Stop();
      rabbitService.WaitForStatus(ServiceControllerStatus.Stopped);
    }

    public static void RestartRabbitService()
    {
      StopRabbitService();
      StartRabbitService();
    }

    static void RequireAdministrator()
    {
      AppDomain.CurrentDomain.SetPrincipalPolicy(PrincipalPolicy.WindowsPrincipal);
      Thread.CurrentPrincipal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
      PrincipalPermission principalPerm = new PrincipalPermission(null, "Administrators");
      principalPerm.Demand();
    }
  }
}
