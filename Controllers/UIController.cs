using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.Data;
using OpsSecProject.Models;

namespace OpsSecProject.Controllers
{
    public class UIController : Controller
    {

        private readonly LogContext logContext;

        public UIController(LogContext _context) {
            logContext = _context;
        }

        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Index(string FilePath)
        {
            
            ViewBag.LogPath = FilePath;
            using (StreamWriter writer = new StreamWriter("wwwroot\\FilePath.txt")) {
                writer.WriteLine(FilePath);
            }
            return View();
        }

        public IActionResult TestCreate() { 
            logContext.LogInputs.Add ( new LogInput { 
                FilePath = "SYSTEM32/COMMAND PROMPT",
                LinkedUserName = "TOM",
                Name = "TOTALLY LEGIT"
            });
            logContext.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}