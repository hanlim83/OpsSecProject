using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.Data;
using OpsSecProject.Models;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpsSecProject.Controllers
{
    public class AccountController : Controller
    {

        private readonly AuthenticationContext _context;

        public AccountController(AuthenticationContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login([Bind("Username", "Password")]LoginFormModel Credentials)
        {
            User challenge = _context.Users.Find(Credentials.Username);
            if (challenge == null)
            {
                ViewData["Alert"] = "Warning";
                ViewData["Message"] = "Invaild Username and Password combination";
                return View();
            }
            else if (challenge.Existence.Equals(Existence.External))
            {
                ViewData["Alert"] = "Danger";
                ViewData["Message"] = "Please login through the identity provider instead";
                return View();
            }
            else if (!Password.ValidatePassword(Credentials.Password, challenge.Password))
            {
                ViewData["Alert"] = "Warning";
                ViewData["Message"] = "Invaild Username and Password combination";
                return View();
            }
            else
            {
                challenge.ForceSignOut = false;
                challenge.LastSignedIn = DateTime.Now;
                var claims = new List<Claim>{
                    new Claim("name", challenge.Name),
                    new Claim("preferred_username", challenge.Username),
                    new Claim(ClaimTypes.Role, challenge.LinkedRole.RoleName),
                    new Claim("http://schemas.microsoft.com/identity/claims/identityprovider", "https://smartinsights.hansen-lim.me")
                };
                if (challenge.VerifiedPhoneNumber == true)
                    claims.Add(new Claim(ClaimTypes.MobilePhone, challenge.PhoneNumber));
                if (challenge.VerifiedEmail == true)
                    claims.Add(new Claim(ClaimTypes.Email, challenge.EmailAddress));
                var claimsIdentity = new ClaimsIdentity(
                    claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
                _context.Update(challenge);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Home");
            }
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
        public async Task<IActionResult> ResetPassword([Bind("Token", "NewPassword", "ConfirmPassword")]NewPasswordModel NewCredentials)
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
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string authMethod = claimsIdentity.FindFirst("http://schemas.microsoft.com/identity/claims/identityprovider").Value;
            if (authMethod.Equals("https://smartinsights.hansen-lim.me"))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Signout", "Landing");
            }
            else if (!authMethod.Equals("https://smartinisights.hansen-lim.me"))
            {
                foreach (var cookieKey in HttpContext.Request.Cookies.Keys)
                {
                    HttpContext.Response.Cookies.Delete(cookieKey);
                }
                return RedirectToAction("Logout", "Landing");
            }
            else
            {
                return StatusCode(500);
            }
        }
    }
}