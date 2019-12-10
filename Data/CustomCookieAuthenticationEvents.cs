using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OpsSecProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.Data
{
    public class CustomCookieAuthenticationEvents : CookieAuthenticationEvents
    {
        private readonly AccountContext _Acontext;
        private readonly SecurityContext _Scontext;
        public CustomCookieAuthenticationEvents(AccountContext Acontext, SecurityContext Scontext)
        {
            _Acontext = Acontext;
            _Scontext = Scontext;
        }

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            var userPrincipal = context.Principal;
            var currentIdentity = (from c in userPrincipal.Claims where c.Type == "preferred_username" select c.Value).FirstOrDefault();
            User currentUser = await _Acontext.Users.Where(u => u.Username == currentIdentity).FirstOrDefaultAsync();
            var identityProvider = (from c in userPrincipal.Claims where c.Type == "http://schemas.microsoft.com/identity/claims/identityprovider" select c.Value).FirstOrDefault();
            if (currentUser == null || currentUser.ForceSignOut == true || currentUser.LastAuthentication.CompareTo(currentUser.LastPasswordChange) < 0)
            {
                context.RejectPrincipal();
                if (identityProvider.Equals("https://smartinsights.hansen-lim.me"))
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                else
                    await context.HttpContext.SignOutAsync(AzureADDefaults.AuthenticationScheme);
            }
            else if (currentUser != null)
            {
                Activity activity = new Activity
                {
                    Page = context.HttpContext.Request.Path.ToString().Substring(1, context.HttpContext.Request.Path.Value.Length - 1).Replace("/", " ") + context.HttpContext.Request.QueryString.ToString(),
                    LinkedUserID = currentUser.ID,
                    Timestamp = DateTime.Now
                };
                if (context.Request.Method.Equals("GET"))
                    activity.Action = Models.Action.View;
                else if (context.Request.Method.Equals("POST"))
                    activity.Action = Models.Action.Edit;
                if (context.Response.HttpContext.Response.StatusCode <= 308)
                    activity.Status = true;
                else
                    activity.Status = false;
                if (!context.HttpContext.Request.Path.Value.Equals("/"))
                {
                    _Scontext.Activities.Add(activity);
                    await _Scontext.SaveChangesAsync();
                }
            }
        }
    }
}
