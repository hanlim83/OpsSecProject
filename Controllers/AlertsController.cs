using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsSecProject.Data;
using OpsSecProject.Models;
using OpsSecProject.ViewModels;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpsSecProject.Controllers
{
    public class AlertsController : Controller
    {
        private readonly AccountContext _context;
        
        public AlertsController (AccountContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            User user = await _context.Users.Where(u => u.Username == currentIdentity).FirstOrDefaultAsync();
            return View(new AlertsViewModel
            {
                allAlerts = _context.Alerts.Where(a => a.LinkedUserID == user.ID).ToList(),
                successAlerts = _context.Alerts.Where(a => a.LinkedUserID == user.ID).Where(a => a.AlertType.Equals(AlertType.InputIngestSuccess)).ToList().Count(),
                informationalAlerts = _context.Alerts.Where(a => a.LinkedUserID == user.ID).Where(a => a.AlertType.Equals(AlertType.ReportReady) || a.AlertType.Equals(AlertType.InputIngestPending) || a.AlertType.Equals(AlertType.MajorInformationChange)).ToList().Count(),
                warningAlerts = _context.Alerts.Where(a => a.LinkedUserID == user.ID).Where(a => a.AlertType.Equals(AlertType.MetricExceeded) || a.AlertType.Equals(AlertType.InputError)).ToList().Count()
            });
        }

        public async Task<IActionResult> View(int ID)
        {
            Alert a = _context.Alerts.Find(ID);
            if (a == null)
                return StatusCode(404);
            else
            {
                a.Read = true;
                _context.Alerts.Update(a);
                try
                {
                    await _context.SaveChangesAsync();
                    return View(a);
                }
                catch (DbUpdateException)
                {
                    return View(a);
                }
            }
        }
    }
}
