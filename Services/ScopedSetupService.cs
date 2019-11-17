using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace OpsSecProject.Services
{
    internal class ScopedSetupService : IScopedSetupService
    {
        private readonly ILogger _logger;
        public ScopedSetupService(ILogger<ScopedSetupService> logger)
        {
            _logger = logger;
        }

        public Task DoWorkAsync()
        {
            throw new NotImplementedException();
        }
    }
}
