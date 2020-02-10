using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.Data;
using OpsSecProject.Models;

namespace OpsSecProject.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class TasksController : Controller
    {
        private readonly LogContext _logContext;
        private readonly AccountContext _accountContext;
        private IAmazonSimpleNotificationService _SNSClient;
        private IAmazonSimpleEmailService _SESClient;

        public TasksController(LogContext logContext, AccountContext accountContext, IAmazonSimpleEmailService SESClient, IAmazonSimpleNotificationService SNSClient)
        {
            _logContext = logContext;
            _accountContext = accountContext;
            _SESClient = SESClient;
            _SNSClient = SNSClient;
        }

        public IActionResult Index()
        {
            ViewBag.users = _accountContext.Users.ToList();
            return View(_logContext.QuestionableEvents.ToList());
        }
        public IActionResult ChangeState(int EventID)
        {
            QuestionableEvent qe = _logContext.QuestionableEvents.Find(EventID);
            if (qe.status.Equals(QuestionableEventStatus.UserAccepted) || qe.status.Equals(QuestionableEventStatus.AdminAccepted))
                qe.status = QuestionableEventStatus.LockedAccepted;
            else if (qe.status.Equals(QuestionableEventStatus.UserRejected) || qe.status.Equals(QuestionableEventStatus.AdminRejected))
                qe.status = QuestionableEventStatus.LockedRejected;
            else if (qe.status.Equals(QuestionableEventStatus.LockedAccepted) && qe.ReviewUserID > 0)
                qe.status = QuestionableEventStatus.UserAccepted;
            else if (qe.status.Equals(QuestionableEventStatus.LockedRejected) && qe.ReviewUserID > 0)
                qe.status = QuestionableEventStatus.UserRejected;
            else if (qe.status.Equals(QuestionableEventStatus.LockedAccepted))
                qe.status = QuestionableEventStatus.AdminAccepted;
            else if (qe.status.Equals(QuestionableEventStatus.LockedRejected))
                qe.status = QuestionableEventStatus.AdminRejected;
            _logContext.QuestionableEvents.Update(qe);
            _logContext.SaveChanges();
            return RedirectToAction("Index");
        }
        [HttpPost]
        public async Task<IActionResult> Index(int EventID, int UserID)
        {
            QuestionableEvent qe = _logContext.QuestionableEvents.Find(EventID);
            User concerned = await _accountContext.Users.FindAsync(UserID);
            Alert a = new Alert
            {
                TimeStamp = DateTime.Now,
                LinkedUserID = concerned.ID,
                LinkedObjectID = qe.LinkedAlertTrigger.LinkedLogInputID,
                AlertType = AlertType.ReviewQuestionableEvent,
                Message = "There is a event that needs your review"
            };
            if (concerned.LinkedSettings.CommmuicationOptions.Equals(CommmuicationOptions.EMAIL) && concerned.VerifiedEmailAddress)
            {
                SendEmailRequest SESrequest = new SendEmailRequest
                {
                    Source = Environment.GetEnvironmentVariable("SES_EMAIL_FROM-ADDRESS"),
                    Destination = new Destination
                    {
                        ToAddresses = new List<string>
                        {
                            concerned.EmailAddress
                        }
                    },
                    Message = new Message
                    {
                        Subject = new Content("Review of Event needed"),
                        Body = new Body
                        {
                            Text = new Content
                            {
                                Charset = "UTF-8",
                                Data = "Hi " + concerned.Name + ",\r\n\nAn event needs your review.\r\nPlease login to SmartInsights to view more details.\r\n\n\nThis is a computer-generated email, please do not reply"
                            }
                        }
                    }
                };
                SendEmailResponse response = await _SESClient.SendEmailAsync(SESrequest);
                if (response.HttpStatusCode != HttpStatusCode.OK)
                    a.ExternalNotificationType = ExternalNotificationType.EMAIL;
            }
            else if (concerned.LinkedSettings.CommmuicationOptions.Equals(CommmuicationOptions.SMS) && concerned.VerifiedPhoneNumber)
            {
                PublishRequest SNSrequest = new PublishRequest
                {
                    Message = "An event needs your review. Login to view more details.",
                    PhoneNumber = "+65" + concerned.PhoneNumber
                };
                SNSrequest.MessageAttributes["AWS.SNS.SMS.SenderID"] = new MessageAttributeValue { StringValue = "SmartIS", DataType = "String" };
                SNSrequest.MessageAttributes["AWS.SNS.SMS.SMSType"] = new MessageAttributeValue { StringValue = "Transactional", DataType = "String" };
                PublishResponse response = await _SNSClient.PublishAsync(SNSrequest);
                if (response.HttpStatusCode != HttpStatusCode.OK)
                    a.ExternalNotificationType = ExternalNotificationType.SMS;
            }
            _accountContext.Alerts.Add(a);
            qe.ReviewUserID = concerned.ID;
            _logContext.QuestionableEvents.Update(qe);
            await _accountContext.SaveChangesAsync();
            await _logContext.SaveChangesAsync();
            return View(_logContext.QuestionableEvents.ToList());
        }
    }
}