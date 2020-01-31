using System.IO;
using System.Text;
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

        public IActionResult Json()
        {
            return View();
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(string FilePath, string InputName, string Filter, string LogType)
        {

            ViewBag.LogPath = FilePath;
            ViewBag.LogName = InputName;
            ViewBag.Filter = Filter;
            ViewBag.LogType = LogType;

            using (StreamWriter writer = new StreamWriter("wwwroot\\FilePath.txt"))
            {
                writer.WriteLine(
                    "{ \n" + 
                    "\"Sources\" : [ \n " + 
                    "{ \n" +
                    "\"Id\" : \"WindowsEventLog\","



                    );
            }
            return RedirectToAction("Json");
        }

        public IActionResult TestCreate()
        {
            _logContext.LogInputs.Add(new LogInput
            {
                FilePath = "SYSTEM32/COMMAND PROMPT",
                LinkedUserID = 1,
                Name = "TOTALLY LEGIT"
            });
            _logContext.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}