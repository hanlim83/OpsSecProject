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
        private readonly AccountContext _accountContext;
        private readonly LogContext _logContext;

        public AlertsController(AccountContext accountContext, LogContext logContext)
        {
            _accountContext = accountContext;
            _logContext = logContext;
        }
        public async Task<IActionResult> Index()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            User user = await _accountContext.Users.Where(u => u.Username == currentIdentity).FirstOrDefaultAsync();
            return View(new AlertsViewModel
            {
                allAlerts = _accountContext.Alerts.Where(a => a.LinkedUserID == user.ID).ToList(),
                successAlerts = _accountContext.Alerts.Where(a => a.LinkedUserID == user.ID).Where(a => a.AlertType.Equals(AlertType.InputIngestSuccess)).ToList().Count(),
                informationalAlerts = _accountContext.Alerts.Where(a => a.LinkedUserID == user.ID).Where(a => a.AlertType.Equals(AlertType.ReportReady) || a.AlertType.Equals(AlertType.InputIngestPending) || a.AlertType.Equals(AlertType.MajorInformationChange)).ToList().Count(),
                warningAlerts = _accountContext.Alerts.Where(a => a.LinkedUserID == user.ID).Where(a => a.AlertType.Equals(AlertType.MetricExceeded) || a.AlertType.Equals(AlertType.InputError)).ToList().Count()
            });
        }
        public async Task<IActionResult> Manage(int ID)
        {
            return View(await _logContext.AlertTriggers.Where(A => A.LinkedLogInputID.Equals(ID)).ToListAsync());
        }
        public async Task<IActionResult> Create(int ID)
        {
            return View(await _logContext.LogInputs.FindAsync(ID));
        }
        [HttpPost]
        public async Task<IActionResult> Create([Bind("Condition","CondtionalField","ConditionType","AlertTriggerType","LinkedLogInputID")]Trigger AlertTrigger)
        {
            _logContext.AlertTriggers.Add(AlertTrigger);
            await _logContext.SaveChangesAsync();
            return RedirectToAction("Manage", new { ID = AlertTrigger.LinkedLogInputID });
        }
        public async Task<IActionResult> Edit(int ID)
        {
            return View(await _logContext.AlertTriggers.FindAsync(ID));
        }
        [HttpPost]
        public async Task<IActionResult> Edit([Bind("Condition", "CondtionalField", "ConditionType", "AlertTriggerType", "LinkedLogInputID")]Trigger AlertTrigger)
        {
            _logContext.AlertTriggers.Update(AlertTrigger);
            await _logContext.SaveChangesAsync();
            return RedirectToAction("Manage", new { ID = AlertTrigger.LinkedLogInputID });
        }
        public async Task<IActionResult> Remove(int ID)
        {
            Trigger deleted = _logContext.AlertTriggers.Find(ID);
            _logContext.AlertTriggers.Remove(deleted);
            await _logContext.SaveChangesAsync();
            return RedirectToAction("Manage", new { ID = deleted.LinkedLogInputID });
        }
        public async Task<IActionResult> Read(int ID)
        {
            Alert a = _accountContext.Alerts.Find(ID);
            if (a == null)
                return StatusCode(404);
            else
            {
                a.Read = true;
                _accountContext.Alerts.Update(a);
                try
                {
                    await _accountContext.SaveChangesAsync();
                    return RedirectToAction("Index");
                }
                catch (DbUpdateException)
                {
                    return RedirectToAction("Index");
                }
            }
        }
    }
}
