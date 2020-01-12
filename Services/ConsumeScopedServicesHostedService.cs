using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpsSecProject.Services
{
    internal class ConsumeScopedServicesHostedService : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private Timer _timer;

        public ConsumeScopedServicesHostedService(ILogger<ConsumeScopedServicesHostedService> logger, IServiceProvider services)
        {
            _logger = logger;
            Services = services;
        }
        public IServiceProvider Services { get; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Setup Background Service is about to start.");

            _timer = new Timer(DoWorkAsyncSetup, null, TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(-1));

            return Task.CompletedTask;
        }

        private async void DoWorkAsyncSetup(object state)
        {
            using (var scope = Services.CreateScope())
            {
                var ScopedSetupService =
                    scope.ServiceProvider
                        .GetRequiredService<IScopedSetupService>();
                try
                {
                    await ScopedSetupService.DoWorkAsync();
                }
                catch (Exception)
                {

                }
            }
        }
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Setup Background Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
