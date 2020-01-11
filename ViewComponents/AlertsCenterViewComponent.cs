using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsSecProject.Data;
using OpsSecProject.Models;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpsSecProject.ViewComponents
{
    public class AlertsCenterViewComponent : ViewComponent
    {
        private readonly AccountContext _context;

        public AlertsCenterViewComponent (AccountContext context)
        {
            _context = context;
        }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            User user = await _context.Users.Where(u => u.Username == currentIdentity).FirstOrDefaultAsync();
            List<Alert> alerts = await GetAlertsAsync(user.ID);
            if (alerts.Count() < 3)
                return View(alerts);
            else
                return View(alerts.GetRange(0,3));
        }
        private Task<List<Alert>> GetAlertsAsync(int UserID)
        {
            return _context.Alerts.Where(a => a.LinkedUserID == UserID && a.Read == false).OrderByDescending(a => a.TimeStamp).ToListAsync();
        }
    }
}
