using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.ViewModels;
using System.Data.SqlClient;
using OpsSecProject.Models;
using OpsSecProject.Data;
using System.IO;
using CsvHelper.Configuration;
using System.Globalization;
using CsvHelper;
using Amazon.SageMakerRuntime.Model;
using Amazon.SageMakerRuntime;
using Newtonsoft.Json;
using System.Net;

namespace OpsSecProject.Controllers
{
    public class AnalyticsController : Controller
    {
        private readonly LogContext _context;
        private readonly IAmazonSageMakerRuntime _SageMakerClient;

        public AnalyticsController(LogContext context, IAmazonSageMakerRuntime SageMakerClient)
        {
            _context = context;
            _SageMakerClient = SageMakerClient;
        }


        private static string GetRdsConnectionString()
        {
            string hostname = Environment.GetEnvironmentVariable("RDS_HOSTNAME");
            string port = Environment.GetEnvironmentVariable("RDS_PORT");
            string username = Environment.GetEnvironmentVariable("RDS_USERNAME");
            string password = Environment.GetEnvironmentVariable("RDS_PASSWORD");

            return $"Data Source={hostname},{port};Initial Catalog=IngestedData;User ID={username};Password={password};";
        }



        public async Task<IActionResult> ApacheLogs()
        {



            var sovm = new StreamingOverrallViewModel
            {

                results = new List<ApacheWebLog>(),
                charts = new List<ApacheWebLog>(),
                count = new List<ApacheWebLog>(),
                groupBy = new List<ApacheWebLog>(),
                cardsTotalIps = new List<ApacheWebLog>(),
                cardsTotalBytes = new List<ApacheWebLog>()



            };

            using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
            {
                connection.Open();

                // Get values to populate table
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM dbo.smartinsights_apache_web_logs", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
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
                            sovm.results.Add(newItem);
                        }

                    }

                }

                // Values for card

                // Total IP Addresses

                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(DISTINCT host) as totalIp FROM dbo.smartinsights_apache_web_logs", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            ApacheWebLog newItem = new ApacheWebLog();

                            if (!dr.IsDBNull(0))
                                newItem.totalIp = Convert.ToString(dr.GetInt32(0));

