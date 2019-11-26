using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.Areas.Internal.Data;
using OpsSecProject.Areas.Internal.Models;
using OpsSecProject.Data;
using OpsSecProject.Models;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpsSecProject.Areas.Internal.Controllers
{
    [Area("Internal")]
    public class AccountController : Controller
    {

        private readonly AuthenticationContext _context;

        public AccountController(AuthenticationContext context)
        {
            _context = context;
        }
        [AllowAnonymous]
        public IActionResult SignIn(string ReturnUrl)
        {
            if (ReturnUrl != null)
                ViewData["ReturnURL"] = ReturnUrl;
            return View();
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> SignIn([Bind("Username", "Password", "ReturnUrl")]LoginFormModel Credentials)
        {
            User challenge = _context.Users.Find(Credentials.Username);
            if (challenge == null)
            {
                ViewData["Alert"] = "Warning";
                ViewData["Message"] = "Invaild Username and Password combination";
                Credentials.Password = null;
                return View(Credentials);
            }
            else if (challenge.Existence.Equals(Existence.External))
            {
                ViewData["Alert"] = "Danger";
                ViewData["Message"] = "Please login through the identity provider instead";
                Credentials.Password = null;

                return View(Credentials);
            }
            else if (!Password.ValidatePassword(Credentials.Password, challenge.Password))
            {
                ViewData["Alert"] = "Warning";
                ViewData["Message"] = "Invaild Username and Password combination";
                Credentials.Password = null;
                return View(Credentials);
            }
            else
            {
                challenge.ForceSignOut = false;
                challenge.LastAuthentication = DateTime.Now;
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

                var authProperties = new AuthenticationProperties();

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
                _context.Update(challenge);
                await _context.SaveChangesAsync();
                if (Credentials.ReturnUrl == null)
                    return Redirect("/Home");
                else
                    return Redirect(Credentials.ReturnUrl);
            }
        }
        [AllowAnonymous]
        public IActionResult ForgetPassword()
        {
            return View();
        }
        [HttpPost]
        [AllowAnonymous]
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
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([Bind("Token", "NewPassword", "ConfirmPassword")]NewPasswordModel NewCredentials)
        {
            return View();
        }

        public async Task<IActionResult> ChangePassword()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> ChangePassword([Bind("Username","CurrentPassword", "NewPassword", "ConfirmPassword")]ChangePasswordModel NewCredentials)
        {
            return View();
        }
    }
}