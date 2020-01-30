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
using System.Data.SqlClient;
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


        public async Task<IActionResult> ApacheLogs()
        {
            List<ApacheWebLog> results = new List<ApacheWebLog>();

            using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
            {
                connection.Open();
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