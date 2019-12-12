using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.Data;
using OpsSecProject.Models;

namespace OpsSecProject.Controllers
{
    [Authorize(Roles = "Administrator, Power User")]
    public class InputsController : Controller
    {
        private readonly LogContext _logContext;

        public InputsController (LogContext logContext)
        {
            _logContext = logContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(string FilePath)
        {

            ViewBag.LogPath = FilePath;
            using (StreamWriter writer = new StreamWriter("wwwroot\\FilePath.txt"))
            {
                writer.WriteLine(FilePath);
            }
            return RedirectToAction("Index");
        }

        public IActionResult TestCreate()
        {
            _logContext.LogInputs.Add(new LogInput
            {
                FilePath = "SYSTEM32/COMMAND PROMPT",
                LinkedUserName = "TOM",
                Name = "TOTALLY LEGIT"
            });
            _logContext.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}