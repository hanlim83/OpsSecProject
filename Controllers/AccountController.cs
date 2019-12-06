using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsSecProject.Data;
using OpsSecProject.Models;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Net;

namespace OpsSecProject.Controllers
{
    public class AccountController : Controller
    {

        private readonly AuthenticationContext _context;

        public AccountController(AuthenticationContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            User currentUser = await _context.Users.FindAsync(currentIdentity);
            return View(currentUser);
        }

        public async Task<IActionResult> Profile()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            User currentUser = await _context.Users.FindAsync(currentIdentity);
            return View(currentUser);
        }

        public async Task<IActionResult> Settings()
        {
            return View();
        }

        public async Task<IActionResult> Activity()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Unauthorised()
        {
            ViewData["RequestID"] = HttpContext.TraceIdentifier;
            return View();
        }

        public IActionResult Claims()
        {
            ViewData["User"] = HttpContext.User;
            return View();
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Manage()
        {
            AccountManagementViewModel model = new AccountManagementViewModel
            {
                allUsers = await _context.Users.ToListAsync(),
                allRoles = await _context.Roles.ToListAsync()
            };
            return View(model);
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ChangeAccountStatus(string Username)
        {
            User identity = await _context.Users.FindAsync(WebUtility.HtmlDecode(Username));
            if (identity == null)
                return StatusCode(404);
            else if (identity.Name.Equals(User.Claims.First(c => c.Type == "name").Value))
                return StatusCode(403);
            else if (identity.Existence == Existence.External)
                return StatusCode(500);
            else
            {
                if (identity.Status == Status.Active)
                {
                    identity.Status = Status.Disabled;
                    TempData["Message"] = "Succesfully disabled "+identity.Name+"'s account";
                }
                else if (identity.Status == Status.Disabled)
                {
                    identity.Status = Status.Active;
                    TempData["Message"] = "Succesfully enabled " + identity.Name + "'s account";
                }                 
                _context.Users.Update(identity);
                await _context.SaveChangesAsync();
                TempData["Alert"] = "Success";
                return RedirectToAction("Manage");
            }
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> RemoveAccount(string Username)
        {
            User identity = await _context.Users.FindAsync(WebUtility.HtmlDecode(Username));
            if (identity == null)
                return StatusCode(404);
            else if (identity.Name.Equals(User.Claims.First(c => c.Type == "name").Value))
                return StatusCode(403);
            else
            {
                _context.Users.Remove(identity);
                await _context.SaveChangesAsync();
                TempData["Alert"] = "Success";
                TempData["Message"] = "Succesfully removed " + identity.Name + "'s account";
                return RedirectToAction("Manage");
            }
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