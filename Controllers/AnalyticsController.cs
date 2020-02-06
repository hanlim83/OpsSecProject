using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.ViewModels;
using System.Data.SqlClient;
using System.Data;
using OpsSecProject.Data;
using Microsoft.EntityFrameworkCore;
using OpsSecProject.Models;
using System.Configuration;
using System.Net.Sockets;
using System.IO;
using System.Text;




namespace OpsSecProject.Controllers
{
    public class AnalyticsController : Controller
    {

        
        
        private static string GetRdsConnectionString()
        {
            string hostname = Environment.GetEnvironmentVariable("RDS_HOSTNAME");
            string port = Environment.GetEnvironmentVariable("RDS_PORT");
            string username = Environment.GetEnvironmentVariable("RDS_USERNAME");
            string password = Environment.GetEnvironmentVariable("RDS_PASSWORD");

            return $"Data Source={hostname},{port};Initial Catalog=IngestedData;User ID={username};Password={password};";
        }


        // SSH Server Logs

        public async Task<IActionResult> SSHLogs()
        {

            var sovm = new StreamingOverrallViewModel
            {
                SSHresults = new List<SSHServerLogs>()
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

                // Pie chart for number of login attempts
                using (SqlCommand cmd = new SqlCommand("SELECT message, COUNT(*) FROM dbo.smartinsights_ssh_logs WHERE message LIKE ('Failed%') OR message LIKE ('Accepted%') OR message LIKE ('Server%') GROUP BY message ", connection))
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
                                newItem.message = dr.GetString(0);
                                xAxis.Add(newItem.message);
                                newItem.COUNT = Convert.ToString(dr.GetInt32(1));
                                yAxis.Add(newItem.COUNT);

                        }


                        string xAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(xAxis);
                        ViewBag.xAxisJ = xAxisJ;
                        string yAxisJ = Newtonsoft.Json.JsonConvert.SerializeObject(yAxis);
                        ViewBag.yAxisJ = yAxisJ;


                    }

                }


            }




            return View(sovm);

        }


        public async Task<IActionResult> ApacheLogs()
        {

           

            var sovm = new StreamingOverrallViewModel
            {
                
                results = new List<ApacheWebLog>(),
                charts = new List<ApacheWebLog>(),
                count = new List<ApacheWebLog>(),
                groupBy = new List<ApacheWebLog>(),

 
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

        // Squid Proxy Logs

        public async Task<IActionResult> SquidLogs()
        {
            List<SquidProxyLog> results = new List<SquidProxyLog>();

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
                            results.Add(newItem);
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

            return View(results);

        }


        // Windows Security Logs

        public async Task<IActionResult> WindowsLogs()
        {
            List<WindowsSecurityLog> results = new List<WindowsSecurityLog>();

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
                            if (!dr.IsDBNull(1))
                                newItem.description = dr.GetString(1);
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
                            if (!dr.IsDBNull(10))
                                newItem.eventdata = dr.GetString(10);
                            results.Add(newItem);
                        }
                    }



                }
            }

            return View(results);

        }



        public async Task<IActionResult> Web()
        {

            // Code for doing the whois lookup ...


            //string whoisServer = "";

            //string GetWhoisInformation(string whoisServer, string url)
            //{
            //    StringBuilder stringBuilderResult = new StringBuilder();
            //    TcpClient tcpClinetWhois = new TcpClient(whoisServer, 43);
            //    NetworkStream networkStreamWhois = tcpClinetWhois.GetStream();
            //    BufferedStream bufferedStreamWhois = new BufferedStream(networkStreamWhois);
            //    StreamWriter streamWriter = new StreamWriter(bufferedStreamWhois);

            //    streamWriter.WriteLine(url);
            //    streamWriter.Flush();

            //    StreamReader streamReaderReceive = new StreamReader(bufferedStreamWhois);

            //    while (!streamReaderReceive.EndOfStream)
            //        stringBuilderResult.AppendLine(streamReaderReceive.ReadLine());

            //    return stringBuilderResult.ToString();
            //}
            return View();

        }

        //Imported methods 

        public IActionResult Bar()
        {
            Random rnd = new Random();

            //list of department  
            var lstModel = new List<ChartsViewModel>();
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Technology",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Sales",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Marketing",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Human Resource",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Research and Development",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Acconting",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Support",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Logistics",
                Quantity = rnd.Next(10)
            });

            return View(lstModel);

        }

        public IActionResult Line()
        {
            Random rnd = new Random();

            //list of countries  
            var lstModel = new List<ChartsViewModel>();
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Brazil",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "USA",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Ier",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "gfg",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "ghA",
                Quantity = rnd.Next(10)
            });

            return View(lstModel);
        }
    }
}