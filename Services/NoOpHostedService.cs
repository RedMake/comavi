namespace COMAVI_SA.Services;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

public class NoOpHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
