using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
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
            var identityProvider = (from c in userPrincipal.Claims where c.Type == "http://schemas.microsoft.com/identity/claims/identityprovider" select c.Value).FirstOrDefault();
            if (currentUser == null || currentUser.ForceSignOut == true || currentUser.LastAuthentication.CompareTo(currentUser.LastPasswordChange) < 0) {
                context.RejectPrincipal();
                if (identityProvider.Equals("https://smartinsights.hansen-lim.me"))
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                else
                    await context.HttpContext.SignOutAsync(AzureADDefaults.AuthenticationScheme);
            }
        }

    }
}
