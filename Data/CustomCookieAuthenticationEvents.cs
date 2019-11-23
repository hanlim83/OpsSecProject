using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpsSecProject.Models;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.Data
{
    public class CustomCookieAuthenticationEvents : CookieAuthenticationEvents
    {
        private readonly AuthenticationContext _context;

        public CustomCookieAuthenticationEvents(AuthenticationContext context)
        {
            _context = context;
        }

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            var userPrincipal = context.Principal;
            var currentIdentity = (from c in userPrincipal.Claims where c.Type == "preferred_username" select c.Value).FirstOrDefault();
            User currentUser = _context.Users.Find(currentIdentity);
            if (currentUser == null || currentUser.ForceSignOut == true) {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);
                context.HttpContext.Response.Redirect("/Landing/Login?action=relogin");
            }
        }

    }
}
