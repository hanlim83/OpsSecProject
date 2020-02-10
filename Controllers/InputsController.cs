using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.KinesisFirehose;
using Amazon.KinesisFirehose.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SageMaker;
using Amazon.SageMakerRuntime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsSecProject.Data;
using OpsSecProject.Models;
using OpsSecProject.Services;
using OpsSecProject.ViewModels;

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

        public async Task<IActionResult> Index()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            User user = await _accountContext.Users.Where(u => u.Username == currentIdentity).FirstOrDefaultAsync();
          
            return View(new InputsOverrallViewModel
            {
                allUsers = _accountContext.Users.ToList(),
                currentUser = _accountContext.Users.Where(u => u.Username.Equals(currentIdentity)).FirstOrDefault(),
                inputs = _logContext.LogInputs.ToList(),
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
            ViewBag.LogPath = input.FilePath;
            ViewBag.LogName = input.Name;
            ViewBag.Filter = input.Filter;
            ViewBag.LogType = input.LogType; 
            ViewBag.LogInput = input.LogInputCategory;
            string lowcap = input.Name.ToLower();
            string pattern = @"[^A-Za-z0-9]+";
            string replacement = "-";
            string replace = Regex.Replace(lowcap, pattern, replacement);
            var BucketName2 = "smart-insight-" + replace;
            var data = "{ \r\n   \"Sources\":[ \r\n      { \r\n         \"Id\":\"" + "WinSecurityLog" + "\",\r\n         \"SourceType\":\"WindowsEventLogSource\",\r\n         \"Directory\":\"" + input.FilePath + "\",\r\n         \"FileNameFilter\":\" " + input.Filter + "\",\r\n         \"LogName\":\" " + input.Name + " \"\r\n         \"IncludeEventData\" : true\r\n            }\r\n   ],\r\n   \"Sinks\":[ \r\n      { \r\n         \"Id\":\"WinSecurityKinesisFirehose\",\r\n         \"SinkType\":\"KinesisFirehose\",\r\n         \"AccessKey\":\""+ Environment.GetEnvironmentVariable("FIREHOSE_ACCESS_KEY_ID")+"\",\r\n         \"SecretKey\":\""+ Environment.GetEnvironmentVariable("FIREHOSE_SECRET_ACCESS_KEY") +"\",\r\n         \"Region\":\"ap-southeast-1\",\r\n         \"StreamName\":\"" + BucketName2 + "\"\r\n         \"Format\": \"json\"\r\n      }\r\n   ],\r\n   \"Pipes\":[ \r\n      { \r\n         \"Id\":\"WinSecurityPipe\",\r\n         \"SourceRef\":\"WinSecurityLog\",\r\n         \"SinkRef\":\"WinSecurityKinesisFirehose\"\r\n      }\r\n   ],\r\n   \"SelfUpdate\":0\r\n}";
            var data2 = "{\r\n  \"cloudwatch.emitMetrics\": false,\r\n  \"awsSecretAccessKey\": \"XW2HNGQnW9ygpvPDzQQemY0AhsFlUGwiKnVpZGbO\",\r\n  \"firehose.endpoint\": \"firehose.ap-southeast-1.amazonaws.com\",\r\n  \"awsAccessKeyId\": \"AKIASXW25GZQH5IABE4P\",\r\n  \"flows\": [\r\n    {\r\n      \"filePattern\": \"/opt/generators/CLF/*.log\",\r\n      \"deliveryStream\": \"SmartInsights-Apache-Web-Logs\",\r\n      \"dataProcessingOptions\": [\r\n                {\r\n                    \"optionName\": \"LOGTOJSON\",\r\n                    \"logFormat\": \"COMMONAPACHELOG\"\r\n                }\r\n            ]\r\n    },\r\n    {\r\n      \"filePattern\": \"/opt/generators/ELF/*.log\",\r\n      \"deliveryStream\": \"\",\r\n      \"dataProcessingOptions\": [\r\n                {\r\n                    \"optionName\": \"LOGTOJSON\",\r\n                    \"logFormat\": \"COMBINEDAPACHELOG\"\r\n                }\r\n            ]      \r\n    },\r\n    {\r\n      \"filePattern\": \"/opt/log/www1/secure.log\",\r\n      \"deliveryStream\": \"SmartInsights-SSH-Login-Logs\",\r\n      \"dataProcessingOptions\": [\r\n                {\r\n                    \"optionName\": \"LOGTOJSON\",\r\n                    \"logFormat\": \"SYSLOG\",\r\n                    \"matchPattern\": \"^([\\\\w]+) ([\\\\w]+) ([\\\\d]+) ([\\\\d]+) ([\\\\w:]+) ([\\\\w]+) ([\\\\w]+)\\\\[([\\\\d]+)\\\\]\\\\: ([\\\\w\\\\s.\\\\:=]+)$\",\r\n                    \"customFieldNames\": [\"weekday\", \"month\", \"day\", \"year\", \"time\", \"host\", \"process\", \"identifer\",\"message\"]\r\n                }\r\n            ]\r\n    },\r\n    {\r\n      \"filePattern\": \"/opt/log/cisco_router1/cisco_ironport_web.log\",\r\n      \"deliveryStream\": \"SmartInsights-Cisco-Squid-Proxy-Logs\",\r\n      \"dataProcessingOptions\": [\r\n                {\r\n                    \"optionName\": \"LOGTOJSON\",\r\n                    \"logFormat\": \"SYSLOG\",\r\n                    \"matchPattern\": \"^([\\\\w.]+) (?:[\\\\d]+) ([\\\\d.]+) ([\\\\w]+)\\\\/([\\\\d]+) ([\\\\d]+) ([\\\\w.]+) ([\\\\S]+) ([\\\\S]+) (?:[\\\\w]+)\\\\/([\\\\S]+) ([\\\\S]+) (?:[\\\\S\\\\s]+)$\",\r\n                    \"customFieldNames\": [\"timestamp\",\"destination_ip_address\",\"action\",\"http_status_code\",\"bytes_in\",\"http_method\",\"requested_url\",\"user\",\"requested_url_domain\",\"content_type\"]\r\n                }\r\n            ]\r\n    }\r\n  ]\r\n}";
            string data3 = "";
            PutBucketResponse putBucketResponse = await _S3Client.PutBucketAsync(new PutBucketRequest
            {

                BucketName = "smart-insight-" + replace,
                UseClientRegion = true,
                CannedACL = S3CannedACL.Private
            });
            PutBucketTaggingResponse putBucketTaggingResponse = await _S3Client.PutBucketTaggingAsync(new PutBucketTaggingRequest
            {
                BucketName = "smart-insight-" + replace,
                TagSet = new List<Amazon.S3.Model.Tag>
                 {
                     new Amazon.S3.Model.Tag
                     {
                         Key="Project",
                         Value = "OSPJ"
                     }
                 }
            });
            PutPublicAccessBlockResponse putPublicAccessBlockResponse = await _S3Client.PutPublicAccessBlockAsync(new PutPublicAccessBlockRequest
            {
                BucketName = "smart-insight-" + replace,
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
                DeliveryStreamName = "smart-insight-" + replace,
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
            _logContext.S3Buckets.Add(new Models.S3Bucket
            {
                Name = BucketName2
            });
            await _logContext.SaveChangesAsync();
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            User user = await _accountContext.Users.Where(u => u.Username == currentIdentity).FirstOrDefaultAsync();
            Models.S3Bucket bucket = await _logContext.S3Buckets.Where(b => b.Name.Equals(BucketName2)).FirstOrDefaultAsync();
            if (input.LogInputCategory.ToString() != "WindowsEventLogs") {
                data3 = data2;
            }
            else {
                data3 = data;
            }
            _logContext.LogInputs.Add(new Models.LogInput
            {
                Name = input.Name,
                FirehoseStreamName = BucketName2,
                ConfigurationJSON = data3,
                LogInputCategory = input.LogInputCategory,
                LinkedUserID = user.ID,
                LinkedS3BucketID = bucket.ID,
                FilePath = input.FilePath,
                Filter = input.Filter,
                LogType = input.LogType,
            });
            try
            {
                await _logContext.SaveChangesAsync();
                TempData["Alert"] = "Success";
                TempData["Message"] = "Log Input " + input.Name + " created successfully!";
                return RedirectToAction("Manage", new { InputID = _logContext.LogInputs.Where(LI => LI.Name.Equals(input.Name)).FirstOrDefault().ID });
            } catch (DbUpdateException)
            {
                TempData["Alert"] = "Danger";
                TempData["Message"] = "Error Creating log input " + input.Name + "!";
                return View(input);
            }
        }

        public async Task<IActionResult> Remove(int InputID)
        {
            LogInput retrieved = await _logContext.LogInputs.FindAsync(InputID);
            if (retrieved == null)
                return StatusCode(404);
            DeleteDeliveryStreamResponse deleteDeliveryStreamResponse = await _FirehoseClient.DeleteDeliveryStreamAsync(new DeleteDeliveryStreamRequest
            {
                DeliveryStreamName = retrieved.FirehoseStreamName
            }) ;

            DeleteBucketResponse deleteBucketResponse = await _S3Client.DeleteBucketAsync(new DeleteBucketRequest
            {
                BucketName = retrieved.LinkedS3Bucket.Name,
                UseClientRegion = true,
            });

            _logContext.LogInputs.Remove(retrieved);
            await _logContext.SaveChangesAsync();
            return RedirectToAction("Index");

        }

            public async Task<IActionResult> Manage(int InputID)
        {
            LogInput retrieved = await _logContext.LogInputs.FindAsync(InputID);
            if (retrieved == null)
                return StatusCode(404);
            if (retrieved.InitialIngest == true)
            {
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
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM " + dbTableName + ";", connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                ViewData["LogInputEventCount"] = dr.GetValue(0);
                            }
                        }
                    }
                }
            }
            return View(retrieved);
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
