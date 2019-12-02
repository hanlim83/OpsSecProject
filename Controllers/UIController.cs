using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace OpsSecProject.Controllers
{
    public class UIController : Controller
    {
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
        

    }
}