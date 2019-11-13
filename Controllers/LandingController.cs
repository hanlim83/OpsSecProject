using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.Models;
using System.Diagnostics;

namespace OpsSecProject.Controllers
{
    public class LandingController : Controller
    {
        [AllowAnonymous]
        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");
            else
                return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Logout()
        {
            return View();
        }

        public IActionResult Claims()
        {
            ViewData["User"] = HttpContext.User;
            return View();
        }
        public IActionResult Unauthorised()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Unauthenticated()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}