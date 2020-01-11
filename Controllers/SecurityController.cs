using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.Data;
using OpsSecProject.Models;
using System.Collections.Generic;
using System.Linq;

namespace OpsSecProject.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class SecurityController : Controller
    {

        private readonly AccountContext _context;

        public SecurityController(AccountContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            List<Activity> Activites = _context.Activities.ToList();
            return View(Activites);
        }
    }
}