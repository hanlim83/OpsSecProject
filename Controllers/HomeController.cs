using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace OpsSecProject.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Search(string query)
        {
            return View();
        }
    }
}
