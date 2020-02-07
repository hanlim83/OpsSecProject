using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Amazon.KinesisFirehose;
using Amazon.KinesisFirehose.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.SageMaker;
using Amazon.SageMaker.Model;
using Amazon.SageMakerRuntime;
using Amazon.SageMakerRuntime.Model;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpsSecProject.Data;
using OpsSecProject.Models;
using OpsSecProject.Services;
using OpsSecProject.ViewModels;
using Tag = Amazon.SageMaker.Model.Tag;

namespace OpsSecProject.Controllers
{
    [Authorize(Roles = "Administrator, Power User")]
    public class InputsController : Controller
    {
        private readonly LogContext _logContext;
        private readonly AccountContext _accountContext;
        private readonly IAmazonSageMaker _Sclient;
        private readonly IAmazonSageMakerRuntime _SRClient;
        private readonly IAmazonS3 _S3Client;
        private IBackgroundTaskQueue _queue { get; }
        private readonly ILogger _logger;
        private readonly IAmazonKinesisFirehose _FirehoseClient;

        public InputsController(LogContext logContext, IBackgroundTaskQueue queue, ILogger<InputsController> logger, AccountContext accountContext, IAmazonSageMaker Sclient, IAmazonSageMakerRuntime SRClient, IAmazonS3 S3Client, IAmazonKinesisFirehose FirehoseClient)
        {
            _logContext = logContext;
            _queue = queue;
            _logger = logger;
            _accountContext = accountContext;
            _Sclient = Sclient;
            _SRClient = SRClient;
            _S3Client = S3Client;
            _FirehoseClient = FirehoseClient;
        }

        public IActionResult Index()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            return View(new InputsOverrallViewModel
            {
                allUsers = _accountContext.Users.ToList(),
                currentUser = _accountContext.Users.Where(u => u.Username.Equals(currentIdentity)).FirstOrDefault(),
                inputs = _logContext.LogInputs.ToList()
            });
        }

