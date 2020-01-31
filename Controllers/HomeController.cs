using Microsoft.AspNetCore.Mvc;
using OpsSecProject.Data;
using OpsSecProject.Models;
using OpsSecProject.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace OpsSecProject.Controllers
{
    public class HomeController : Controller
    {
        private readonly LogContext _context;
        public HomeController(LogContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Search(string query)
        {
            return View();
        }

        public async Task<IActionResult> Streaming(int InputID)
        {
            if (InputID <= 0)
                return StatusCode(404);
            LogInput retrieved = await _context.LogInputs.FindAsync(InputID);
            if (retrieved == null)
                return StatusCode(404);
            string dbTableName = retrieved.LinkedS3Bucket.Name.Replace("-", "_");
            List<ApacheWebLog> webLogs = new List<ApacheWebLog>();
            using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM dbo." + dbTableName, connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (retrieved.LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                            {
                                ApacheWebLog newItem = new ApacheWebLog();
                                if (!dr.IsDBNull(0))
                                    newItem.host = dr.GetString(0);
                                if (!dr.IsDBNull(1))
                                    newItem.ident = dr.GetString(1);
                                if (!dr.IsDBNull(2))
                                    newItem.authuser = dr.GetString(2);
                                if (!dr.IsDBNull(3))
                                    newItem.datetime = dr.GetString(3);
                                if (!dr.IsDBNull(4))
                                    newItem.request = dr.GetString(4);
                                if (!dr.IsDBNull(5))
                                    newItem.response = dr.GetString(5);
                                if (!dr.IsDBNull(6))
                                    newItem.bytes = Convert.ToInt32(dr.GetString(6));
                                if(dr.FieldCount == 9)
                                {
                                    if (!dr.IsDBNull(7))
                                        newItem.referer = dr.GetString(7);
                                    if (!dr.IsDBNull(8))
                                        newItem.agent = dr.GetString(8);
                                }
                                webLogs.Add(newItem);
                            }

                        }
                    }
                }
            }
            return View(new StreamingOverrallViewModel
            {
                input = retrieved,
                webLogs = webLogs
            });
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
