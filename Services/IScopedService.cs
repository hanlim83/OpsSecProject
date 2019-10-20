using System.Threading.Tasks;

namespace OpsSecProject.Services
{
    internal interface IScopedService
    {
        Task DoWorkAsync();
    }
}