        public IActionResult Json()
        {
            return View();
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create([Bind("FilePath", "Name", "Filter", "LogType", "LogInputCategory")]LogInput input)
        {
            ViewBag.LogPath = input.FilePath; //test asd
            ViewBag.LogName = input.Name; //  will this work
            ViewBag.Filter = input.Filter; //*.log asd
            ViewBag.LogType = input.LogType; //security
            ViewBag.LogInput = input.LogInputCategory; //squidproxy asd
            var lowcap = input.Name.ToLower();
            var nospace = lowcap.Replace(" ", "-");
            var BucketName2 = "smart-insight-" + nospace;
            var data = "{ \r\n   \"Sources\":[ \r\n      { \r\n         \"Id\":\"" + "WinSecurityLog" + "\",\r\n         \"SourceType\":\"WindowsEventLogSource\",\r\n         \"Directory\":\"" + input.FilePath + "\",\r\n         \"FileNameFilter\":\" " + input.Filter + "\",\r\n         \"LogName\":\" " + input.Name + " \"\r\n         \"IncludeEventData\" : true\r\n            }\r\n   ],\r\n   \"Sinks\":[ \r\n      { \r\n         \"Id\":\"WinSecurityKinesisFirehose\",\r\n         \"SinkType\":\"KinesisFirehose\",\r\n         \"AccessKey\":\""+ Environment.GetEnvironmentVariable("FIREHOSE_ACCESS_KEY_ID")+"\",\r\n         \"SecretKey\":\""+ Environment.GetEnvironmentVariable("FIREHOSE_SECRET_ACCESS_KEY") +"\",\r\n         \"Region\":\"ap-southeast-1\",\r\n         \"StreamName\":\"" + BucketName2 + "\"\r\n         \"Format\": \"json\"\r\n      }\r\n   ],\r\n   \"Pipes\":[ \r\n      { \r\n         \"Id\":\"WinSecurityPipe\",\r\n         \"SourceRef\":\"WinSecurityLog\",\r\n         \"SinkRef\":\"WinSecurityKinesisFirehose\"\r\n      }\r\n   ],\r\n   \"SelfUpdate\":0\r\n}"
;
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(data);
            var output = new FileContentResult(bytes, "application/octet-stream");
            output.FileDownloadName = "download.json";
            TempData["qwerty"] = data;
            PutBucketResponse putBucketResponse1 = await _S3Client.PutBucketAsync(new PutBucketRequest
             {

                 BucketName = "smart-insight-" + nospace,
                 UseClientRegion = true,
                 CannedACL = S3CannedACL.Private
             });
             PutBucketTaggingResponse putBucketTaggingResponse1 = await _S3Client.PutBucketTaggingAsync(new PutBucketTaggingRequest
             {
                 BucketName = "smart-insight-" + nospace,
                 TagSet = new List<Amazon.S3.Model.Tag>
                 {
                     new Amazon.S3.Model.Tag
                     {
                         Key="Project",
                         Value = "OSPJ"
                     }
                 }
             });
             PutPublicAccessBlockResponse putPublicAccessBlockResponse1 = await _S3Client.PutPublicAccessBlockAsync(new PutPublicAccessBlockRequest
             {
                 BucketName = "smart-insight-" + nospace,
                 PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration
                 {
                     BlockPublicAcls = true,
                     BlockPublicPolicy = true,
                     IgnorePublicAcls = true,
                     RestrictPublicBuckets = true
                 }
             });

            CreateDeliveryStreamResponse createDeliveryStreamResponse = await _FirehoseClient.CreateDeliveryStreamAsync(new CreateDeliveryStreamRequest
            {
                DeliveryStreamName = "smart-insight-" + nospace,
                DeliveryStreamType = DeliveryStreamType.DirectPut,
                ExtendedS3DestinationConfiguration = new ExtendedS3DestinationConfiguration
                {
                    BucketARN = "arn:aws:s3:::" + BucketName2,
                    BufferingHints = new BufferingHints
                    {
                        IntervalInSeconds = 60,
                        SizeInMBs = 5
                    },
                    RoleARN = Environment.GetEnvironmentVariable("FIREHOSE_EXECUTION_ROLE")
                },
                Tags = new List<Amazon.KinesisFirehose.Model.Tag>
                {
                    new Amazon.KinesisFirehose.Model.Tag
                    {
                        Key = "Project",
                        Value = "OSPJ"
                    }
                }
            });

            using (StreamWriter writer = new StreamWriter("wwwroot\\FilePath.txt"))
            {
                writer.WriteLine(
                    "{ \r\n   \"Sources\":[ \r\n      { \r\n         \"Id\":\"" + input.LogInputCategory + "\",\r\n         \"SourceType\":\"WindowsEventLogSource\",\r\n         \"Directory\":\"" + input.FilePath + "\",\r\n         \"FileNameFilter\":\" " + input.Filter + "\",\r\n         \"LogName\":\" " + input.Name + " \"\r\n         \"IncludeEventData\" : true\r\n            }\r\n   ],\r\n   \"Sinks\":[ \r\n      { \r\n         \"Id\":\"WinSecurityKinesisFirehose\",\r\n         \"SinkType\":\"KinesisFirehose\",\r\n         \"AccessKey\":\"\",\r\n         \"SecretKey\":\"\",\r\n         \"Region\":\"ap-southeast-1\",\r\n         \"StreamName\":\"" + BucketName2 + "\"\r\n         \"Format\": \"json\"\r\n      }\r\n   ],\r\n   \"Pipes\":[ \r\n      { \r\n         \"Id\":\"WinSecurityPipe\",\r\n         \"SourceRef\":\"WinSecurityLog\",\r\n         \"SinkRef\":\"WinSecurityKinesisFirehose\"\r\n      }\r\n   ],\r\n   \"SelfUpdate\":0\r\n}"



                    );
            }
            return RedirectToAction("Json");
        }

        /*
        public async Task<IActionResult> Manage(int InputID)
        {
            ViewBag.LogPath = FilePath;
            ViewBag.LogName = InputName;
            ViewBag.Filter = Filter;
            ViewBag.LogType = LogType;

            using (StreamWriter writer = new StreamWriter("wwwroot\\FilePath.txt"))
            {
                writer.WriteLine(
                    "{ \n" +
                    "\"Sources\" : [ \n " +
                    "{ \n" +
                    "\"Id\" : \"WindowsEventLog\","



                    );
            }
            return RedirectToAction("Json");
        }
        */

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
