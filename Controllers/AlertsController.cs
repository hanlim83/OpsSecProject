using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.SageMaker;
using Amazon.SageMaker.Model;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsSecProject.Data;
using OpsSecProject.Models;
using OpsSecProject.Services;
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
using CompressionType = Amazon.SageMaker.CompressionType;

namespace OpsSecProject.Controllers
{
    public class AlertsController : Controller
    {
        private readonly AccountContext _accountContext;
        private readonly LogContext _logContext;
        private readonly IAmazonSageMaker _Sclient;
        private readonly IAmazonS3 _S3Client;
        private IBackgroundTaskQueue _queue { get; }
        private readonly ILogger _logger;

        public AlertsController(AccountContext accountContext, LogContext logContext, IAmazonSageMaker Sclient, IAmazonS3 S3Client, IBackgroundTaskQueue queue, ILogger<AlertsController> logger)
        {
            _accountContext = accountContext;
            _logContext = logContext;
            _Sclient = Sclient;
            _S3Client = S3Client;
            _queue = queue;
            _logger = logger;
        }
        public async Task<IActionResult> Index()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            User user = await _accountContext.Users.Where(u => u.Username == currentIdentity).FirstOrDefaultAsync();
            return View(new AlertsViewModel
            {
                allAlerts = _accountContext.Alerts.Where(a => a.LinkedUserID == user.ID).ToList(),
                successAlerts = _accountContext.Alerts.Where(a => a.LinkedUserID == user.ID).Where(a => a.AlertType.Equals(AlertType.InputIngestSuccess) || a.AlertType.Equals(AlertType.SageMakerTrained) || a.AlertType.Equals(AlertType.SageMakerDeployed)).ToList().Count(),
                informationalAlerts = _accountContext.Alerts.Where(a => a.LinkedUserID == user.ID).Where(a => a.AlertType.Equals(AlertType.SageMakerBatchTransformCompleted) || a.AlertType.Equals(AlertType.InputIngestPending) || a.AlertType.Equals(AlertType.MajorInformationChange)).ToList().Count(),
                warningAlerts = _accountContext.Alerts.Where(a => a.LinkedUserID == user.ID).Where(a => a.AlertType.Equals(AlertType.MetricExceeded) || a.AlertType.Equals(AlertType.SageMakerPredictionTriggered)).ToList().Count()
            });
        }
        public async Task<IActionResult> Manage(int LogInputID)
        {
            ViewData["LogInputID"] = LogInputID;
            return View(await _logContext.AlertTriggers.Where(A => A.LinkedLogInputID.Equals(LogInputID)).ToListAsync());
        }
        public async Task<IActionResult> Create(int LogInputID)
        {
            ViewData["LogInputID"] = LogInputID;
            LogInput retrieved = await _logContext.LogInputs.FindAsync(LogInputID);
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
            }
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Create([Bind("Name", "Condtion", "CondtionalField", "CondtionType", "AlertTriggerType", "LinkedLogInputID", "IPAddressField", "UserField")]Trigger AlertTrigger)
        {
            LogInput retrieved = _logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID);
            string dbTableName = "dbo." + retrieved.LinkedS3Bucket.Name.Replace("-", "_");
            string condtionalOperator = string.Empty, Condtion = string.Empty, dateTimeField = string.Empty, num_entities = string.Empty;
            List<String> userNames = new List<string>();
            switch (AlertTrigger.CondtionType)
            {
                case "Equal":
                    condtionalOperator = "=";
                    Condtion = "'" + AlertTrigger.Condtion + "'";
                    break;
                case "NotEqual":
                    condtionalOperator = "!=";
                    Condtion = "'" + AlertTrigger.Condtion + "'";
                    break;
                case "Similar":
                    condtionalOperator = "LIKE";
                    Condtion = "'%" + AlertTrigger.Condtion + "%'";
                    break;
                case "NotSimilar":
                    condtionalOperator = "NOT LIKE";
                    Condtion = "'%" + AlertTrigger.Condtion + "%'";
                    break;
                case "LessThan":
                    condtionalOperator = "<";
                    Condtion = "'" + AlertTrigger.Condtion + "'";
                    break;
                case "LessOrEqualThan":
                    condtionalOperator = "<=";
                    Condtion = "'" + AlertTrigger.Condtion + "'";
                    break;
                case "MoreThan":
                    condtionalOperator = ">";
                    Condtion = "'" + AlertTrigger.Condtion + "'";
                    break;
                case "MoreOrEqualThan":
                    condtionalOperator = ">=";
                    Condtion = "'" + AlertTrigger.Condtion + "'";
                    break;
            }
            if (AlertTrigger.AlertTriggerType.Equals(AlertTriggerType.IPInsights))
            {
                List<GenericRecordHolder> records = new List<GenericRecordHolder>();
                using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
                {
                    connection.Open();
                    string sqlQuery1 = string.Empty, sqlQuery2 = string.Empty;
                    if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                    {
                        dateTimeField = "datetime";
                        sqlQuery1 = @"SELECT ROW_NUMBER() OVER(ORDER BY " + dateTimeField + " ASC), " + AlertTrigger.UserField + ", " + AlertTrigger.IPAddressField + " FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " AND response = 200;";
                    }
                    else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH))
                    {
                        if (AlertTrigger.IPAddressField.Equals(AlertTrigger.UserField))
                            sqlQuery1 = @"SELECT ROW_NUMBER() OVER(ORDER BY year, month, day, time) FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + ";";
                        else
                            return StatusCode(500);
                    }
                    else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SquidProxy))
                    {
                        dateTimeField = "timestamp";
                        sqlQuery1 = @"SELECT ROW_NUMBER() OVER(ORDER BY " + dateTimeField + " ASC), " + AlertTrigger.UserField + ", " + AlertTrigger.IPAddressField + " FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + ";";
                    }
                    using (SqlCommand cmd = new SqlCommand(sqlQuery1, connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                AlertTrigger.InferenceBookmark = Convert.ToInt32(dr.GetValue(0));
                                AlertTrigger.TrainingBookmark = Convert.ToInt32(dr.GetValue(0));
                                if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH))
                                {
                                    records.Add(new GenericRecordHolder
                                    {
                                        field1 = dr.GetValue(1).ToString().Substring(dr.GetValue(1).ToString().IndexOf("for") + 4, dr.GetValue(1).ToString().IndexOf("from") - 1),
                                        field2 = dr.GetValue(1).ToString().Substring(dr.GetValue(1).ToString().IndexOf("from") + 5, dr.GetValue(1).ToString().IndexOf("port") - 1),
                                    });
                                }
                                else
                                {
                                    records.Add(new GenericRecordHolder
                                    {
                                        field1 = dr.GetValue(1).ToString(),
                                        field2 = dr.GetValue(2).ToString(),
                                    });
                                }
                            }
                        }
                    }
                    if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer) || _logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SquidProxy))
                        sqlQuery2 = @"SELECT count(DISTINCT " + AlertTrigger.UserField + ") FROM " + dbTableName + ";";
                    else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH))
                        sqlQuery2 = sqlQuery1;
                    using (SqlCommand cmd = new SqlCommand(sqlQuery2, connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH))
                                {
                                    userNames.Add(dr.GetValue(1).ToString().Substring(dr.GetValue(1).ToString().IndexOf("for") + 4, dr.GetValue(1).ToString().IndexOf("from") - 1));
                                }
                                else
                                    num_entities = (Convert.ToInt32(dr.GetValue(0)) * 2).ToString();
                            }
                        }
                        num_entities = userNames.Distinct().ToList().Count().ToString();
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
                    BucketName = _logContext.S3Buckets.Find(1).Name
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
                    { "num_entity_vectors", num_entities },
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
                                S3Uri = "s3://" + _logContext.S3Buckets.Find(1).Name+ "/" + inputDataKey
                            }
                        },
                        ContentType = "text/csv"
                    }
                },
                    OutputDataConfig = new OutputDataConfig
                    {
                        S3OutputPath = "s3://" + _logContext.S3Buckets.Find(1).Name + "/" + modelFileKey
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
                        S3Uri = "s3://" + _logContext.S3Buckets.Find(1).Name + "/" + checkpointKey
                    }
                };
                CreateTrainingJobResponse createTrainingJobResponse = await _Sclient.CreateTrainingJobAsync(createTrainingJobRequest);
                if (createTrainingJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    AlertTrigger.CurrentInputDataKey = inputDataKey;
                    AlertTrigger.CurrentModelFileKey = modelFileKey;
                    AlertTrigger.CheckpointKey = checkpointKey;
                    AlertTrigger.TrainingJobName = jobName;
                    AlertTrigger.TrainingJobARN = createTrainingJobResponse.TrainingJobArn;
                    AlertTrigger.SagemakerStatus = SagemakerStatus.Training;
                    AlertTrigger.SagemakerErrorStage = SagemakerErrorStage.None;
                }
            }
            else if (AlertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
            {
                List<WithTimestampRecordHolder> records = new List<WithTimestampRecordHolder>();
                using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
                {
                    connection.Open();
                    string sqlQuery = string.Empty;
                    if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                    {
                        dateTimeField = "datetime";
                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))),TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' ')), COUNT(" + AlertTrigger.CondtionalField + ") FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " GROUP BY DATEPART(YEAR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(MONTH, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(DAY, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(HOUR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), (DATEPART(MINUTE, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))) / 60), " + AlertTrigger.CondtionalField + ", TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))";
                    }
                    else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH))
                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY year, month, day, time), day,month,year,time, count(*) FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " BY year, month, day, time;";
                    else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SquidProxy))
                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY timestamp), timestamp, count(*) FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " BY timestamp;";
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                AlertTrigger.InferenceBookmark = Convert.ToInt32(dr.GetValue(0));
                                AlertTrigger.TrainingBookmark = Convert.ToInt32(dr.GetValue(0));
                                if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                                {
                                    records.Add(new WithTimestampRecordHolder
                                    {
                                        Timesetamp = Convert.ToDateTime(dr.GetValue(1).ToString()),
                                        field = dr.GetValue(2).ToString(),
                                    });
                                }
                                else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH))
                                {
                                    int month = 0;
                                    switch (dr.GetValue(1).ToString())
                                    {
                                        case "Jan":
                                            month = 1;
                                            break;
                                        case "Feb":
                                            month = 2;
                                            break;
                                        case "Mar":
                                            month = 3;
                                            break;
                                        case "Apr":
                                            month = 4;
                                            break;
                                        case "May":
                                            month = 5;
                                            break;
                                        case "Jun":
                                            month = 6;
                                            break;
                                        case "July":
                                            month = 7;
                                            break;
                                        case "Aug":
                                            month = 8;
                                            break;
                                        case "Sep":
                                            month = 9;
                                            break;
                                        case "Oct":
                                            month = 10;
                                            break;
                                        case "Nov":
                                            month = 11;
                                            break;
                                        case "Dec":
                                            month = 12;
                                            break;
                                    }
                                    if (month < 10)
                                    {
                                        records.Add(new WithTimestampRecordHolder
                                        {
                                            Timesetamp = Convert.ToDateTime(dr.GetValue(1).ToString() + "/0" + month + dr.GetValue(2).ToString() + "/" + dr.GetValue(3).ToString() + " " + dr.GetValue(4).ToString()),
                                            field = dr.GetValue(2).ToString(),
                                        });
                                    }
                                    else
                                    {
                                        records.Add(new WithTimestampRecordHolder
                                        {
                                            Timesetamp = Convert.ToDateTime(dr.GetValue(1).ToString()),
                                            field = dr.GetValue(2).ToString(),
                                        });
                                    }
                                }
                                else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SquidProxy))
                                {
                                    records.Add(new WithTimestampRecordHolder
                                    {
                                        Timesetamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Convert.ToInt64(dr.GetValue(1))),
                                        field = dr.GetValue(2).ToString(),
                                    });
                                }

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
                                S3Uri = "s3://" + _logContext.S3Buckets.Find(1).Name + "/" + inputDataKey.Substring(0,inputDataKey.Length - 28)
                            }
                        },
                        ContentType = "text/csv"
                    }
                },
                    OutputDataConfig = new OutputDataConfig
                    {
                        S3OutputPath = "s3://" + _logContext.S3Buckets.Find(1).Name + "/" + modelFileKey
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
                        S3Uri = "s3://" + _logContext.S3Buckets.Find(1).Name + "/" + checkpointKey
                    }
                };
                CreateTrainingJobResponse createTrainingJobResponse = await _Sclient.CreateTrainingJobAsync(createTrainingJobRequest);
                if (createTrainingJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    AlertTrigger.CurrentInputDataKey = inputDataKey;
                    AlertTrigger.CurrentModelFileKey = modelFileKey;
                    AlertTrigger.CheckpointKey = checkpointKey;
                    AlertTrigger.TrainingJobName = jobName;
                    AlertTrigger.TrainingJobARN = createTrainingJobResponse.TrainingJobArn;
                    AlertTrigger.SagemakerStatus = SagemakerStatus.Training;
                    AlertTrigger.SagemakerErrorStage = SagemakerErrorStage.None;
                }
            }
            _logContext.AlertTriggers.Add(AlertTrigger);
            await _logContext.SaveChangesAsync();
            return RedirectToAction("Manage", new { LogInputID = AlertTrigger.LinkedLogInputID });
        }
        public async Task<IActionResult> Edit(int TriggerID)
        {
            Trigger alert = await _logContext.AlertTriggers.FindAsync(TriggerID);
            LogInput retrieved = alert.LinkedLogInput;
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
            }
            return View(alert);
        }
        [HttpPost]
        public async Task<IActionResult> Edit([Bind("Name", "Condtion", "CondtionalField", "CondtionType", "AlertTriggerType")]Trigger AlertTrigger)
        {
            _logContext.AlertTriggers.Update(AlertTrigger);
            await _logContext.SaveChangesAsync();
            return RedirectToAction("Manage", new { LogInputID = AlertTrigger.LinkedLogInputID });
        }
        public async Task<IActionResult> Remove(int TriggerID)
        {
            Trigger deleted = _logContext.AlertTriggers.Find(TriggerID);
            _logContext.AlertTriggers.Remove(deleted);
            await _logContext.SaveChangesAsync();
            if (deleted.AlertTriggerType.Equals(AlertTriggerType.IPInsights) || deleted.AlertTriggerType.Equals(AlertTriggerType.RCF))
            {
                if (deleted.SagemakerStatus.Equals(SagemakerStatus.Ready))
                {
                    DeleteEndpointResponse deleteEndpointResponse = await _Sclient.DeleteEndpointAsync(new DeleteEndpointRequest
                    {
                        EndpointName = deleted.EndpointName
                    });
                    _queue.QueueBackgroundWorkItem(async token =>
                    {
                        _logger.LogInformation("Deletion of SageMaker remnant resources scheduled");
                        await Task.Delay(TimeSpan.FromMinutes(5), token);
                        DeleteEndpointConfigResponse deleteEndpointConfigResponse = await _Sclient.DeleteEndpointConfigAsync(new DeleteEndpointConfigRequest
                        {
                            EndpointConfigName = deleted.EndpointConfigurationName
                        });
                        if (deleteEndpointConfigResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                        {
                            await _Sclient.DeleteModelAsync(new DeleteModelRequest
                            {
                                ModelName = deleted.CurrentModelName
                            });
                        }
                    });
                }
            }
            return RedirectToAction("Manage", new { LogInputID = deleted.LinkedLogInputID });
        }
        public async Task<IActionResult> Deploy(int TriggerID)
        {
            Trigger operatedEntity = await _logContext.AlertTriggers.FindAsync(TriggerID);
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
                if (operatedEntity.AlertTriggerType.Equals(AlertTriggerType.IPInsights))
                    createModelRequest.PrimaryContainer = new ContainerDefinition
                    {
                        Image = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com/ipinsights:1",
                        ModelDataUrl = "s3://" + _logContext.S3Buckets.Find(1).Name + "/" + operatedEntity.CurrentModelFileKey + "/" + operatedEntity.TrainingJobName + "/output/model.tar.gz"
                    };
                else if (operatedEntity.AlertTriggerType.Equals(AlertTriggerType.RCF))
                    createModelRequest.PrimaryContainer = new ContainerDefinition
                    {
                        Image = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com/randomcutforest:1",
                        ModelDataUrl = "s3://" + _logContext.S3Buckets.Find(1).Name + "/" + operatedEntity.CurrentModelFileKey + "/" + operatedEntity.TrainingJobName + "/output/model.tar.gz"
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
                _logContext.AlertTriggers.Update(operatedEntity);
                await _logContext.SaveChangesAsync();
                TempData["Alert"] = "Success";
                TempData["Message"] = "Inference Endpoint Deployment Jobs Created with ARN: " + createEndpointResponse.EndpointArn;
                return RedirectToAction("Manage", new { LogInputID = operatedEntity.LinkedLogInputID });
            }
            else
            {
                TempData["Alert"] = "Warning";
                TempData["Message"] = "Inference Endpoint Configuration and Inference Endpoint Deployment Jobs Failed to create";
                return RedirectToAction("Manage", new { LogInputID = operatedEntity.LinkedLogInputID });
            }
        }
        public async Task<IActionResult> Tune(int TriggerID)
        {
            Trigger AlertTrigger = _logContext.AlertTriggers.Find(TriggerID);
            LogInput retrieved = AlertTrigger.LinkedLogInput;
            string dbTableName = "dbo." + retrieved.LinkedS3Bucket.Name.Replace("-", "_");
            string condtionalOperator = string.Empty, Condtion = string.Empty, dateTimeField = string.Empty, num_entities = string.Empty;
            List<String> userNames = new List<string>();
            switch (AlertTrigger.CondtionType)
            {
                case "Equal":
                    condtionalOperator = "=";
                    Condtion = "'" + AlertTrigger.Condtion + "'";
                    break;
                case "NotEqual":
                    condtionalOperator = "!=";
                    Condtion = "'" + AlertTrigger.Condtion + "'";
                    break;
                case "Similar":
                    condtionalOperator = "LIKE";
                    Condtion = "'%" + AlertTrigger.Condtion + "%'";
                    break;
                case "NotSimilar":
                    condtionalOperator = "NOT LIKE";
                    Condtion = "'%" + AlertTrigger.Condtion + "%'";
                    break;
                case "LessThan":
                    condtionalOperator = "<";
                    Condtion = "'" + AlertTrigger.Condtion + "'";
                    break;
                case "LessOrEqualThan":
                    condtionalOperator = "<=";
                    Condtion = "'" + AlertTrigger.Condtion + "'";
                    break;
                case "MoreThan":
                    condtionalOperator = ">";
                    Condtion = "'" + AlertTrigger.Condtion + "'";
                    break;
                case "MoreOrEqualThan":
                    condtionalOperator = ">=";
                    Condtion = "'" + AlertTrigger.Condtion + "'";
                    break;
            }
            if (AlertTrigger.AlertTriggerType.Equals(AlertTriggerType.IPInsights))
            {
                List<GenericRecordHolder> records = new List<GenericRecordHolder>();
                using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
                {
                    connection.Open();
                    string sqlQuery1 = string.Empty, sqlQuery2 = string.Empty;
                    if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                    {
                        dateTimeField = "datetime";
                        sqlQuery1 = @"SELECT ROW_NUMBER() OVER(ORDER BY " + dateTimeField + " ASC), " + AlertTrigger.UserField + ", " + AlertTrigger.IPAddressField + " FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " AND response = 200;";
                    }
                    else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH))
                    {
                        if (AlertTrigger.IPAddressField.Equals(AlertTrigger.UserField))
                            sqlQuery1 = @"SELECT ROW_NUMBER() OVER(ORDER BY year, month, day, time) FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + ";";
                        else
                            return StatusCode(500);
                    }
                    else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SquidProxy))
                    {
                        dateTimeField = "timestamp";
                        sqlQuery1 = @"SELECT ROW_NUMBER() OVER(ORDER BY " + dateTimeField + " ASC), " + AlertTrigger.UserField + ", " + AlertTrigger.IPAddressField + " FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + ";";
                    }
                    using (SqlCommand cmd = new SqlCommand(sqlQuery1, connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                if (Convert.ToInt32(dr.GetValue(0)) < AlertTrigger.InferenceBookmark)
                                {
                                    if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH))
                                    {
                                        records.Add(new GenericRecordHolder
                                        {
                                            field1 = dr.GetValue(1).ToString().Substring(dr.GetValue(1).ToString().IndexOf("for") + 4, dr.GetValue(1).ToString().IndexOf("from") - 1),
                                            field2 = dr.GetValue(1).ToString().Substring(dr.GetValue(1).ToString().IndexOf("from") + 5, dr.GetValue(1).ToString().IndexOf("port") - 1),
                                        });
                                    }
                                    else
                                    {
                                        records.Add(new GenericRecordHolder
                                        {
                                            field1 = dr.GetValue(1).ToString(),
                                            field2 = dr.GetValue(2).ToString(),
                                        });
                                    }
                                    AlertTrigger.TrainingBookmark = Convert.ToInt32(dr.GetValue(0));
                                }
                            }
                        }
                    }
                    if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer) || _logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SquidProxy))
                        sqlQuery2 = @"SELECT count(DISTINCT " + AlertTrigger.UserField + ") FROM " + dbTableName + ";";
                    else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH))
                        sqlQuery2 = sqlQuery1;
                    using (SqlCommand cmd = new SqlCommand(sqlQuery2, connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH))
                                {
                                    userNames.Add(dr.GetValue(1).ToString().Substring(dr.GetValue(1).ToString().IndexOf("for") + 4, dr.GetValue(1).ToString().IndexOf("from") - 1));
                                }
                                else
                                    num_entities = (Convert.ToInt32(dr.GetValue(0)) * 2).ToString();
                            }
                        }
                        num_entities = userNames.Distinct().ToList().Count().ToString();
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
                string inputDataKey = retrieved.Name.Replace(" ", "-") + "/Input/ipinsights/data-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv";
                string modelFileKey = retrieved.Name.Replace(" ", "-") + "/Model";
                string checkpointKey = retrieved.Name.Replace(" ", "-") + "/Checkpoint";
                string jobName = retrieved.Name.Replace(" ", "-") + "-IPInsights-HyperParameter-Tuning-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                TransferUtility tu = new TransferUtility(_S3Client);
                await tu.UploadAsync(new TransferUtilityUploadRequest
                {
                    InputStream = new MemoryStream(memoryStream.ToArray()),
                    Key = inputDataKey,
                    BucketName = _logContext.S3Buckets.Find(1).Name
                });
                CreateHyperParameterTuningJobRequest createHyperParameterTuningJobRequest = new CreateHyperParameterTuningJobRequest
                {
                    HyperParameterTuningJobConfig = new HyperParameterTuningJobConfig
                    {
                        HyperParameterTuningJobObjective = new HyperParameterTuningJobObjective
                        {
                            MetricName = "validation:discriminator_auc",
                            Type = HyperParameterTuningJobObjectiveType.Maximize
                        },
                        ParameterRanges = new ParameterRanges
                        {
                            IntegerParameterRanges = new List<IntegerParameterRange>
                        {
                            new IntegerParameterRange
                            {
                                Name = "vector_dim",
                                MinValue = "64",
                                MaxValue = "1024",
                                ScalingType = HyperParameterScalingType.Auto
                            }
                        }
                        },
                        ResourceLimits = new ResourceLimits
                        {
                            MaxNumberOfTrainingJobs = 4,
                            MaxParallelTrainingJobs = 2
                        },
                        Strategy = HyperParameterTuningJobStrategyType.Bayesian,
                        TrainingJobEarlyStoppingType = TrainingJobEarlyStoppingType.Auto
                    },
                    HyperParameterTuningJobName = jobName,
                    Tags = new List<Tag>
                {
                    new Tag
                    {
                        Key = "Project",
                        Value = "OSPJ"
                    }
                },
                    TrainingJobDefinition = new HyperParameterTrainingJobDefinition
                    {
                        AlgorithmSpecification = new HyperParameterAlgorithmSpecification
                        {
                            TrainingImage = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com/ipinsights:1",
                            TrainingInputMode = TrainingInputMode.File
                        },
                        EnableManagedSpotTraining = true,
                        EnableInterContainerTrafficEncryption = false,
                        EnableNetworkIsolation = false,
                        StaticHyperParameters = new Dictionary<string, string>
                        {
                            { "random_negative_sampling_rate","5"},
                            { "num_entity_vectors", num_entities},
                            { "epochs", "5"},
                            { "learning_rate", "0.01"},
                            { "mini_batch_size", "1000"}
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
                            ContentType = "text/csv",
                            CompressionType = CompressionType.None,
                            RecordWrapperType = RecordWrapper.None
                        },
                        new Channel
                        {
                            ChannelName = "validation",
                            DataSource = new DataSource
                            {
                                S3DataSource = new S3DataSource
                                {
                                    S3DataDistributionType = S3DataDistribution.FullyReplicated,
                                    S3DataType = S3DataType.S3Prefix,
                                    S3Uri = "s3://" + _logContext.S3Buckets.Find(2).Name+ "/" + AlertTrigger.CurrentInputDataKey
                                }
                            },
                            ContentType = "text/csv",
                            CompressionType = CompressionType.None,
                            RecordWrapperType = RecordWrapper.None
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
                        CheckpointConfig = new CheckpointConfig
                        {
                            S3Uri = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + checkpointKey
                        }
                    }
                };
                CreateHyperParameterTuningJobResponse createHyperParameterTuningJobResponse = await _Sclient.CreateHyperParameterTuningJobAsync(createHyperParameterTuningJobRequest);
                if (createHyperParameterTuningJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    AlertTrigger.SagemakerStatus = SagemakerStatus.Tuning;
                    AlertTrigger.SagemakerErrorStage = SagemakerErrorStage.None;
                    if (AlertTrigger.DeprecatedInputDataKeys == null)
                        AlertTrigger.DeprecatedInputDataKeys = new string[] { AlertTrigger.CurrentInputDataKey };
                    else
                    {
                        string[] newDeprecatedInputDataKeys = new string[AlertTrigger.DeprecatedInputDataKeys.Length + 1];
                        newDeprecatedInputDataKeys[0] = AlertTrigger.CurrentInputDataKey;
                        Array.Copy(AlertTrigger.DeprecatedInputDataKeys, 0, newDeprecatedInputDataKeys, 1, AlertTrigger.DeprecatedInputDataKeys.Length);
                    }
                    AlertTrigger.CurrentInputDataKey = inputDataKey;
                    AlertTrigger.CurrentModelFileKey = modelFileKey;
                    AlertTrigger.CheckpointKey = checkpointKey;
                    AlertTrigger.HyperParameterTurningJobARN = createHyperParameterTuningJobResponse.HyperParameterTuningJobArn;
                    AlertTrigger.HyperParameterTurningJobName = jobName;
                    _logContext.AlertTriggers.Update(AlertTrigger);
                    TempData["Alert"] = "Success";
                    TempData["Message"] = "HyperParameter Tuning Job Created with ARN: " + createHyperParameterTuningJobResponse.HyperParameterTuningJobArn;
                }
                else
                {
                    TempData["Alert"] = "Warning";
                    TempData["Message"] = "HyperParameter Tuning Job Failed to create";
                }
            }
            else if (AlertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
            {
                List<WithTimestampRecordHolder> records = new List<WithTimestampRecordHolder>();
                using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
                {
                    connection.Open();
                    string sqlQuery = string.Empty;
                    if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                    {
                        dateTimeField = "datetime";
                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))),TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' ')), COUNT(" + AlertTrigger.CondtionalField + ") FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " GROUP BY DATEPART(YEAR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(MONTH, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(DAY, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(HOUR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), (DATEPART(MINUTE, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))) / 60), " + AlertTrigger.CondtionalField + ", TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))";
                    }
                    else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH))
                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY year, month, day, time), day,month,year,time, count(*) FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " BY year, month, day, time;";
                    else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SquidProxy))
                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY timestamp), timestamp, count(*) FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " BY timestamp;";
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                if (Convert.ToInt32(dr.GetValue(0)) < AlertTrigger.InferenceBookmark)
                                {
                                    if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                                    {
                                        records.Add(new WithTimestampRecordHolder
                                        {
                                            Timesetamp = Convert.ToDateTime(dr.GetValue(1).ToString()),
                                            field = dr.GetValue(2).ToString(),
                                        });
                                    }
                                    else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH))
                                    {
                                        int month = 0;
                                        switch (dr.GetValue(1).ToString())
                                        {
                                            case "Jan":
                                                month = 1;
                                                break;
                                            case "Feb":
                                                month = 2;
                                                break;
                                            case "Mar":
                                                month = 3;
                                                break;
                                            case "Apr":
                                                month = 4;
                                                break;
                                            case "May":
                                                month = 5;
                                                break;
                                            case "Jun":
                                                month = 6;
                                                break;
                                            case "July":
                                                month = 7;
                                                break;
                                            case "Aug":
                                                month = 8;
                                                break;
                                            case "Sep":
                                                month = 9;
                                                break;
                                            case "Oct":
                                                month = 10;
                                                break;
                                            case "Nov":
                                                month = 11;
                                                break;
                                            case "Dec":
                                                month = 12;
                                                break;
                                        }
                                        if (month < 10)
                                        {
                                            records.Add(new WithTimestampRecordHolder
                                            {
                                                Timesetamp = Convert.ToDateTime(dr.GetValue(1).ToString() + "/0" + month + dr.GetValue(2).ToString() + "/" + dr.GetValue(3).ToString() + " " + dr.GetValue(4).ToString()),
                                                field = dr.GetValue(2).ToString(),
                                            });
                                        }
                                        else
                                        {
                                            records.Add(new WithTimestampRecordHolder
                                            {
                                                Timesetamp = Convert.ToDateTime(dr.GetValue(1).ToString()),
                                                field = dr.GetValue(2).ToString(),
                                            });
                                        }
                                    }
                                    else if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SquidProxy))
                                    {
                                        records.Add(new WithTimestampRecordHolder
                                        {
                                            Timesetamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Convert.ToInt64(dr.GetValue(1))),
                                            field = dr.GetValue(2).ToString(),
                                        });
                                    }
                                    AlertTrigger.TrainingBookmark = Convert.ToInt32(dr.GetValue(0));
                                }
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
                string inputDataKey = retrieved.Name.Replace(" ", "-") + "/Input/randomcutforest/data-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv";
                string modelFileKey = retrieved.Name.Replace(" ", "-") + "/Model";
                string checkpointKey = retrieved.Name.Replace(" ", "-") + "/Checkpoint";
                string jobName = retrieved.Name.Replace(" ", "-") + "-RandomCutForest-HyperParameter-Tuning-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                TransferUtility tu = new TransferUtility(_S3Client);
                await tu.UploadAsync(new TransferUtilityUploadRequest
                {
                    InputStream = new MemoryStream(memoryStream.ToArray()),
                    Key = inputDataKey,
                    BucketName = _logContext.S3Buckets.Find(1).Name
                });
                CreateHyperParameterTuningJobRequest createHyperParameterTuningJobRequest = new CreateHyperParameterTuningJobRequest
                {
                    HyperParameterTuningJobConfig = new HyperParameterTuningJobConfig
                    {
                        HyperParameterTuningJobObjective = new HyperParameterTuningJobObjective
                        {
                            MetricName = "test:f1",
                            Type = HyperParameterTuningJobObjectiveType.Maximize
                        },
                        ParameterRanges = new ParameterRanges
                        {
                            IntegerParameterRanges = new List<IntegerParameterRange>
                        {
                            new IntegerParameterRange
                            {
                                Name = "num_samples_per_tree",
                                MinValue = "1",
                                MaxValue = "2048",
                                ScalingType = HyperParameterScalingType.Auto
                            }
                        }
                        },
                        ResourceLimits = new ResourceLimits
                        {
                            MaxNumberOfTrainingJobs = 4,
                            MaxParallelTrainingJobs = 2
                        },
                        Strategy = HyperParameterTuningJobStrategyType.Bayesian,
                        TrainingJobEarlyStoppingType = TrainingJobEarlyStoppingType.Auto
                    },
                    HyperParameterTuningJobName = jobName,
                    Tags = new List<Tag>
                {
                    new Tag
                    {
                        Key = "Project",
                        Value = "OSPJ"
                    }
                },
                    TrainingJobDefinition = new HyperParameterTrainingJobDefinition
                    {
                        AlgorithmSpecification = new HyperParameterAlgorithmSpecification
                        {
                            TrainingImage = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com/randomcutforest:1",
                            TrainingInputMode = TrainingInputMode.File
                        },
                        EnableManagedSpotTraining = true,
                        EnableInterContainerTrafficEncryption = false,
                        EnableNetworkIsolation = false,
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
                                    S3Uri = "s3://" + _logContext.S3Buckets.Find(2).Name+ "/" + inputDataKey.Substring(0,inputDataKey.Length - 28)
                                }
                            },
                            ContentType = "text/csv",
                            CompressionType = CompressionType.None,
                            RecordWrapperType = RecordWrapper.None
                        },
                        new Channel
                        {
                            ChannelName = "validation",
                            DataSource = new DataSource
                            {
                                S3DataSource = new S3DataSource
                                {
                                    S3DataDistributionType = S3DataDistribution.ShardedByS3Key,
                                    S3DataType = S3DataType.S3Prefix,
                                    S3Uri = "s3://" + _logContext.S3Buckets.Find(2).Name+ "/" + AlertTrigger.CurrentInputDataKey.Substring(0,inputDataKey.Length - 28)
                                }
                            },
                            ContentType = "text/csv",
                            CompressionType = CompressionType.None,
                            RecordWrapperType = RecordWrapper.None
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
                        CheckpointConfig = new CheckpointConfig
                        {
                            S3Uri = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + checkpointKey
                        }
                    }
                };
                CreateHyperParameterTuningJobResponse createHyperParameterTuningJobResponse = await _Sclient.CreateHyperParameterTuningJobAsync(createHyperParameterTuningJobRequest);
                if (createHyperParameterTuningJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    AlertTrigger.SagemakerStatus = SagemakerStatus.Tuning;
                    AlertTrigger.SagemakerErrorStage = SagemakerErrorStage.None;
                    if (AlertTrigger.DeprecatedInputDataKeys == null)
                        AlertTrigger.DeprecatedInputDataKeys = new string[] { AlertTrigger.CurrentInputDataKey };
                    else
                    {
                        string[] newDeprecatedInputDataKeys = new string[AlertTrigger.DeprecatedInputDataKeys.Length + 1];
                        newDeprecatedInputDataKeys[0] = AlertTrigger.CurrentInputDataKey;
                        Array.Copy(AlertTrigger.DeprecatedInputDataKeys, 0, newDeprecatedInputDataKeys, 1, AlertTrigger.DeprecatedInputDataKeys.Length);
                    }
                    AlertTrigger.CurrentInputDataKey = inputDataKey;
                    AlertTrigger.CurrentModelFileKey = modelFileKey;
                    AlertTrigger.CheckpointKey = checkpointKey;
                    AlertTrigger.HyperParameterTurningJobARN = createHyperParameterTuningJobResponse.HyperParameterTuningJobArn;
                    AlertTrigger.HyperParameterTurningJobName = jobName;
                    _logContext.AlertTriggers.Update(AlertTrigger);
                    TempData["Alert"] = "Success";
                    TempData["Message"] = "HyperParameter Tuning Job Created with ARN: " + createHyperParameterTuningJobResponse.HyperParameterTuningJobArn;
                }
                else
                {
                    TempData["Alert"] = "Warning";
                    TempData["Message"] = "HyperParameter Tuning Job Failed to create";
                }
            }
            await _logContext.SaveChangesAsync();
            return RedirectToAction("Manage", new { InputID = retrieved.ID });
        }
        public async Task<IActionResult> Read(int ID)
        {
            Alert a = _accountContext.Alerts.Find(ID);
            if (a == null)
                return StatusCode(404);
            else
            {
                a.Read = true;
                _accountContext.Alerts.Update(a);
                try
                {
                    await _accountContext.SaveChangesAsync();
                    return RedirectToAction("Index");
                }
                catch (DbUpdateException)
                {
                    return RedirectToAction("Index");
                }
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
    class GenericRecordHolder
    {
        public string field1 { get; set; }
        public string field2 { get; set; }
    }
    class WithTimestampRecordHolder
    {
        public DateTime Timesetamp { get; set; }
        public string field { get; set; }
    }
}
