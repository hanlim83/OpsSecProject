using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.Data;
using OpsSecProject.Helpers;
using OpsSecProject.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Activity = System.Diagnostics.Activity;

namespace OpsSecProject.Controllers
{
    [AllowAnonymous]
    public class LandingController : Controller
    {
        private readonly AccountContext _context;

        public LandingController(AccountContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");
            else
                return RedirectToAction("RealmDiscovery");
        }

        public IActionResult RealmDiscovery(string ReturnUrl)
        {
            if (ReturnUrl != null)
                ViewData["ReturnURL"] = ReturnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RealmDiscovery([Bind("Username", "recaptchaResponse", "ReturnUrl")]RealmDiscoveryModel user)
        {
            if (await GoogleRecaptchaHelper.IsReCaptchaV2PassedAsync(user.recaptchaResponse))
            {
                User identity = _context.Users.Where(u => u.Username == user.Username).FirstOrDefault();
                if (identity == null)
                {
                    if (user.Username.Contains("@"))
                    {
                        var authenticationProperties = new AuthenticationProperties();
                        authenticationProperties.Items["login_hint"] = user.Username;
                        if (user.ReturnUrl == null)
                            authenticationProperties.RedirectUri = "/Landing/";
                        else
                            authenticationProperties.RedirectUri = user.ReturnUrl;
                        return Challenge(authenticationProperties, AzureADDefaults.AuthenticationScheme);
                    } else
                    {
                        ViewData["Alert"] = "Danger";
                        ViewData["Message"] = "We can't find an account with that Username";
                        return View(user);
                    }
                } else if (identity.Existence != Existence.Internal)
                {
                    var authenticationProperties = new AuthenticationProperties();
                    authenticationProperties.Items["login_hint"] = user.Username;
                    if (user.ReturnUrl == null)
                        authenticationProperties.RedirectUri = "/Landing/";
                    else
                        authenticationProperties.RedirectUri = user.ReturnUrl;
                    return Challenge(authenticationProperties, AzureADDefaults.AuthenticationScheme);
                }
                else
                {
                    TempData["Username"] = identity.Username;
                    TempData["ReturnUrl"] = user.ReturnUrl;
                    return Redirect("/Internal/Account/SignIn");
                }
            }
            else
            {
                ViewData["Alert"] = "Warning";
                ViewData["Message"] = "Unable to verify reCAPTCHA! Please try again";
                return View(user);
            }
        }

        public IActionResult Login(string ReturnUrl)
        {
            if (!String.IsNullOrEmpty(ReturnUrl))
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
            return Challenge(authenticationProperties, AzureADDefaults.AuthenticationScheme);
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