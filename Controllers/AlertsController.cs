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
            string condtionalOperator = string.Empty, Condtion = string.Empty, dateTimeField = string.Empty;
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
            List<GenericRecordHolder> records = new List<GenericRecordHolder>();
            if (AlertTrigger.AlertTriggerType.Equals(AlertTriggerType.IPInsights))
            {
                using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
                {
                    connection.Open();
                    string sqlQuery = string.Empty;
                    if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY datetime ASC), " + AlertTrigger.UserField + ", " + AlertTrigger.IPAddressField + " FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " AND response = 200;";
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                AlertTrigger.InferenceBookmark = Convert.ToInt32(dr.GetValue(0));
                                records.Add(new GenericRecordHolder
                                {
                                    field1 = dr.GetValue(1).ToString(),
                                    field2 = dr.GetValue(2).ToString(),
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
                using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
                {
                    connection.Open();
                    string sqlQuery = string.Empty;
                    if (_logContext.LogInputs.Find(AlertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                        sqlQuery = @"SELECT TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, '" + dateTimeField + "')), COUNT(" + AlertTrigger.CondtionalField + ") FROM " + dbTableName + " WHERE " + AlertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " GROUP BY DATEPART(YEAR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' '))), DATEPART(MONTH, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' '))), DATEPART(DAY, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' '))), DATEPART(HOUR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' '))), (DATEPART(MINUTE, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' '))) / 60), " + AlertTrigger.CondtionalField + ", TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' ')) ORDER BY TRY_CONVERT(DATETIME, STUFF(SUBSTRING(datetime,0,18), 12, 1, ' '));";
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
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
}
