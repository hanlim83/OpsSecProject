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
using System.Text.RegularExpressions;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsSecProject.Data;
using OpsSecProject.Models;
using OpsSecProject.Services;
using OpsSecProject.ViewModels;
using S3Bucket = OpsSecProject.Models.S3Bucket;
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

        public async Task<IActionResult> IndexAsync()
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
            ViewBag.LogPath = input.FilePath; //test asd
            ViewBag.LogName = input.Name; //  will this work
            ViewBag.Filter = input.Filter; //*.log asd
            ViewBag.LogType = input.LogType; //security
            ViewBag.LogInput = input.LogInputCategory; //squidproxy asd
            string lowcap = input.Name.ToLower();
            string pattern = @"[^A-Za-z0-9]+";
            string replacement = "-";
            string replace = Regex.Replace(lowcap, pattern, replacement);
            var BucketName2 = "smart-insight-" + replace;
            var data = "{ \r\n   \"Sources\":[ \r\n      { \r\n         \"Id\":\"" + "WinSecurityLog" + "\",\r\n         \"SourceType\":\"WindowsEventLogSource\",\r\n         \"Directory\":\"" + input.FilePath + "\",\r\n         \"FileNameFilter\":\" " + input.Filter + "\",\r\n         \"LogName\":\" " + input.Name + " \"\r\n         \"IncludeEventData\" : true\r\n            }\r\n   ],\r\n   \"Sinks\":[ \r\n      { \r\n         \"Id\":\"WinSecurityKinesisFirehose\",\r\n         \"SinkType\":\"KinesisFirehose\",\r\n         \"AccessKey\":\""+ Environment.GetEnvironmentVariable("FIREHOSE_ACCESS_KEY_ID")+"\",\r\n         \"SecretKey\":\""+ Environment.GetEnvironmentVariable("FIREHOSE_SECRET_ACCESS_KEY") +"\",\r\n         \"Region\":\"ap-southeast-1\",\r\n         \"StreamName\":\"" + BucketName2 + "\"\r\n         \"Format\": \"json\"\r\n      }\r\n   ],\r\n   \"Pipes\":[ \r\n      { \r\n         \"Id\":\"WinSecurityPipe\",\r\n         \"SourceRef\":\"WinSecurityLog\",\r\n         \"SinkRef\":\"WinSecurityKinesisFirehose\"\r\n      }\r\n   ],\r\n   \"SelfUpdate\":0\r\n}";
            var data2 = "{\r\n  \"cloudwatch.emitMetrics\": false,\r\n  \"awsSecretAccessKey\": \"XW2HNGQnW9ygpvPDzQQemY0AhsFlUGwiKnVpZGbO\",\r\n  \"firehose.endpoint\": \"firehose.ap-southeast-1.amazonaws.com\",\r\n  \"awsAccessKeyId\": \"AKIASXW25GZQH5IABE4P\",\r\n  \"flows\": [\r\n    {\r\n      \"filePattern\": \"/opt/generators/CLF/*.log\",\r\n      \"deliveryStream\": \"SmartInsights-Apache-Web-Logs\",\r\n      \"dataProcessingOptions\": [\r\n                {\r\n                    \"optionName\": \"LOGTOJSON\",\r\n                    \"logFormat\": \"COMMONAPACHELOG\"\r\n                }\r\n            ]\r\n    },\r\n    {\r\n      \"filePattern\": \"/opt/generators/ELF/*.log\",\r\n      \"deliveryStream\": \"\",\r\n      \"dataProcessingOptions\": [\r\n                {\r\n                    \"optionName\": \"LOGTOJSON\",\r\n                    \"logFormat\": \"COMBINEDAPACHELOG\"\r\n                }\r\n            ]      \r\n    },\r\n    {\r\n      \"filePattern\": \"/opt/log/www1/secure.log\",\r\n      \"deliveryStream\": \"SmartInsights-SSH-Login-Logs\",\r\n      \"dataProcessingOptions\": [\r\n                {\r\n                    \"optionName\": \"LOGTOJSON\",\r\n                    \"logFormat\": \"SYSLOG\",\r\n                    \"matchPattern\": \"^([\\\\w]+) ([\\\\w]+) ([\\\\d]+) ([\\\\d]+) ([\\\\w:]+) ([\\\\w]+) ([\\\\w]+)\\\\[([\\\\d]+)\\\\]\\\\: ([\\\\w\\\\s.\\\\:=]+)$\",\r\n                    \"customFieldNames\": [\"weekday\", \"month\", \"day\", \"year\", \"time\", \"host\", \"process\", \"identifer\",\"message\"]\r\n                }\r\n            ]\r\n    },\r\n    {\r\n      \"filePattern\": \"/opt/log/cisco_router1/cisco_ironport_web.log\",\r\n      \"deliveryStream\": \"SmartInsights-Cisco-Squid-Proxy-Logs\",\r\n      \"dataProcessingOptions\": [\r\n                {\r\n                    \"optionName\": \"LOGTOJSON\",\r\n                    \"logFormat\": \"SYSLOG\",\r\n                    \"matchPattern\": \"^([\\\\w.]+) (?:[\\\\d]+) ([\\\\d.]+) ([\\\\w]+)\\\\/([\\\\d]+) ([\\\\d]+) ([\\\\w.]+) ([\\\\S]+) ([\\\\S]+) (?:[\\\\w]+)\\\\/([\\\\S]+) ([\\\\S]+) (?:[\\\\S\\\\s]+)$\",\r\n                    \"customFieldNames\": [\"timestamp\",\"destination_ip_address\",\"action\",\"http_status_code\",\"bytes_in\",\"http_method\",\"requested_url\",\"user\",\"requested_url_domain\",\"content_type\"]\r\n                }\r\n            ]\r\n    }\r\n  ]\r\n}";
            string data3 = "";

            if (input.LogInputCategory.ToString() != "WindowsEventLogs") {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(data2);
                var output = new FileContentResult(bytes, "application/octet-stream")
                {
                    FileDownloadName = "download.json"
                };
                TempData["qwerty"] = data2;
            }
            else {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(data);
                var output = new FileContentResult(bytes, "application/octet-stream")
                {
                    FileDownloadName = "download.json"
                };
                TempData["qwerty"] = data;
            }

            /*PutBucketResponse putBucketResponse1 = await _S3Client.PutBucketAsync(new PutBucketRequest
             {

                 BucketName = "smart-insight-" + replace,
                 UseClientRegion = true,
                 CannedACL = S3CannedACL.Private
             });
             PutBucketTaggingResponse putBucketTaggingResponse1 = await _S3Client.PutBucketTaggingAsync(new PutBucketTaggingRequest
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
             PutPublicAccessBlockResponse putPublicAccessBlockResponse1 = await _S3Client.PutPublicAccessBlockAsync(new PutPublicAccessBlockRequest
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
            });*/
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
            });
            await _logContext.SaveChangesAsync();
            return RedirectToAction("Json");
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
                if (retrieved.LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                {
                    ViewData["DefaultUserFieldIPS"] = "authuser";
                    ViewData["DefaultIPAddressFieldIPS"] = "host";
                    ViewData["DefaultConditionFieldIPS"] = "request";
                    ViewData["DefaultConditionIPS"] = "GET /login_success HTTP/1.0";
                    ViewData["DefaultConditionFieldRCF"] = "response";
                    ViewData["DefaultConditionRCF"] = "5";
                }
                else if (retrieved.LogInputCategory.Equals(LogInputCategory.SSH))
                {
                    ViewData["DefaultUserFieldIPS"] = "message";
                    ViewData["DefaultIPAddressFieldIPS"] = "message";
                    ViewData["DefaultConditionFieldIPS"] = "message";
                    ViewData["DefaultConditionIPS"] = "Accepted password for";
                    ViewData["DefaultConditionFieldRCF"] = "message";
                    ViewData["DefaultConditionRCF"] = "Failed password for";
                }
                foreach (var sagemaker in retrieved.LinkedSagemakerEntities)
                {
                    if (sagemaker.SagemakerAlgorithm.Equals(SagemakerAlgorithm.IP_Insights))
                    {
                        ViewData["IPSExist"] = "YES";
                        break;
                    }
                }
            }
            return View(retrieved);
        }
        [HttpPost]
        public async Task<IActionResult> StartTrainingIPS(string TrainingType, string identitySourceField, string ipAddressSourceField, string condtionSourceField, string ConditionType, string Condtion, int ID)
        {
            if (checkForSQLInjection(identitySourceField) || checkForSQLInjection(ipAddressSourceField) || checkForSQLInjection(condtionSourceField) || checkForSQLInjection(Condtion))
                return StatusCode(403);
            LogInput retrieved = await _logContext.LogInputs.FindAsync(ID);
            if (retrieved == null)
                return StatusCode(500);
            string dbTableName = "dbo." + retrieved.LinkedS3Bucket.Name.Replace("-", "_");
            string condtionalOperator = "";
            switch (ConditionType)
            {
                case "Equals":
                    condtionalOperator = "=";
                    Condtion = "'" + Condtion + "'";
                    break;
                case "NotEquals":
                    condtionalOperator = "!=";
                    Condtion = "'" + Condtion + "'";
                    break;
                case "Similar":
                    condtionalOperator = "LIKE";
                    Condtion = "'%" + Condtion + "%'";
                    break;
                case "NotSimilar":
                    condtionalOperator = "NOT LIKE";
                    Condtion = "'%" + Condtion + "%'";
                    break;
            }
            List<GenericRecordHolder> records = new List<GenericRecordHolder>();
            using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand(@"SELECT " + identitySourceField + ", " + ipAddressSourceField + " FROM " + dbTableName + " WHERE " + condtionSourceField + " " + condtionalOperator + " " + Condtion + " AND response = 200;", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            records.Add(new GenericRecordHolder
                            {
                                field1 = dr.GetValue(0).ToString(),
                                field2 = dr.GetValue(1).ToString(),
                            });
                        }
                    }
                }
            }
            MemoryStream memoryStream = new MemoryStream();
            CsvConfiguration config = new CsvConfiguration(CultureInfo.CurrentCulture)
            {
                HasHeaderRecord = false
            };
            using (var streamWriter = new StreamWriter(memoryStream))
            {
                using (var csvWriter = new CsvWriter(streamWriter, config))
                {
                    csvWriter.WriteRecords(records);
                }
            }
            TransferUtility tu = new TransferUtility(_S3Client);
            string inputDataKey = retrieved.Name.Replace(" ", "-") + "/Input/ipinsights/data-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv";
            string modelFileKey = retrieved.Name.Replace(" ", "-") + "/Model";
            string checkpointKey = retrieved.Name.Replace(" ", "-") + "/Checkpoint";
            string jobName = retrieved.Name.Replace(" ", "-") + "-IPInsights-Training-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            await tu.UploadAsync(new TransferUtilityUploadRequest
            {
                InputStream = new MemoryStream(memoryStream.ToArray()),
                Key = inputDataKey,
                BucketName = _logContext.S3Buckets.Find(2).Name
            });
            CreateTrainingJobRequest createTrainingJobRequest = new CreateTrainingJobRequest
            {
                AlgorithmSpecification = new AlgorithmSpecification
                {
                    TrainingImage = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com/ipinsights:1",
                    TrainingInputMode = TrainingInputMode.File,
                    EnableSageMakerMetricsTimeSeries = false
                },
                EnableManagedSpotTraining = true,
                EnableInterContainerTrafficEncryption = false,
                EnableNetworkIsolation = false,
                HyperParameters = new Dictionary<string, string>
                {
                    { "num_entity_vectors", "20000" },
                    { "random_negative_sampling_rate", "5" },
                    { "vector_dim", "128" },
                    { "mini_batch_size", "1000" },
                    { "epochs", "5" },
                    { "learning_rate", "0.01" }
                },
                InputDataConfig = new List<Channel>
                {
                    new Channel
                    {
                        ChannelName = "train",
                        DataSource = new DataSource
                        {
                            S3DataSource = new S3DataSource
                            {
                                S3DataDistributionType = S3DataDistribution.FullyReplicated,
                                S3DataType = S3DataType.S3Prefix,
                                S3Uri = "s3://" + _logContext.S3Buckets.Find(2).Name+ "/" + inputDataKey
                            }
                        },
                        ContentType = "text/csv"
                    }
                },
                OutputDataConfig = new OutputDataConfig
                {
                    S3OutputPath = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + modelFileKey
                },
                ResourceConfig = new ResourceConfig
                {
                    InstanceCount = 1,
                    InstanceType = TrainingInstanceType.MlP32xlarge,
                    VolumeSizeInGB = 30
                },
                RoleArn = Environment.GetEnvironmentVariable("SAGEMAKER_EXECUTION_ROLE"),
                StoppingCondition = new StoppingCondition
                {
                    MaxRuntimeInSeconds = 3600,
                    MaxWaitTimeInSeconds = 3600
                },
                Tags = new List<Amazon.SageMaker.Model.Tag>
                {
                    new Amazon.SageMaker.Model.Tag
                    {
                        Key = "Project",
                        Value = "OSPJ"
                    }
                },
                TrainingJobName = jobName,
                CheckpointConfig = new CheckpointConfig
                {
                    S3Uri = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + checkpointKey
                }
            };
            CreateTrainingJobResponse createTrainingJobResponse = await _Sclient.CreateTrainingJobAsync(createTrainingJobRequest);
            if (createTrainingJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
            {
                SagemakerConsolidatedEntity newEntity = new SagemakerConsolidatedEntity
                {
                    SagemakerAlgorithm = SagemakerAlgorithm.IP_Insights,
                    CondtionalField = condtionSourceField,
                    Condtion = Condtion.Replace("'", ""),
                    CurrentInputDataKey = inputDataKey,
                    CurrentModelFileKey = modelFileKey,
                    CheckpointKey = checkpointKey,
                    LinkedLogInputID = retrieved.ID,
                    TrainingJobARN = createTrainingJobResponse.TrainingJobArn,
                    TrainingJobName = jobName,
                    SagemakerStatus = SagemakerStatus.Training,
                    SagemakerErrorStage = SagemakerErrorStage.None,
                    IPAddressField = ipAddressSourceField,
                    UserField = identitySourceField
                };
                if (TrainingType.Equals("Auto"))
                    newEntity.TrainingType = Models.TrainingType.Automatic;
                else if (TrainingType.Equals("Manual"))
                    newEntity.TrainingType = Models.TrainingType.Manual;
                _logContext.SagemakerConsolidatedEntities.Add(newEntity);
                await _logContext.SaveChangesAsync();
                TempData["Alert"] = "Success";
                TempData["Message"] = "Training Job Created with ARN: " + createTrainingJobResponse.TrainingJobArn;
                return RedirectToAction("Manage", new { InputID = retrieved.ID });
            }
            else
            {
                TempData["Alert"] = "Warning";
                TempData["Message"] = "Training Job Failed to create";
                return RedirectToAction("Manage", new { InputID = retrieved.ID });
            }
        }

        [HttpPost]
        public async Task<IActionResult> StartTrainingRCF(string TrainingType, string condtionSourceField, string ConditionType, string Condtion, int ID)
        {
            if (checkForSQLInjection(condtionSourceField) || checkForSQLInjection(Condtion))
                return StatusCode(403);
            LogInput retrieved = await _logContext.LogInputs.FindAsync(ID);
            if (retrieved == null)
                return StatusCode(500);
            string dbTableName = "dbo." + retrieved.LinkedS3Bucket.Name.Replace("-", "_");
            string condtionalOperator = "";
            switch (ConditionType)
            {
                case "Equals":
                    condtionalOperator = "=";
                    Condtion = "'" + Condtion + "'";
                    break;
                case "NotEquals":
                    condtionalOperator = "!=";
                    Condtion = "'" + Condtion + "'";
                    break;
                case "Similar":
                    condtionalOperator = "LIKE";
                    Condtion = "'%" + Condtion + "%'";
                    break;
                case "NotSimilar":
                    condtionalOperator = "NOT LIKE";
                    Condtion = "'%" + Condtion + "%'";
                    break;
            }
            List<GenericRecordHolder> records = new List<GenericRecordHolder>();
            using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand(@"SELECT TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' ')), COUNT(" + condtionSourceField + ") FROM " + dbTableName + " WHERE " + condtionSourceField + " " + condtionalOperator + " " + Condtion + " GROUP BY DATEPART(YEAR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' '))), DATEPART(MONTH, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' '))), DATEPART(DAY, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' '))), DATEPART(HOUR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' '))), (DATEPART(MINUTE, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' '))) / 60), " + condtionSourceField + ", TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' ')) ORDER BY TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' '));", connection))
                {
                    cmd.CommandTimeout = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            records.Add(new GenericRecordHolder
                            {
                                field1 = dr.GetValue(0).ToString(),
                                field2 = dr.GetValue(1).ToString(),
                            });
                        }
                    }
                }
            }
            MemoryStream memoryStream = new MemoryStream();
            CsvConfiguration config = new CsvConfiguration(CultureInfo.CurrentCulture)
            {
                HasHeaderRecord = false
            };
            using (var streamWriter = new StreamWriter(memoryStream))
            {
                using (var csvWriter = new CsvWriter(streamWriter, config))
                {
                    csvWriter.WriteRecords(records);
                }
            }
            TransferUtility tu = new TransferUtility(_S3Client);
            string inputDataKey = retrieved.Name.Replace(" ", "-") + "/Input/randomcutforest/data-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv";
            string modelFileKey = retrieved.Name.Replace(" ", "-") + "/Model";
            string checkpointKey = retrieved.Name.Replace(" ", "-") + "/Checkpoint";
            string jobName = retrieved.Name.Replace(" ", "-") + "-RandomCutForest-Training-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            await tu.UploadAsync(new TransferUtilityUploadRequest
            {
                InputStream = new MemoryStream(memoryStream.ToArray()),
                Key = inputDataKey,
                BucketName = _logContext.S3Buckets.Find(2).Name
            });
            CreateTrainingJobRequest createTrainingJobRequest = new CreateTrainingJobRequest
            {
                AlgorithmSpecification = new AlgorithmSpecification
                {
                    TrainingImage = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com/randomcutforest:1",
                    TrainingInputMode = TrainingInputMode.File,
                    EnableSageMakerMetricsTimeSeries = false
                },
                EnableManagedSpotTraining = true,
                EnableInterContainerTrafficEncryption = false,
                EnableNetworkIsolation = false,
                HyperParameters = new Dictionary<string, string>
                {
                    { "num_trees", "50" },
                    { "num_samples_per_tree", "512" },
                    { "feature_dim", "1" },
                    { "mini_batch_size", "1000" }
                },
                InputDataConfig = new List<Channel>
                {
                    new Channel
                    {
                        ChannelName = "train",
                        DataSource = new DataSource
                        {
                            S3DataSource = new S3DataSource
                            {
                                S3DataDistributionType = S3DataDistribution.ShardedByS3Key,
                                S3DataType = S3DataType.S3Prefix,
                                S3Uri = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + inputDataKey.Substring(0,inputDataKey.Length - 28)
                            }
                        },
                        ContentType = "text/csv"
                    }
                },
                OutputDataConfig = new OutputDataConfig
                {
                    S3OutputPath = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + modelFileKey
                },
                ResourceConfig = new ResourceConfig
                {
                    InstanceCount = 1,
                    InstanceType = TrainingInstanceType.MlP32xlarge,
                    VolumeSizeInGB = 30
                },
                RoleArn = Environment.GetEnvironmentVariable("SAGEMAKER_EXECUTION_ROLE"),
                StoppingCondition = new StoppingCondition
                {
                    MaxRuntimeInSeconds = 3600,
                    MaxWaitTimeInSeconds = 3600
                },
                Tags = new List<Amazon.SageMaker.Model.Tag>
                {
                    new Amazon.SageMaker.Model.Tag
                    {
                        Key = "Project",
                        Value = "OSPJ"
                    }
                },
                TrainingJobName = jobName,
                CheckpointConfig = new CheckpointConfig
                {
                    S3Uri = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + checkpointKey
                }
            };
            CreateTrainingJobResponse createTrainingJobResponse = await _Sclient.CreateTrainingJobAsync(createTrainingJobRequest);
            if (createTrainingJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
            {
                SagemakerConsolidatedEntity newEntity = new SagemakerConsolidatedEntity
                {
                    SagemakerAlgorithm = SagemakerAlgorithm.Random_Cut_Forest,
                    CondtionalField = condtionSourceField,
                    Condtion = Condtion.Replace("'", ""),
                    CurrentInputDataKey = inputDataKey,
                    CurrentModelFileKey = modelFileKey,
                    CheckpointKey = checkpointKey,
                    LinkedLogInputID = retrieved.ID,
                    TrainingJobARN = createTrainingJobResponse.TrainingJobArn,
                    TrainingJobName = jobName,
                    SagemakerStatus = SagemakerStatus.Training,
                    SagemakerErrorStage = SagemakerErrorStage.None
                };
                if (TrainingType.Equals("Auto"))
                    newEntity.TrainingType = Models.TrainingType.Automatic;
                else if (TrainingType.Equals("Manual"))
                    newEntity.TrainingType = Models.TrainingType.Manual;
                _logContext.SagemakerConsolidatedEntities.Add(newEntity);
                await _logContext.SaveChangesAsync();
                TempData["Alert"] = "Success";
                TempData["Message"] = "Training Job Created with ARN: " + createTrainingJobResponse.TrainingJobArn;
                return RedirectToAction("Manage", new { InputID = retrieved.ID });
            }
            else
            {
                TempData["Alert"] = "Warning";
                TempData["Message"] = "Training Job Failed to create";
                return RedirectToAction("Manage", new { InputID = retrieved.ID });
            }
        }

        public async Task<IActionResult> Deploy(int SageMakerID)
        {
            SagemakerConsolidatedEntity operatedEntity = await _logContext.SagemakerConsolidatedEntities.FindAsync(SageMakerID);
            string currentModel = operatedEntity.CurrentModelName;
            string endpointConfig = operatedEntity.EndpointConfigurationName;
            string endpointName = operatedEntity.LinkedLogInput.Name.Replace(" ", "-") + "Endpoint" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            if (currentModel == null && operatedEntity.DeprecatedModelNames == null)
            {
                currentModel = operatedEntity.LinkedLogInput.Name.Replace(" ", "-") + "Model" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                CreateModelRequest createModelRequest = new CreateModelRequest
                {
                    EnableNetworkIsolation = false,
                    ModelName = currentModel,
                    ExecutionRoleArn = Environment.GetEnvironmentVariable("SAGEMAKER_EXECUTION_ROLE"),
                    Tags = new List<Tag>
                {
                    new Tag
                    {
                        Key = "Project",
                        Value = "OSPJ"
                    }
                }
                };
                if (operatedEntity.SagemakerAlgorithm.Equals(SagemakerAlgorithm.IP_Insights))
                    createModelRequest.PrimaryContainer = new ContainerDefinition
                    {
                        Image = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com/ipinsights:1",
                        ModelDataUrl = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + operatedEntity.CurrentModelFileKey + "/" + operatedEntity.TrainingJobName + "/output/model.tar.gz"
                    };
                else if (operatedEntity.SagemakerAlgorithm.Equals(SagemakerAlgorithm.Random_Cut_Forest))
                    createModelRequest.PrimaryContainer = new ContainerDefinition
                    {
                        Image = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com/randomcutforest:1",
                        ModelDataUrl = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + operatedEntity.CurrentModelFileKey + "/" + operatedEntity.TrainingJobName + "/output/model.tar.gz"
                    };
                CreateModelResponse createModelResponse = await _Sclient.CreateModelAsync(createModelRequest);
                if (createModelResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                    operatedEntity.CurrentModelName = currentModel;
            }
            if (endpointConfig == null)
            {
                endpointConfig = operatedEntity.LinkedLogInput.Name.Replace(" ", "-") + "EndpointConfig" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                CreateEndpointConfigRequest createEndpointConfigRequest = new CreateEndpointConfigRequest
                {
                    EndpointConfigName = endpointConfig,
                    ProductionVariants = new List<ProductionVariant>
                {
                    new ProductionVariant
                    {
                        VariantName = operatedEntity.LinkedLogInput.Name.Replace(" ", "-") + "ProductionVariant" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                        ModelName = currentModel,
                        InitialInstanceCount = 1,
                        InstanceType = ProductionVariantInstanceType.MlM4Xlarge,
                        InitialVariantWeight = 1
                    }
                },
                    Tags = new List<Tag>
                {
                    new Tag
                    {
                        Key = "Project",
                        Value = "OSPJ"
                    }
                }
                };
                CreateEndpointConfigResponse createEndpointConfigResponse = await _Sclient.CreateEndpointConfigAsync(createEndpointConfigRequest);
                if (createEndpointConfigResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    operatedEntity.EndpointConfigurationARN = createEndpointConfigResponse.EndpointConfigArn;
                    operatedEntity.EndpointConfigurationName = endpointConfig;
                }
            }
            CreateEndpointRequest createEndpointRequest = new CreateEndpointRequest
            {
                EndpointConfigName = endpointConfig,
                EndpointName = endpointName,
                Tags = new List<Tag>
                    {
                        new Tag
                        {
                            Key = "Project",
                            Value = "OSPJ"
                        }
                    }
            };
            CreateEndpointResponse createEndpointResponse = await _Sclient.CreateEndpointAsync(createEndpointRequest);
            if (createEndpointResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
            {
                operatedEntity.EndpointJobARN = createEndpointResponse.EndpointArn;
                operatedEntity.EndpointName = createEndpointRequest.EndpointName;
                operatedEntity.SagemakerStatus = SagemakerStatus.Deploying;
                operatedEntity.SagemakerErrorStage = SagemakerErrorStage.None;
                _logContext.SagemakerConsolidatedEntities.Update(operatedEntity);
                await _logContext.SaveChangesAsync();
                TempData["Alert"] = "Success";
                TempData["Message"] = "Inference Endpoint Deployment Jobs Created with ARN: " + createEndpointResponse.EndpointArn;
                return RedirectToAction("Manage", new { InputID = operatedEntity.LinkedLogInputID });
            }
            else
            {
                TempData["Alert"] = "Warning";
                TempData["Message"] = "Inference Endpoint Configuration and Inference Endpoint Deployment Jobs Failed to create";
                return RedirectToAction("Manage", new { InputID = operatedEntity.LinkedLogInputID });
            }
        }
        public async Task<IActionResult> Remove(int SageMakerID)
        {
            SagemakerConsolidatedEntity operatedEntity = await _logContext.SagemakerConsolidatedEntities.FindAsync(SageMakerID);
            if (operatedEntity.SagemakerStatus.Equals(SagemakerStatus.Ready))
            {
                DeleteEndpointResponse deleteEndpointResponse = await _Sclient.DeleteEndpointAsync(new DeleteEndpointRequest
                {
                    EndpointName = operatedEntity.EndpointName
                });
                _queue.QueueBackgroundWorkItem(async token =>
                {
                    _logger.LogInformation("Deletion of SageMaker remnant resources scheduled");
                    await Task.Delay(TimeSpan.FromMinutes(5), token);
                    DeleteEndpointConfigResponse deleteEndpointConfigResponse = await _Sclient.DeleteEndpointConfigAsync(new DeleteEndpointConfigRequest
                    {
                        EndpointConfigName = operatedEntity.EndpointConfigurationName
                    });
                    if (deleteEndpointConfigResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                    {
                        await _Sclient.DeleteModelAsync(new DeleteModelRequest
                        {
                            ModelName = operatedEntity.CurrentModelName
                        });
                    }
                });
            }
            _logContext.SagemakerConsolidatedEntities.Remove(operatedEntity);
            await _logContext.SaveChangesAsync();
            TempData["Alert"] = "Success";
            TempData["Message"] = "Machine Learning Model deleted successfully!";
            return RedirectToAction("Manage", new { InputID = operatedEntity.LinkedLogInputID });
        }

        private static string GetRdsConnectionString()
        {
            string hostname = Environment.GetEnvironmentVariable("RDS_HOSTNAME");
            string port = Environment.GetEnvironmentVariable("RDS_PORT");
            string username = Environment.GetEnvironmentVariable("RDS_USERNAME");
            string password = Environment.GetEnvironmentVariable("RDS_PASSWORD");

            return $"Data Source={hostname},{port};Initial Catalog=IngestedData;User ID={username};Password={password};";
        }
        private static bool checkForSQLInjection(string userInput)
        {
            bool isSQLInjection = false;
            string[] sqlCheckList = { "--", ";--", ";", "/*", "*/", "@@", "@", "char", "nchar", "varchar", "nvarchar", "alter", "begin", "cast", "create", "cursor", "declare", "delete", "drop", "end", "exec", "execute", "fetch", "insert", "kill", "select", "sys", "sysobjects", "syscolumns", "table", "update" };
            string CheckString = userInput.Replace("'", "''");
            for (int i = 0; i <= sqlCheckList.Length - 1; i++)
            {
                if ((CheckString.IndexOf(sqlCheckList[i], StringComparison.OrdinalIgnoreCase) >= 0))
                    isSQLInjection = true;
                if (isSQLInjection == true)
                    break;
            }
            return isSQLInjection;
        }
    }
    class GenericRecordHolder
    {
        public string field1 { get; set; }
        public string field2 { get; set; }
    }
}
