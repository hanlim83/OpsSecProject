using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpsSecProject.Controllers
{
    public class AccountController : Controller
    {
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login([Bind("Username", "Password")]LoginFormModel Credentials)
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult ForgetPassword()
        {
            return View();
        }
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> ForgetPassword([Bind("Username")]ForgetPasswordModel User)
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult ResetPassword(string token)
        {
            if (token == null)
                return StatusCode(403);
            else
            {
                NewPasswordModel model = new NewPasswordModel
                {
                    Token = token
                };
                return View(model);
            }
        }
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> ResetPassword([Bind("Token","NewPassword","ConfirmPassword")]NewPasswordModel NewCredentials)
        {
            return View();
        }

        public IActionResult Unauthorised()
        {
            return View();
        }

        public IActionResult Claims()
        {
            ViewData["User"] = HttpContext.User;
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            string authMethod = HttpContext.User.FindFirstValue("AuthMethod");
            if (authMethod.Equals("External"))
            {
                foreach (var cookieKey in HttpContext.Request.Cookies.Keys)
                {
                    HttpContext.Response.Cookies.Delete(cookieKey);
                }
                return RedirectToAction("Logout", "Landing");
            }
            else if (authMethod.Equals("Internal"))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Signout", "Landing");
            }
            else
            {
                return StatusCode(500);
            }
        }
    }
}