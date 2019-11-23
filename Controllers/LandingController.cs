using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.Models;
using System.Diagnostics;

namespace OpsSecProject.Controllers
{
    [AllowAnonymous]
    public class LandingController : Controller
    {
        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");
            else
                return RedirectToAction("Login");
        }

        public IActionResult Login(string ReturnUrl)
        {
            if (ReturnUrl != null)
                ViewData["ReturnURL"] = ReturnUrl;
            return View();
        }

        public IActionResult Logout()
        {
            return View();
        }

        public IActionResult Signout()
        {
            return View();
        }

        public IActionResult Unauthenticated()
        {
            return View();
        }

        public IActionResult Reauthenticate()
        {
            var authenticationProperties = new AuthenticationProperties();
            authenticationProperties.Items["prompt"] = "login";
            authenticationProperties.RedirectUri = "/Landing/";
            return Challenge(authenticationProperties,AzureADDefaults.AuthenticationScheme);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(string code)
        {
            var statusCodeReExecuteFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            if (statusCodeReExecuteFeature != null)
                return View(new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    OriginalPath = statusCodeReExecuteFeature.OriginalPathBase + statusCodeReExecuteFeature.OriginalPath + statusCodeReExecuteFeature.OriginalQueryString,
                    ErrorStatusCode = code
                });
            else
                return View(new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ErrorStatusCode = code
                });
        }
    }
}