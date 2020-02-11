using Amazon.SageMakerRuntime;
using Amazon.SageMakerRuntime.Model;
using CsvHelper;
using CsvHelper.Configuration;
using IpStack;
using IpStack.Models;
using Microsoft.AspNetCore.Mvc;
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
            IpAddressDetails details = ipStackClient.GetIpAddressDetails(chosenEvent.IPAddressField);
            return View(new QuestionableEventReviewViewModel
            {
                ReviewEvent = chosenEvent,
                SupplmentaryInformation = details
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
            Trigger linkedTrigger = chosenEvent.LinkedAlertTrigger;
            if (linkedTrigger.IgnoredEvents == null)
                linkedTrigger.IgnoredEvents = new string[] {chosenEvent.UserField, chosenEvent.IPAddressField};
            else
            {
                string[] newIgnoredEvents = new string[linkedTrigger.IgnoredEvents.Count() + 2];
                Array.Copy(linkedTrigger.IgnoredEvents, 0, newIgnoredEvents, 0, linkedTrigger.IgnoredEvents.Count());
                newIgnoredEvents[linkedTrigger.IgnoredEvents.Count()] = chosenEvent.UserField;
                newIgnoredEvents[linkedTrigger.IgnoredEvents.Count() + 1] = chosenEvent.IPAddressField;
                linkedTrigger.IgnoredEvents = newIgnoredEvents;
            }
            _context.QuestionableEvents.Update(chosenEvent);
            _context.AlertTriggers.Update(linkedTrigger);
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
        public async Task<IActionResult> Predict(string eventData, int SageMakerID)
        {
            Trigger entity = await _context.AlertTriggers.FindAsync(SageMakerID);
            if (entity == null || eventData == null)
                return StatusCode(404);
            if (eventData.Contains(entity.Condtion) && eventData.Contains("R:301"))
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