                            sovm.cardsTotalIps.Add(newItem);

                        }

                    }

                }

                // Total bytes

                using (SqlCommand cmd = new SqlCommand("SELECT SUM(CONVERT (int, bytes)) AS totalBytes FROM dbo.smartinsights_apache_web_logs", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            ApacheWebLog newItem = new ApacheWebLog();

                            if (!dr.IsDBNull(0))
                                newItem.totalBytes = Convert.ToString(dr.GetInt32(0));

                            sovm.cardsTotalBytes.Add(newItem);

                        }

                    }

                }

                // Most Recent Timestamp


                // Get value for response table 
                using (SqlCommand cmd = new SqlCommand("SELECT DISTINCT response FROM dbo.smartinsights_apache_web_logs", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            ApacheWebLog newItem = new ApacheWebLog();

                            if (!dr.IsDBNull(0))
                                newItem.response = dr.GetString(0);

                            Console.WriteLine(newItem);
                            sovm.charts.Add(newItem);

                        }

                    }

                }

                //Get count for response table

                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(DISTINCT response) FROM dbo.smartinsights_apache_web_logs", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            ApacheWebLog newItem = new ApacheWebLog();

                            if (!dr.IsDBNull(0))
                                newItem.response = Convert.ToString(dr.GetInt32(0));

                            Console.WriteLine(newItem);
                            sovm.count.Add(newItem);

                        }

                    }

                }



                //Get count group by for response table

                using (SqlCommand cmd = new SqlCommand("SELECT response, COUNT(*) FROM dbo.smartinsights_apache_web_logs GROUP BY response", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {

                        IList<string> xAxis = new List<string>();
                        IList<string> yAxis = new List<string>();



                        while (dr.Read())
                        {
                            ApacheWebLog newItem = new ApacheWebLog();

                            if (!dr.IsDBNull(0))
                                newItem.response = dr.GetString(0);
                            xAxis.Add(newItem.response);
                            newItem.COUNT = Convert.ToString(dr.GetInt32(1));
                            yAxis.Add(newItem.COUNT);


                            sovm.groupBy.Add(newItem);

                        }


                        string xAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(xAxis);
                        ViewBag.xAxisJ = xAxisJ;
                        string yAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(yAxis);
                        ViewBag.yAxisJ = yAxisJ;


                    }

                }


                // Line Chart

                using (SqlCommand cmd = new SqlCommand("SELECT request, COUNT(*) FROM dbo.smartinsights_apache_web_logs GROUP BY request ORDER BY request DESC", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {

                        IList<string> xAxisR = new List<string>();
                        IList<string> yAxisR = new List<string>();



                        while (dr.Read())
                        {
                            ApacheWebLog newItem = new ApacheWebLog();

                            if (!dr.IsDBNull(0))
                                newItem.request = dr.GetString(0);
                            xAxisR.Add(newItem.request);
                            newItem.COUNT = Convert.ToString(dr.GetInt32(1));
                            yAxisR.Add(newItem.COUNT);

                            sovm.groupBy.Add(newItem);

                        }


                        string xAxisRJ = Newtonsoft.Json.JsonConvert.SerializeObject(xAxisR);
                        ViewBag.xAxisRJ = xAxisRJ;
                        string yAxisRJ = Newtonsoft.Json.JsonConvert.SerializeObject(yAxisR);
                        ViewBag.yAxisRJ = yAxisRJ;


                    }

                }

            }


            //List to db

            //IList<string> xAxis = new List<string>();
            //xAxis.Add("Va1");
            //xAxis.Add("Va2");
            //xAxis.Add("Va13");
            //string xAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(xAxis);
            //ViewBag.xAxisJ = xAxisJ;

            //IList<string> yAxis = new List<string>();
            //yAxis.Add("20");
            //yAxis.Add("30");
            //yAxis.Add("10");
            //string yAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(yAxis);
            //ViewBag.yAxisJ = yAxisJ;


            return View(sovm);

        }


        // SSH Server Logs

        public async Task<IActionResult> SSHLogs()
        {

            var sovm = new StreamingOverrallViewModel
            {
                SSHresults = new List<SSHServerLogs>(),
                cardsFailedLogin = new List<SSHServerLogs>(),
                cardsTopUserFailedLogin = new List<SSHServerLogs>(),
                cardsTopPort = new List<SSHServerLogs>(),
                chartsPieLogin = new List<SSHServerLogs>(),
                chartsBarLoginAttemptsTime = new List<SSHServerLogs>(),
                chartsBarLoginAttemptsTime2 = new List<SSHServerLogs>()
            };

            // Get entire DB
            using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM dbo.smartinsights_ssh_logs", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            SSHServerLogs newItem = new SSHServerLogs();
                            if (!dr.IsDBNull(0))
                                newItem.weekday = dr.GetString(0);
                            if (!dr.IsDBNull(1))
                                newItem.month = dr.GetString(1);
                            if (!dr.IsDBNull(2))
                                newItem.day = dr.GetString(2);
                            if (!dr.IsDBNull(3))
                                newItem.year = dr.GetString(3);
                            if (!dr.IsDBNull(4))
                                newItem.time = dr.GetString(4);
                            if (!dr.IsDBNull(5))
                                newItem.host = dr.GetString(5);
                            if (!dr.IsDBNull(6))
                                newItem.process = dr.GetString(6);
                            if (!dr.IsDBNull(7))
                                newItem.identifier = dr.GetString(7);
                            if (!dr.IsDBNull(8))
                                newItem.message = dr.GetString(8);
                            sovm.SSHresults.Add(newItem);
                        }
                    }
                }


                // Get values for card

                // Number of Failed Login
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(message) as failedLogin FROM dbo.smartinsights_ssh_logs WHERE message LIKE 'Failed%'", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            SSHServerLogs newItem = new SSHServerLogs();

                            if (!dr.IsDBNull(0))
                                newItem.failedLogin = Convert.ToString(dr.GetInt32(0));

                            sovm.cardsFailedLogin.Add(newItem);

                        }

                    }

                }

                // User with most failed logins
                using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 substring(message,21,  CHARINDEX('from', message)-21) AS u, count(*) as totalNum FROM dbo.smartinsights_ssh_logs where (message like 'Failed password for%') GROUP BY substring(message,21, CHARINDEX('from', message)-21) ORDER BY totalNum DESC", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            SSHServerLogs newItem = new SSHServerLogs();

                            if (!dr.IsDBNull(0))
                                newItem.u = dr.GetString(0);
                            if (!dr.IsDBNull(1))
                                newItem.totalNum = Convert.ToString(dr.GetInt32(1));

                            sovm.cardsTopUserFailedLogin.Add(newItem);

                        }

                    }

                }

                // Port with most failed login
                using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 substring(message,CHARINDEX('port', message),  CHARINDEX('ssh2', message)-CHARINDEX('port', message)) as topPort, count(*) as totalNumPort FROM dbo.smartinsights_ssh_logs where (message like 'Failed password for%') GROUP BY substring(message,CHARINDEX('port', message), CHARINDEX('ssh2', message)-CHARINDEX('port', message)) ORDER BY totalNumPort Desc", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            SSHServerLogs newItem = new SSHServerLogs();

                            if (!dr.IsDBNull(0))
                                newItem.topPort = dr.GetString(0);
                            if (!dr.IsDBNull(1))
                                newItem.totalNumPort = Convert.ToString(dr.GetInt32(1));

                            sovm.cardsTopPort.Add(newItem);

                        }

                    }

                }






                // Pie chart for number of login attempts
                using (SqlCommand cmd = new SqlCommand("SELECT substring(message,1,  CHARINDEX('password', message)-1) AS loginAttempt, count(*) AS totalNumLoginAttempt FROM dbo.smartinsights_ssh_logs where (message like 'Failed password for%') OR (message LIKE 'Accepted%') GROUP BY substring(message,1, CHARINDEX('password', message)-1)", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {

                        IList<string> xAxis = new List<string>();
                        IList<string> yAxis = new List<string>();



                        while (dr.Read())
                        {
                            SSHServerLogs newItem = new SSHServerLogs();

                            if (!dr.IsDBNull(0))
                                newItem.loginAttempt = dr.GetString(0);
                            xAxis.Add(newItem.loginAttempt);
                            newItem.totalNumLoginAttempt = Convert.ToString(dr.GetInt32(1));
                            yAxis.Add(newItem.totalNumLoginAttempt);

                            sovm.chartsPieLogin.Add(newItem);

                        }


                        string xAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(xAxis);
                        ViewBag.xAxisJ = xAxisJ;
                        string yAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(yAxis);
                        ViewBag.yAxisJ = yAxisJ;


                    }

                }


                // Bar chart for month over login attempts (Failed attempts)
                using (SqlCommand cmd = new SqlCommand("SELECT substring(message,1,  CHARINDEX('password', message)-1) AS loginAttempt, count(*) AS totalNumLoginAttempt, month FROM dbo.smartinsights_ssh_logs where (message like 'Failed password for%') GROUP BY substring(message,1, CHARINDEX('password', message)-1), month", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {

                        IList<string> xAxisMonth = new List<string>();
                        IList<string> yAxisNumLoginAttempts = new List<string>();




                        while (dr.Read())
                        {
                            SSHServerLogs newItem = new SSHServerLogs();

                            if (!dr.IsDBNull(0))
                                newItem.totalNumLoginAttempt = Convert.ToString(dr.GetInt32(1));
                            yAxisNumLoginAttempts.Add(newItem.totalNumLoginAttempt);
                            newItem.month = dr.GetString(2);
                            xAxisMonth.Add(newItem.month);


                            sovm.chartsBarLoginAttemptsTime.Add(newItem);

                        }


                        string xAxisM = Newtonsoft.Json.JsonConvert.SerializeObject(xAxisMonth);
                        ViewBag.xAxisM = xAxisM;
                        string yAxisM = Newtonsoft.Json.JsonConvert.SerializeObject(yAxisNumLoginAttempts);
                        ViewBag.yAxisM = yAxisM;

                    }

                }

                // Bar chart for month over login attempts (Accepted attempts)

                using (SqlCommand cmd = new SqlCommand("SELECT substring(message,1,  CHARINDEX('password', message)-1) AS loginAttempt, count(*) AS totalNumLoginAttempt, month FROM dbo.smartinsights_ssh_logs where (message like 'Accepted password for%') GROUP BY substring(message,1, CHARINDEX('password', message)-1), month", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {


                        IList<string> yAxisNumLoginAttempts2 = new List<string>();

                        while (dr.Read())
                        {
                            SSHServerLogs newItem = new SSHServerLogs();

                            if (!dr.IsDBNull(0))

                                newItem.totalNumLoginAttempt = Convert.ToString(dr.GetInt32(1));
                            yAxisNumLoginAttempts2.Add(newItem.totalNumLoginAttempt);



                            sovm.chartsBarLoginAttemptsTime2.Add(newItem);

                        }

                        string yAxisM2 = Newtonsoft.Json.JsonConvert.SerializeObject(yAxisNumLoginAttempts2);
                        ViewBag.yAxisM2 = yAxisM2;



                    }

                }


            }




            return View(sovm);

        }




        // Squid Proxy Logs

        public async Task<IActionResult> SquidLogs()
        {
            var sovm = new StreamingOverrallViewModel
            {
                squidResults = new List<SquidProxyLog>()
            };

            using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM dbo.smartinsights_squid_proxy_logs", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            SquidProxyLog newItem = new SquidProxyLog();
                            if (!dr.IsDBNull(0))
                                newItem.timestamp = dr.GetString(0);
                            if (!dr.IsDBNull(1))
                                newItem.destination_ip_address = dr.GetString(1);
                            if (!dr.IsDBNull(2))
                                newItem.action = dr.GetString(2);
                            if (!dr.IsDBNull(3))
                                newItem.http_status_Code = dr.GetString(3);
                            if (!dr.IsDBNull(4))
                                newItem.bytes_in = dr.GetString(4);
                            if (!dr.IsDBNull(5))
                                newItem.http_method = dr.GetString(5);
                            if (!dr.IsDBNull(6))
                                newItem.requested_url = dr.GetString(6);
                            if (!dr.IsDBNull(7))
                                newItem.user = dr.GetString(7);
                            if (!dr.IsDBNull(8))
                                newItem.requested_url_domain = dr.GetString(8);
                            if (!dr.IsDBNull(9))
                                newItem.content_type = dr.GetString(9);
                            sovm.squidResults.Add(newItem);
                        }
                    }

                }


                // Pie Chart
                using (SqlCommand cmd = new SqlCommand("SELECT TOP 5 requested_url_domain, COUNT(*) FROM dbo.smartinsights_squid_proxy_logs GROUP BY requested_url_domain ORDER BY COUNT(*) DESC", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {

                        IList<string> xAxis = new List<string>();
                        IList<string> yAxis = new List<string>();



                        while (dr.Read())
                        {
                            SquidProxyLog newItem = new SquidProxyLog();

                            if (!dr.IsDBNull(0))
                                newItem.requested_url_domain = dr.GetString(0);
                            xAxis.Add(newItem.requested_url_domain);
                            newItem.COUNT = Convert.ToString(dr.GetInt32(1));
                            yAxis.Add(newItem.COUNT);

                        }


                        string xAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(xAxis);
                        ViewBag.xAxisJ = xAxisJ;
                        string yAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(yAxis);
                        ViewBag.yAxisJ = yAxisJ;


                    }

                }

                //Line Chart

                using (SqlCommand cmd = new SqlCommand("SELECT action, COUNT(*) FROM dbo.smartinsights_squid_proxy_logs GROUP BY action;", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {

                        IList<string> xAxisUser = new List<string>();
                        IList<string> yAxisAmt = new List<string>();



                        while (dr.Read())
                        {
                            SquidProxyLog newItem = new SquidProxyLog();

                            if (!dr.IsDBNull(0))
                                newItem.user = dr.GetString(0);
                            xAxisUser.Add(newItem.user);
                            newItem.COUNT = Convert.ToString(dr.GetInt32(1));
                            yAxisAmt.Add(newItem.COUNT);

                        }


                        string xAxisUserJ = Newtonsoft.Json.JsonConvert.SerializeObject(xAxisUser);
                        ViewBag.xAxisUserJ = xAxisUserJ;
                        string yAxisAmtJ = Newtonsoft.Json.JsonConvert.SerializeObject(yAxisAmt);
                        ViewBag.yAxisAmtJ = yAxisAmtJ;


                    }

                }





            }

            return View(sovm);

        }


        // Windows Security Logs

        public async Task<IActionResult> WindowsLogs()
        {
            var sovm = new StreamingOverrallViewModel
            {
                windowslogs = new List<WindowsSecurityLog>(),
                cardsFailedAccount = new List<WindowsSecurityLog>()

            };

            using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM dbo.smartinsights_windows_security_logs", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            WindowsSecurityLog newItem = new WindowsSecurityLog();
                            if (!dr.IsDBNull(0))
                                newItem.eventid = dr.GetInt32(0);
                            newItem.eventid.ToString();
                            //if (!dr.IsDBNull(1))
                            //    newItem.description = dr.GetString(1);
                            if (!dr.IsDBNull(2))
                                newItem.leveldisplayname = dr.GetString(2);
                            if (!dr.IsDBNull(3))
                                newItem.logname = dr.GetString(3);
                            if (!dr.IsDBNull(4))
                                newItem.machinename = dr.GetString(4);
                            if (!dr.IsDBNull(5))
                                newItem.providername = dr.GetString(5);
                            if (!dr.IsDBNull(6))
                                newItem.timecreated = dr.GetString(6);
                            if (!dr.IsDBNull(7))
                                newItem.index = dr.GetInt32(7);
                            newItem.index.ToString();
                            if (!dr.IsDBNull(8))
                                newItem.username = dr.GetString(8);
                            if (!dr.IsDBNull(9))
                                newItem.keywords = dr.GetString(9);
                            //if (!dr.IsDBNull(10))
                            //    newItem.eventdata = dr.GetString(10);
                            sovm.windowslogs.Add(newItem);
                        }
                    }



                }

                // Card

                // Failed Account Logins
                // User with most failed logins
                using (SqlCommand cmd = new SqlCommand("SELECT substring(description,1,  CHARINDEX('to', description)-1) AS failedAccount, count(*) AS totalNumFailedAccount FROM dbo.smartinsights_windows_security_logs where (description like 'An account failed to log on%') GROUP BY substring(description,1, CHARINDEX('to', description)-1)", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            WindowsSecurityLog newItem = new WindowsSecurityLog();

                            if (!dr.IsDBNull(1))
                                newItem.totalNumFailedAccount = Convert.ToString(dr.GetInt32(1));

                            sovm.cardsFailedAccount.Add(newItem);

                        }

                    }

                }





            }

            return View(sovm);

        }






        // DASHBOARD



        public async Task<IActionResult> Dashboard(int InputID)
        {
            LogInput retrieved = await _context.LogInputs.FindAsync(InputID);
            string dbTableName = "dbo." + retrieved.LinkedS3Bucket.Name.Replace("-", "_");
            var sovm = new StreamingOverrallViewModel
            {

                results = new List<ApacheWebLog>(),
                charts = new List<ApacheWebLog>(),
                count = new List<ApacheWebLog>(),
                groupBy = new List<ApacheWebLog>(),
                cardsTotalIps = new List<ApacheWebLog>(),
                cardsTotalBytes = new List<ApacheWebLog>(),
                cardsTopIp = new List<ApacheWebLog>(),

                SSHresults = new List<SSHServerLogs>(),
                cardsFailedLogin = new List<SSHServerLogs>(),
                cardsTopUserFailedLogin = new List<SSHServerLogs>(),
                cardsTopPort = new List<SSHServerLogs>(),
                chartsPieLogin = new List<SSHServerLogs>(),
                chartsBarLoginAttemptsTime = new List<SSHServerLogs>(),
                chartsBarLoginAttemptsTime2 = new List<SSHServerLogs>(),

                squidResults = new List<SquidProxyLog>(),
                cardsTopContentType = new List<SquidProxyLog>(),
                cardsTopDestIp = new List<SquidProxyLog>(),
                cardsTopReqUser = new List<SquidProxyLog>(),

                windowslogs = new List<WindowsSecurityLog>(),
                cardsFailedAccount = new List<WindowsSecurityLog>(),
                input = retrieved


            };

            using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
            {
                connection.Open();
                if (retrieved.LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                {
                    // Get values to populate table
                    using (SqlCommand cmd = new SqlCommand("SELECT * FROM " + dbTableName + ";", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
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
                                sovm.results.Add(newItem);
                            }

                        }

                    }

                    // Values for card

                    // Total IP Addresses

                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(DISTINCT host) as totalIp FROM " + dbTableName + ";", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                ApacheWebLog newItem = new ApacheWebLog();

                                if (!dr.IsDBNull(0))
                                    newItem.totalIp = Convert.ToString(dr.GetInt32(0));

                                sovm.cardsTotalIps.Add(newItem);

                            }

                        }

                    }

                    // Total bytes

                    using (SqlCommand cmd = new SqlCommand("SELECT SUM(CONVERT (int, bytes)) AS totalBytes FROM  " + dbTableName + ";", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                ApacheWebLog newItem = new ApacheWebLog();
                                if (!dr.IsDBNull(0))
                                    newItem.totalBytes = Convert.ToString(dr.GetInt32(0));

                                sovm.cardsTotalBytes.Add(newItem);

                            }

                        }

                    }

                    // IP address with most request


                    // Get value for response table 
                    using (SqlCommand cmd = new SqlCommand("select Top 1 host, count(*) as COUNT from " + dbTableName + " group by host Order by COUNT desc", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                ApacheWebLog newItem = new ApacheWebLog();

                                if (!dr.IsDBNull(0))
                                    newItem.host = dr.GetString(0);

                                sovm.cardsTopIp.Add(newItem);

                            }

                        }

                    }


                    //Get count for response table

                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(DISTINCT response) FROM  " + dbTableName + ";", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                ApacheWebLog newItem = new ApacheWebLog();

                                if (!dr.IsDBNull(0))
                                    newItem.response = Convert.ToString(dr.GetInt32(0));

                                Console.WriteLine(newItem);
                                sovm.count.Add(newItem);

                            }

                        }

                    }



                    //Get count group by for response table

                    using (SqlCommand cmd = new SqlCommand("SELECT response, COUNT(*) FROM  " + dbTableName + " GROUP BY response;", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {

                            IList<string> xAxis = new List<string>();
                            IList<string> yAxis = new List<string>();



                            while (dr.Read())
                            {
                                ApacheWebLog newItem = new ApacheWebLog();

                                if (!dr.IsDBNull(0))
                                    newItem.response = dr.GetString(0);
                                xAxis.Add(newItem.response);
                                newItem.COUNT = Convert.ToString(dr.GetInt32(1));
                                yAxis.Add(newItem.COUNT);


                                sovm.groupBy.Add(newItem);

                            }


                            string xAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(xAxis);
                            ViewBag.xAxisJ = xAxisJ;
                            string yAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(yAxis);
                            ViewBag.yAxisJ = yAxisJ;


                        }

                    }


                    // Line Chart

                    using (SqlCommand cmd = new SqlCommand("SELECT request, COUNT(*) FROM  " + dbTableName + " GROUP BY request ORDER BY request DESC;", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {

                            IList<string> xAxisR = new List<string>();
                            IList<string> yAxisR = new List<string>();



                            while (dr.Read())
                            {
                                ApacheWebLog newItem = new ApacheWebLog();

                                if (!dr.IsDBNull(0))
                                    newItem.request = dr.GetString(0);
                                xAxisR.Add(newItem.request);
                                newItem.COUNT = Convert.ToString(dr.GetInt32(1));
                                yAxisR.Add(newItem.COUNT);

                                sovm.groupBy.Add(newItem);

                            }


                            string xAxisRJ = Newtonsoft.Json.JsonConvert.SerializeObject(xAxisR);
                            ViewBag.xAxisRJ = xAxisRJ;
                            string yAxisRJ = Newtonsoft.Json.JsonConvert.SerializeObject(yAxisR);
                            ViewBag.yAxisRJ = yAxisRJ;


                        }

                    }
                }
                else if (retrieved.LogInputCategory.Equals(LogInputCategory.SSH))
                {
                    // SSH BELOW 


                    // Get values for card

                    // Number of Failed Login
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(message) as failedLogin FROM dbo.smartinsights_ssh_logs WHERE message LIKE 'Failed%'", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                SSHServerLogs newItem = new SSHServerLogs();

                                if (!dr.IsDBNull(0))
                                    newItem.failedLogin = Convert.ToString(dr.GetInt32(0));

                                sovm.cardsFailedLogin.Add(newItem);

                            }

                        }

                    }

                    // User with most failed logins
                    using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 substring(message,21,  CHARINDEX('from', message)-21) AS u, count(*) as totalNum FROM dbo.smartinsights_ssh_logs where (message like 'Failed password for%') GROUP BY substring(message,21, CHARINDEX('from', message)-21) ORDER BY totalNum DESC", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                SSHServerLogs newItem = new SSHServerLogs();

                                if (!dr.IsDBNull(0))
                                    newItem.u = dr.GetString(0);
                                if (!dr.IsDBNull(1))
                                    newItem.totalNum = Convert.ToString(dr.GetInt32(1));

                                sovm.cardsTopUserFailedLogin.Add(newItem);

                            }

                        }

                    }

                    // Port with most failed login
                    using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 substring(message,CHARINDEX('port', message),  CHARINDEX('ssh2', message)-CHARINDEX('port', message)) as topPort, count(*) as totalNumPort FROM dbo.smartinsights_ssh_logs where (message like 'Failed password for%') GROUP BY substring(message,CHARINDEX('port', message), CHARINDEX('ssh2', message)-CHARINDEX('port', message)) ORDER BY totalNumPort Desc", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                SSHServerLogs newItem = new SSHServerLogs();

                                if (!dr.IsDBNull(0))
                                    newItem.topPort = dr.GetString(0);
                                if (!dr.IsDBNull(1))
                                    newItem.totalNumPort = Convert.ToString(dr.GetInt32(1));

                                sovm.cardsTopPort.Add(newItem);

                            }

                        }

                    }



                    // Pie chart for number of login attempts
                    using (SqlCommand cmd = new SqlCommand("SELECT substring(message,1,  CHARINDEX('password', message)-1) AS loginAttempt, count(*) AS totalNumLoginAttempt FROM dbo.smartinsights_ssh_logs where (message like 'Failed password for%') OR (message LIKE 'Accepted%') GROUP BY substring(message,1, CHARINDEX('password', message)-1)", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {

                            IList<string> xAxis = new List<string>();
                            IList<string> yAxis = new List<string>();



                            while (dr.Read())
                            {
                                SSHServerLogs newItem = new SSHServerLogs();

                                if (!dr.IsDBNull(0))
                                    newItem.loginAttempt = dr.GetString(0);
                                xAxis.Add(newItem.loginAttempt);
                                newItem.totalNumLoginAttempt = Convert.ToString(dr.GetInt32(1));
                                yAxis.Add(newItem.totalNumLoginAttempt);

                                sovm.chartsPieLogin.Add(newItem);

                            }


                            string xAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(xAxis);
                            ViewBag.xAxisJ = xAxisJ;
                            string yAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(yAxis);
                            ViewBag.yAxisJ = yAxisJ;


                        }

                    }


                    // Bar chart for month over login attempts (Failed attempts)
                    using (SqlCommand cmd = new SqlCommand("SELECT substring(message,1,  CHARINDEX('password', message)-1) AS loginAttempt, count(*) AS totalNumLoginAttempt, month FROM dbo.smartinsights_ssh_logs where (message like 'Failed password for%') GROUP BY substring(message,1, CHARINDEX('password', message)-1), month", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {

                            IList<string> xAxisMonth = new List<string>();
                            IList<string> yAxisNumLoginAttempts = new List<string>();




                            while (dr.Read())
                            {
                                SSHServerLogs newItem = new SSHServerLogs();

                                if (!dr.IsDBNull(0))
                                    newItem.totalNumLoginAttempt = Convert.ToString(dr.GetInt32(1));
                                yAxisNumLoginAttempts.Add(newItem.totalNumLoginAttempt);
                                newItem.month = dr.GetString(2);
                                xAxisMonth.Add(newItem.month);


                                sovm.chartsBarLoginAttemptsTime.Add(newItem);

                            }


                            string xAxisM = Newtonsoft.Json.JsonConvert.SerializeObject(xAxisMonth);
                            ViewBag.xAxisM = xAxisM;
                            string yAxisM = Newtonsoft.Json.JsonConvert.SerializeObject(yAxisNumLoginAttempts);
                            ViewBag.yAxisM = yAxisM;

                        }

                    }

                    // Bar chart for month over login attempts (Accepted attempts)

                    using (SqlCommand cmd = new SqlCommand("SELECT substring(message,1,  CHARINDEX('password', message)-1) AS loginAttempt, count(*) AS totalNumLoginAttempt, month FROM dbo.smartinsights_ssh_logs where (message like 'Accepted password for%') GROUP BY substring(message,1, CHARINDEX('password', message)-1), month", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            IList<string> yAxisNumLoginAttempts2 = new List<string>();

                            while (dr.Read())
                            {
                                SSHServerLogs newItem = new SSHServerLogs();

                                if (!dr.IsDBNull(0))

                                    newItem.totalNumLoginAttempt = Convert.ToString(dr.GetInt32(1));
                                yAxisNumLoginAttempts2.Add(newItem.totalNumLoginAttempt);



                                sovm.chartsBarLoginAttemptsTime2.Add(newItem);

                            }

                            string yAxisM2 = Newtonsoft.Json.JsonConvert.SerializeObject(yAxisNumLoginAttempts2);
                            ViewBag.yAxisM2 = yAxisM2;



                        }

                    }

                }
                else if (retrieved.LogInputCategory.Equals(LogInputCategory.SquidProxy))
                {

                    //cards
                    //Top content type

                    using (SqlCommand cmd = new SqlCommand("select Top 1 content_type, count(*) as COUNT from dbo.smartinsights_squid_proxy_logs group by content_type Order by COUNT Desc", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                SquidProxyLog newItem = new SquidProxyLog();

                                if (!dr.IsDBNull(0))
                                    newItem.content_type = dr.GetString(0);

                                sovm.cardsTopContentType.Add(newItem);

                            }

                        }

                    }


                    // Top dest ip addr

                    using (SqlCommand cmd = new SqlCommand("select Top 1 destination_ip_address, count(*) as COUNT from dbo.smartinsights_squid_proxy_logs group by destination_ip_address Order by COUNT Desc", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                SquidProxyLog newItem = new SquidProxyLog();

                                if (!dr.IsDBNull(0))
                                    newItem.destination_ip_address = dr.GetString(0);

                                sovm.cardsTopDestIp.Add(newItem);

                            }

                        }

                    }

                    // Top req user

                    using (SqlCommand cmd = new SqlCommand("select Top 1 [user], count(*) as COUNT from dbo.smartinsights_squid_proxy_logs group by [user] Order by COUNT Desc", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                SquidProxyLog newItem = new SquidProxyLog();

                                if (!dr.IsDBNull(0))
                                    newItem.user = dr.GetString(0);

                                sovm.cardsTopReqUser.Add(newItem);

                            }

                        }

                    }

                    // Pie Chart
                    using (SqlCommand cmd = new SqlCommand("SELECT TOP 5 requested_url_domain, COUNT(*) FROM dbo.smartinsights_squid_proxy_logs GROUP BY requested_url_domain ORDER BY COUNT(*) DESC", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {

                            IList<string> xAxis = new List<string>();
                            IList<string> yAxis = new List<string>();



                            while (dr.Read())
                            {
                                SquidProxyLog newItem = new SquidProxyLog();

                                if (!dr.IsDBNull(0))
                                    newItem.requested_url_domain = dr.GetString(0);
                                xAxis.Add(newItem.requested_url_domain);
                                newItem.COUNT = Convert.ToString(dr.GetInt32(1));
                                yAxis.Add(newItem.COUNT);

                            }


                            string xAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(xAxis);
                            ViewBag.xAxisJ = xAxisJ;
                            string yAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(yAxis);
                            ViewBag.yAxisJ = yAxisJ;


                        }

                    }

                    //Line Chart

                    using (SqlCommand cmd = new SqlCommand("SELECT action, COUNT(*) FROM dbo.smartinsights_squid_proxy_logs GROUP BY action;", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {

                            IList<string> xAxisUser = new List<string>();
                            IList<string> yAxisAmt = new List<string>();



                            while (dr.Read())
                            {
                                SquidProxyLog newItem = new SquidProxyLog();

                                if (!dr.IsDBNull(0))
                                    newItem.user = dr.GetString(0);
                                xAxisUser.Add(newItem.user);
                                newItem.COUNT = Convert.ToString(dr.GetInt32(1));
                                yAxisAmt.Add(newItem.COUNT);

                            }


                            string xAxisUserJ = Newtonsoft.Json.JsonConvert.SerializeObject(xAxisUser);
                            ViewBag.xAxisUserJ = xAxisUserJ;
                            string yAxisAmtJ = Newtonsoft.Json.JsonConvert.SerializeObject(yAxisAmt);
                            ViewBag.yAxisAmtJ = yAxisAmtJ;


                        }

                    }

                }
                else if (retrieved.LogInputCategory.Equals(LogInputCategory.WindowsEventLogs))
                {

                    // Card
                    // Failed Account Logins
                    using (SqlCommand cmd = new SqlCommand("SELECT substring(description,1,  CHARINDEX('to', description)-1) AS failedAccount, count(*) AS totalNumFailedAccount FROM dbo.smartinsights_windows_security_logs where (description like 'An account failed to log on%') GROUP BY substring(description,1, CHARINDEX('to', description)-1)", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                WindowsSecurityLog newItem = new WindowsSecurityLog();

                                if (!dr.IsDBNull(1))
                                    newItem.totalNumFailedAccount = Convert.ToString(dr.GetInt32(1));

                                sovm.cardsFailedAccount.Add(newItem);

                            }

                        }

                    }


                    // pie chart eventID

                    using (SqlCommand cmd = new SqlCommand("select Top 5 eventid, count(*) as n from dbo.smartinsights_windows_security_logs group by eventid order by n desc", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {

                            IList<string> xAxis = new List<string>();
                            IList<string> yAxis = new List<string>();



                            while (dr.Read())
                            {
                                WindowsSecurityLog newItem = new WindowsSecurityLog();

                                if (!dr.IsDBNull(0))
                                    newItem.eventid = dr.GetInt32(0);
                                xAxis.Add("EventID: " + newItem.eventid.ToString());
                                newItem.n = Convert.ToString(dr.GetInt32(1));
                                yAxis.Add(newItem.n);

                            }


                            string xAxisEventId = Newtonsoft.Json.JsonConvert.SerializeObject(xAxis);
                            ViewBag.xAxisEventId = xAxisEventId;
                            string yAxisEventId = Newtonsoft.Json.JsonConvert.SerializeObject(yAxis);
                            ViewBag.yAxisEventId = yAxisEventId;


                        }

                    }

                    // Bar Chart win act

                    using (SqlCommand cmd = new SqlCommand("SELECT substring(description,1,  CHARINDEX('.', description)-1) AS winAct, count(*) AS n FROM dbo.smartinsights_windows_security_logs where (description like 'An account failed to log on%') or (description like 'Credential Manager credentials were read%') or (description like 'Special privileges assigned to new logon%') or (description like 'A security-enabled local group membership was enumerated%') or  (description like 'A user%') GROUP BY substring(description,1, CHARINDEX('.', description)-1)", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {

                            IList<string> xAxis = new List<string>();
                            IList<string> yAxis = new List<string>();



                            while (dr.Read())
                            {
                                WindowsSecurityLog newItem = new WindowsSecurityLog();

                                if (!dr.IsDBNull(0))
                                    newItem.winAct = dr.GetString(0);
                                xAxis.Add(newItem.winAct);
                                newItem.n = Convert.ToString(dr.GetInt32(1));
                                yAxis.Add(newItem.n);

                            }


                            string xAxisWinAct = Newtonsoft.Json.JsonConvert.SerializeObject(xAxis);
                            ViewBag.xAxisWinAct = xAxisWinAct;
                            string yAxisWinAct = Newtonsoft.Json.JsonConvert.SerializeObject(yAxis);
                            ViewBag.yAxisWinAct = yAxisWinAct;


                        }

                    }









                }

            }


            return View(sovm);

        }

        public async Task<IActionResult> Streaming(int InputID)
        {
            if (InputID <= 0)
                return StatusCode(404);
            LogInput retrieved = await _context.LogInputs.FindAsync(InputID);
            if (retrieved == null)
                return StatusCode(404);
            string dbTableName = "dbo." + retrieved.LinkedS3Bucket.Name.Replace("-", "_");
            List<ApacheWebLog> webLogs = new List<ApacheWebLog>();
            List<SSHServerLogs> sshLogs = new List<SSHServerLogs>();
            List<SquidProxyLog> squidLogs = new List<SquidProxyLog>();
            List<WindowsSecurityLog> windowslogs = new List<WindowsSecurityLog>();
            using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand(@"SELECT * FROM " + dbTableName + ";", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (retrieved.LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                            {
                                ApacheWebLog newItem = new ApacheWebLog();
                                if (retrieved.Name.Contains("IPInsights"))
                                {
                                    if (!dr.IsDBNull(0))
                                        newItem.host = dr.GetString(0);
                                    if (!dr.IsDBNull(1))
                                        newItem.ident = dr.GetString(1);
                                    if (!dr.IsDBNull(2))
                                        newItem.authuser = dr.GetString(2);
                                    if (!dr.IsDBNull(3))
                                        newItem.datetime = dr.GetString(3);
                                    if (!dr.IsDBNull(4) && !dr.IsDBNull(5) && !dr.IsDBNull(6))
                                        newItem.request = dr.GetString(4) + " " + dr.GetString(5) + " " + dr.GetString(6);
                                    if (!dr.IsDBNull(7))
                                        newItem.response = dr.GetString(7);
                                    if (!dr.IsDBNull(8))
                                        newItem.bytes = Convert.ToInt32(dr.GetString(8));
                                    if (!dr.IsDBNull(9))
                                        newItem.referer = dr.GetString(9);
                                    if (!dr.IsDBNull(10))
                                        newItem.agent = dr.GetString(10);
                                    webLogs.Add(newItem);
                                }
                                else
                                {
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
                                    if (dr.FieldCount == 9)
                                    {
                                        if (!dr.IsDBNull(7))
                                            newItem.referer = dr.GetString(7);
                                        if (!dr.IsDBNull(8))
                                            newItem.agent = dr.GetString(8);
                                    }
                                    webLogs.Add(newItem);
                                }
                            }
                            else if (retrieved.LogInputCategory.Equals(LogInputCategory.SSH))
                            {
                                SSHServerLogs newItem = new SSHServerLogs();
                                if (!dr.IsDBNull(0))
                                    newItem.weekday = dr.GetString(0);
                                if (!dr.IsDBNull(1))
                                    newItem.month = dr.GetString(1);
                                if (!dr.IsDBNull(2))
                                    newItem.day = dr.GetString(2);
                                if (!dr.IsDBNull(3))
                                    newItem.year = dr.GetString(3);
                                if (!dr.IsDBNull(4))
                                    newItem.time = dr.GetString(4);
                                if (!dr.IsDBNull(5))
                                    newItem.host = dr.GetString(5);
                                if (!dr.IsDBNull(6))
                                    newItem.process = dr.GetString(6);
                                if (!dr.IsDBNull(7))
                                    newItem.identifier = dr.GetString(7);
                                if (!dr.IsDBNull(8))
                                    newItem.message = dr.GetString(8);
                                sshLogs.Add(newItem);
                            }
                            else if (retrieved.LogInputCategory.Equals(LogInputCategory.SquidProxy))
                            {
                                SquidProxyLog newItem = new SquidProxyLog();
                                if (!dr.IsDBNull(0))
                                    newItem.timestamp = dr.GetString(0);
                                if (!dr.IsDBNull(1))
                                    newItem.destination_ip_address = dr.GetString(1);
                                if (!dr.IsDBNull(2))
                                    newItem.action = dr.GetString(2);
                                if (!dr.IsDBNull(3))
                                    newItem.http_status_Code = dr.GetString(3);
                                if (!dr.IsDBNull(4))
                                    newItem.bytes_in = dr.GetString(4);
                                if (!dr.IsDBNull(5))
                                    newItem.http_method = dr.GetString(5);
                                if (!dr.IsDBNull(6))
                                    newItem.requested_url = dr.GetString(6);
                                if (!dr.IsDBNull(7))
                                    newItem.user = dr.GetString(7);
                                if (!dr.IsDBNull(8))
                                    newItem.requested_url_domain = dr.GetString(8);
                                if (!dr.IsDBNull(9))
                                    newItem.content_type = dr.GetString(9);
                                squidLogs.Add(newItem);
                            }
                            else if (retrieved.LogInputCategory.Equals(LogInputCategory.WindowsEventLogs))
                            {
                                WindowsSecurityLog newItem = new WindowsSecurityLog();
                                if (!dr.IsDBNull(0))
                                    newItem.eventid = dr.GetInt32(0);
                                newItem.eventid.ToString();
                                //if (!dr.IsDBNull(1))
                                //    newItem.description = dr.GetString(1);
                                if (!dr.IsDBNull(2))
                                    newItem.leveldisplayname = dr.GetString(2);
                                if (!dr.IsDBNull(3))
                                    newItem.logname = dr.GetString(3);
                                if (!dr.IsDBNull(4))
                                    newItem.machinename = dr.GetString(4);
                                if (!dr.IsDBNull(5))
                                    newItem.providername = dr.GetString(5);
                                if (!dr.IsDBNull(6))
                                    newItem.timecreated = dr.GetString(6);
                                if (!dr.IsDBNull(7))
                                    newItem.index = dr.GetInt32(7);
                                newItem.index.ToString();
                                if (!dr.IsDBNull(8))
                                    newItem.username = dr.GetString(8);
                                if (!dr.IsDBNull(9))
                                    newItem.keywords = dr.GetString(9);
                                //if (!dr.IsDBNull(10))
                                //    newItem.eventdata = dr.GetString(10);
                                windowslogs.Add(newItem);
                            }

                        }
                    }
                }
            }
            Trigger ipinsights = null;
            foreach (var sagemaker in retrieved.LinkedSagemakerEntities)
            {
                if (sagemaker.AlertTriggerType.Equals(AlertTriggerType.IPInsights) && sagemaker.SagemakerStatus.Equals(SagemakerStatus.Ready))
                {
                    ipinsights = sagemaker;
                    break;
                }
            }
            return View(new StreamingOverrallViewModel
            {
                input = retrieved,
                webLogs = webLogs,
                sagemakerConsolidatedEntity = ipinsights,
                SSHresults = sshLogs,
                squidResults = squidLogs,
                windowslogs = windowslogs

            });
        }



        public async Task<IActionResult> Web()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Predict(string eventData, int SageMakerID)
        {
            Trigger entity = await _context.AlertTriggers.FindAsync(SageMakerID);
            if (entity == null || eventData == null)
                return StatusCode(404);
            if (eventData.Contains(entity.Condtion) || (eventData.Contains("R:301") && entity.LinkedLogInput.LogInputCategory.Equals(LogInputCategory.ApacheWebServer)))
            {
                string[] eventDataSplit = eventData.Split('|');
                List<GenericRecordHolder> genericRecordHolder = new List<GenericRecordHolder>
                {
                    new GenericRecordHolder
                    {
                        field1 = eventDataSplit[2],
                        field2 = eventDataSplit[0]
                    }
                };
                MemoryStream sendingMemoryStream = new MemoryStream();
                CsvConfiguration config = new CsvConfiguration(CultureInfo.CurrentCulture)
                {
                    HasHeaderRecord = false
                };
                using (var streamWriter = new StreamWriter(sendingMemoryStream))
                {
                    using (var csvWriter = new CsvWriter(streamWriter, config))
                    {
                        csvWriter.WriteRecords(genericRecordHolder);
                    }
                }
                InvokeEndpointResponse invokeEndpointResponse = await _SageMakerClient.InvokeEndpointAsync(new InvokeEndpointRequest
                {
                    Accept = "application/json",
                    ContentType = "text/csv",
                    EndpointName = entity.EndpointName,
                    Body = new MemoryStream(sendingMemoryStream.ToArray())
                });
                if (invokeEndpointResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    string json = string.Empty;
                    MemoryStream receivingMemoryStream = new MemoryStream(invokeEndpointResponse.Body.ToArray())
                    {
                        Position = 0
                    };
                    using (StreamReader reader = new StreamReader(receivingMemoryStream))
                    {
                        json = reader.ReadToEnd();
                    }
                    IPInsightsPredictions predictions = JsonConvert.DeserializeObject<IPInsightsPredictions>(json);
                    TempData["Alert"] = "Success";
                    TempData["Message"] = "The Machine Learning Model returned " + predictions.Predictions[0].Dot_product;
                    return RedirectToAction("Streaming", new { InputID = entity.LinkedLogInputID });
                }
                else
                {
                    TempData["Alert"] = "Danger";
                    TempData["Message"] = "The Machine Learning Model is facing some issues at the moment...";
                    return RedirectToAction("Streaming", new { InputID = entity.LinkedLogInputID });
                }
            }
            else
            {
                TempData["Alert"] = "Warning";
                TempData["Message"] = "This event cannot be inferred by the Machine Learning Model!";
                return RedirectToAction("Streaming", new { InputID = entity.LinkedLogInputID });
            }
        }
    }
}