using Microsoft.AspNetCore.Mvc;

namespace OpsSecProject.Areas.Development.Controllers
{
    [Area("Development")]
    public class ExamplesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Charts()
        {
            return View();
        }
        public IActionResult Table()
        {
            return View();
        }
        public IActionResult Buttons()
        {
            return View();
        }
        public IActionResult Cards()
        {
            return View();
        }
        public IActionResult Colours()
        {
            return View();
        }
        public IActionResult Borders()
        {
            return View();
        }
        public IActionResult Animations()
        {
            return View();
        }
        public IActionResult Others()
        {
            return View();
        }
    }
}
