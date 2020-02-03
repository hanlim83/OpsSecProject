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
using Amazon.S3;
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
        
        public InputsController(LogContext logContext, IBackgroundTaskQueue queue, ILogger<InputsController> logger, AccountContext accountContext, IAmazonSageMaker Sclient, IAmazonSageMakerRuntime SRClient, IAmazonS3 S3Client)
        {
            _logContext = logContext;
            _queue = queue;
            _logger = logger;
            _accountContext = accountContext;
            _Sclient = Sclient;
            _SRClient = SRClient;
            _S3Client = S3Client;
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
        public IActionResult Create(string FilePath, string InputName, string Filter, string LogType)
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

        public async Task<IActionResult> Manage(int InputID)
        {
            LogInput retrieved = await _logContext.LogInputs.FindAsync(InputID);
            if (retrieved == null)
                return StatusCode(404);
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
            } else if (retrieved.LogInputCategory.Equals(LogInputCategory.SSH))
            {
                ViewData["DefaultUserFieldIPS"] = "message";
                ViewData["DefaultIPAddressFieldIPS"] = "message";
                ViewData["DefaultConditionFieldIPS"] = "message";
                ViewData["DefaultConditionIPS"] = "Accepted password for";
                ViewData["DefaultConditionFieldRCF"] = "message";
                ViewData["DefaultConditionRCF"] = "Failed password for";
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
                using (SqlCommand cmd = new SqlCommand(@"SELECT " + identitySourceField + ", " + ipAddressSourceField + " FROM " + dbTableName + " WHERE " + condtionSourceField + " " + condtionalOperator + " " + Condtion + " ;", connection))
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
            string inputDataKey = retrieved.Name + "/Input/ipinsights/data-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv";
            string modelFileKey = retrieved.Name + "/Model/ipinsights/model-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".tar.gz";
            string checkpointKey = retrieved.Name + "/Checkpoint/ipinsights/" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "/";
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
                        ChannelName = "Training",
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
                    MaxRuntimeInSeconds = 14400,
                    MaxWaitTimeInSeconds = 86400
                },
                Tags = new List<Tag>
                {
                    new Tag
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
                    Condtion = Condtion,
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
            string inputDataKey = retrieved.Name + "/Input/randomcutforest/data-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv";
            string modelFileKey = retrieved.Name + "/Model/randomcutforest/model-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".tar.gz";
            string checkpointKey = retrieved.Name + "/Checkpoint/randomcutforest/" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "/";
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
                        ChannelName = "Training",
                        DataSource = new DataSource
                        {
                            S3DataSource = new S3DataSource
                            {
                                S3DataDistributionType = S3DataDistribution.ShardedByS3Key,
                                S3DataType = S3DataType.S3Prefix,
                                S3Uri = "s3://" + _logContext.S3Buckets.Find(2).Name + inputDataKey.Substring(0,inputDataKey.Length - 28)
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
                    MaxRuntimeInSeconds = 14400,
                    MaxWaitTimeInSeconds = 86400
                },
                Tags = new List<Tag>
                {
                    new Tag
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
                    Condtion = Condtion,
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