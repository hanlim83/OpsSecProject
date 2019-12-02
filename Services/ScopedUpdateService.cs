using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace OpsSecProject.Services
{
    internal class ScopedUpdateService : IScopedUpdateService
    {
        private readonly ILogger _logger;
        public ScopedUpdateService(ILogger<ScopedUpdateService> logger)
        {
            _logger = logger;
        }

        public Task DoWorkAsync()
        {
            throw new NotImplementedException();
        }
    }
}
