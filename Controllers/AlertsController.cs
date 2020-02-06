using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsSecProject.Data;
using OpsSecProject.Models;
using OpsSecProject.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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
                successAlerts = _accountContext.Alerts.Where(a => a.LinkedUserID == user.ID).Where(a => a.AlertType.Equals(AlertType.InputIngestSuccess) || a.AlertType.Equals(AlertType.SageMakerTrained) || a.AlertType.Equals(AlertType.SageMakerDeployed)).ToList().Count(),
                informationalAlerts = _accountContext.Alerts.Where(a => a.LinkedUserID == user.ID).Where(a => a.AlertType.Equals(AlertType.SageMakerBatchTransformCompleted) || a.AlertType.Equals(AlertType.InputIngestPending) || a.AlertType.Equals(AlertType.MajorInformationChange)).ToList().Count(),
                warningAlerts = _accountContext.Alerts.Where(a => a.LinkedUserID == user.ID).Where(a => a.AlertType.Equals(AlertType.MetricExceeded) || a.AlertType.Equals(AlertType.SageMakerPredictionTriggered)).ToList().Count()
            });
        }
        public async Task<IActionResult> Manage(int LogInputID)
        {
            ViewData["LogInputID"] = LogInputID;
            return View(await _logContext.AlertTriggers.Where(A => A.LinkedLogInputID.Equals(LogInputID)).ToListAsync());
        }
        public async Task<IActionResult> Create(int LogInputID)
        {
            ViewData["LogInputID"] = LogInputID;
            LogInput retrieved = await _logContext.LogInputs.FindAsync(LogInputID);
            string dbTableName = "dbo." + retrieved.LinkedS3Bucket.Name.Replace("-", "_");
            ViewBag.fields = new List<string>();
            using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand(@"SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(@TableName);", connection))
                {
                    cmd.CommandTimeout = 0;
                    cmd.Parameters.AddWithValue("@TableName", dbTableName);
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            ViewBag.fields.Add(dr.GetString(0));
                        }
                    }
                }
            }
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Create([Bind("Name", "Condtion", "CondtionalField", "CondtionType", "AlertTriggerType", "LinkedLogInputID")]Trigger AlertTrigger)
        {
            _logContext.AlertTriggers.Add(AlertTrigger);
            await _logContext.SaveChangesAsync();
            return RedirectToAction("Manage", new { LogInputID = AlertTrigger.LinkedLogInputID });
        }
        public async Task<IActionResult> Edit(int TriggerID)
        {
            Trigger alert = await _logContext.AlertTriggers.FindAsync(TriggerID);
            LogInput retrieved = alert.LinkedLogInput;
            string dbTableName = "dbo." + retrieved.LinkedS3Bucket.Name.Replace("-", "_");
            ViewBag.fields = new List<string>();
            using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand(@"SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(@TableName);", connection))
                {
                    cmd.CommandTimeout = 0;
                    cmd.Parameters.AddWithValue("@TableName", dbTableName);
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            ViewBag.fields.Add(dr.GetString(0));
                        }
                    }
                }
            }
            return View(alert);
        }
        [HttpPost]
        public async Task<IActionResult> Edit([Bind("Name", "Condtion", "CondtionalField", "CondtionType", "AlertTriggerType")]Trigger AlertTrigger)
        {
            _logContext.AlertTriggers.Update(AlertTrigger);
            await _logContext.SaveChangesAsync();
            return RedirectToAction("Manage", new { LogInputID = AlertTrigger.LinkedLogInputID });
        }
        public async Task<IActionResult> Remove(int TriggerID)
        {
            Trigger deleted = _logContext.AlertTriggers.Find(TriggerID);
            _logContext.AlertTriggers.Remove(deleted);
            await _logContext.SaveChangesAsync();
            return RedirectToAction("Manage", new { LogInputID = deleted.LinkedLogInputID });
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
        private static string GetRdsConnectionString()
        {
            string hostname = Environment.GetEnvironmentVariable("RDS_HOSTNAME");
            string port = Environment.GetEnvironmentVariable("RDS_PORT");
            string username = Environment.GetEnvironmentVariable("RDS_USERNAME");
            string password = Environment.GetEnvironmentVariable("RDS_PASSWORD");

            return $"Data Source={hostname},{port};Initial Catalog=IngestedData;User ID={username};Password={password};";
        }
    }
}
