using Amazon.SageMakerRuntime;
using Amazon.SageMakerRuntime.Model;
using CsvHelper;
using CsvHelper.Configuration;
using IpStack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OpsSecProject.Data;
using OpsSecProject.Models;
using OpsSecProject.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpsSecProject.Controllers
{
    public class HomeController : Controller
    {
        private readonly LogContext _context;
        private readonly IAmazonSageMakerRuntime _SageMakerClient;
        private readonly AccountContext _accountContext;
        private readonly IpStackClient ipStackClient;
        public HomeController(LogContext context, IAmazonSageMakerRuntime SageMakerClient, AccountContext accountContext)
        {
            _context = context;
            _SageMakerClient = SageMakerClient;
            _accountContext = accountContext;
            ipStackClient = new IpStackClient(Environment.GetEnvironmentVariable("IPSTACK_API_KEY"));
        }
        public IActionResult Index()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            User user = _accountContext.Users.Where(u => u.Username == currentIdentity).FirstOrDefault();
            return View(_context.QuestionableEvents.Where(q => q.ReviewUserID == user.ID && q.status == QuestionableEventStatus.PendingReview).ToList());
        }
        public IActionResult Tasks()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            User user = _accountContext.Users.Where(u => u.Username == currentIdentity).FirstOrDefault();
            return View(_context.QuestionableEvents.Where(q => q.ReviewUserID == user.ID).ToList());
        }
        public IActionResult Review(int EventID)
        {
            QuestionableEvent chosenEvent = _context.QuestionableEvents.Find(EventID);
            return View(new QuestionableEventReviewViewModel
            {
                ReviewEvent = chosenEvent,
                SupplmentaryInformation = ipStackClient.GetIpAddressDetails(chosenEvent.IPAddressField)
            });
        }
        public IActionResult Accept(int EventID)
        {
            QuestionableEvent chosenEvent = _context.QuestionableEvents.Find(EventID);
            chosenEvent.status = QuestionableEventStatus.UserAccepted;
            chosenEvent.UpdatedTimestamp = DateTime.Now;
            _context.QuestionableEvents.Update(chosenEvent);
            _context.SaveChanges();
            TempData["Alert"] = "Success";
            TempData["Message"] = "Your response has been recorded successfully";
            return RedirectToAction("View", new { EventID = chosenEvent.ID });
        }
        public IActionResult Reject(int EventID)
        {
            QuestionableEvent chosenEvent = _context.QuestionableEvents.Find(EventID);
            chosenEvent.status = QuestionableEventStatus.UserRejected;
            chosenEvent.UpdatedTimestamp = DateTime.Now;
            _context.QuestionableEvents.Update(chosenEvent);
            _context.SaveChanges();
            TempData["Alert"] = "Success";
            TempData["Message"] = "Your response has been recorded successfully";
            return RedirectToAction("View", new { EventID = chosenEvent.ID });
        }
        public IActionResult View(int EventID)
        {
            QuestionableEvent chosenEvent = _context.QuestionableEvents.Find(EventID);
            return View(new QuestionableEventReviewViewModel
            {
                ReviewEvent = chosenEvent,
                SupplmentaryInformation = ipStackClient.GetIpAddressDetails(chosenEvent.IPAddressField)
            });
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
            string dbTableName = "dbo." + retrieved.LinkedS3Bucket.Name.Replace("-", "_");
            List<ApacheWebLog> webLogs = new List<ApacheWebLog>();
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
                                        newItem.request = dr.GetString(4)+ " " + dr.GetString(5)+ " " + dr.GetString(6);
                                    if (!dr.IsDBNull(7))
                                        newItem.response = dr.GetString(7);
                                    if (!dr.IsDBNull(8))
                                        newItem.bytes = Convert.ToInt32(dr.GetString(8));
                                    if (!dr.IsDBNull(9))
                                        newItem.referer = dr.GetString(9);
                                    if (!dr.IsDBNull(10))
                                        newItem.agent = dr.GetString(10);
                                    webLogs.Add(newItem);
                                } else
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
                sagemakerConsolidatedEntity = ipinsights
            });
        }

        [HttpPost]
        public async Task<IActionResult> Predict(string eventData, int SageMakerID)
        {
            Trigger entity = await _context.AlertTriggers.FindAsync(SageMakerID);
            if (entity == null || eventData == null)
                return StatusCode(404);
            if (eventData.Contains(entity.Condtion) && eventData.Contains("R:200"))
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
                } else
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
