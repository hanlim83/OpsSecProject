using Amazon.Glue;
using Amazon.Glue.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SageMaker;
using Amazon.SageMaker.Model;
using Amazon.SageMakerRuntime;
using Amazon.SageMakerRuntime.Model;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpsSecProject.Controllers;
using OpsSecProject.Data;
using OpsSecProject.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Tag = Amazon.SageMaker.Model.Tag;

namespace OpsSecProject.Services
{
    internal class ScopedUpdateService : IScopedUpdateService
    {
        private readonly ILogger _logger;
        private LogContext _logContext;
        private IAmazonSageMaker _SagemakerClient;
        private IAmazonSageMakerRuntime _SageMakerRuntimeClient;
        private AccountContext _accountContext;
        private IAmazonSimpleNotificationService _SNSClient;
        private IAmazonSimpleEmailService _SESClient;

        public ScopedUpdateService(ILogger<ScopedSetupService> logger, LogContext logContext, IAmazonSageMaker SagemakerClient, IAmazonSageMakerRuntime SageMakerRuntimeClient, AccountContext accountContext, IAmazonSimpleEmailService SESClient, IAmazonSimpleNotificationService SNSClient)
        {
            _logger = logger;
            _logContext = logContext;
            _SagemakerClient = SagemakerClient;
            _SageMakerRuntimeClient = SageMakerRuntimeClient;
            _accountContext = accountContext;
            _SESClient = SESClient;
            _SNSClient = SNSClient;
        }

        public async Task DoWorkAsync()
        {
            foreach (LogInput input in await _logContext.LogInputs.ToListAsync())
            {
                if (input.InitialIngest == true)
                {
                    foreach (Models.Trigger alertTrigger in input.LinkedSagemakerEntities)
                    {
                        if (alertTrigger.SagemakerStatus.Equals(SagemakerStatus.Training))
                        {
                            DescribeTrainingJobResponse describeTrainingJobResponse = await _SagemakerClient.DescribeTrainingJobAsync(new DescribeTrainingJobRequest
                            {
                                TrainingJobName = alertTrigger.TrainingJobName
                            });
                            if (describeTrainingJobResponse.TrainingJobArn.Equals(alertTrigger.TrainingJobARN) && describeTrainingJobResponse.TrainingJobStatus.Equals(TrainingJobStatus.Completed))
                            {
                                alertTrigger.SagemakerStatus = SagemakerStatus.Trained;
                                alertTrigger.SagemakerErrorStage = SagemakerErrorStage.None;
                                if (_accountContext.Settings.Where(s => s.LinkedUserID.Equals(alertTrigger.LinkedLogInput.LinkedUserID)).FirstOrDefault().AutoDeploy == true)
                                {
                                    alertTrigger.CurrentModelName = alertTrigger.LinkedLogInput.Name.Replace(" ", "-") + "Model" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                                    alertTrigger.EndpointConfigurationName = alertTrigger.LinkedLogInput.Name.Replace(" ", "-") + "EndpointConfig" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                                    alertTrigger.EndpointName = alertTrigger.LinkedLogInput.Name.Replace(" ", "-") + "EndpointName" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                                    CreateModelRequest createModelRequest = new CreateModelRequest
                                    {
                                        EnableNetworkIsolation = false,
                                        ModelName = alertTrigger.CurrentModelName,
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
                                    if (alertTrigger.AlertTriggerType.Equals(AlertTriggerType.IPInsights))
                                        createModelRequest.PrimaryContainer = new ContainerDefinition
                                        {
                                            Image = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com/ipinsights:1",
                                            ModelDataUrl = describeTrainingJobResponse.OutputDataConfig.S3OutputPath + "/" + describeTrainingJobResponse.TrainingJobName + "/output/model.tar.gz"
                                        };
                                    else if (alertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
                                        createModelRequest.PrimaryContainer = new ContainerDefinition
                                        {
                                            Image = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com/randomcutforest:1",
                                            ModelDataUrl = describeTrainingJobResponse.OutputDataConfig.S3OutputPath + "/" + describeTrainingJobResponse.TrainingJobName + "/output/model.tar.gz"
                                        };
                                    CreateModelResponse createModelResponse = await _SagemakerClient.CreateModelAsync(createModelRequest);
                                    if (createModelResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                                    {
                                        CreateEndpointConfigRequest createEndpointConfigRequest = new CreateEndpointConfigRequest
                                        {
                                            EndpointConfigName = alertTrigger.EndpointConfigurationName,
                                            ProductionVariants = new List<ProductionVariant>
                                            {
                                                new ProductionVariant
                                                {
                                                    VariantName = alertTrigger.LinkedLogInput.Name.Replace(" ", "-") + "ProductionVariant" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                                                    ModelName = alertTrigger.CurrentModelName,
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
                                        CreateEndpointConfigResponse createEndpointConfigResponse = await _SagemakerClient.CreateEndpointConfigAsync(createEndpointConfigRequest);
                                        if (createEndpointConfigResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                                        {
                                            alertTrigger.EndpointConfigurationARN = createEndpointConfigResponse.EndpointConfigArn;
                                            CreateEndpointRequest createEndpointRequest = new CreateEndpointRequest
                                            {
                                                EndpointConfigName = alertTrigger.EndpointConfigurationName,
                                                EndpointName = alertTrigger.EndpointName,
                                                Tags = new List<Tag>
                                                {
                                                    new Tag
                                                    {
                                                        Key = "Project",
                                                        Value = "OSPJ"
                                                    }
                                                }
                                            };
                                            CreateEndpointResponse createEndpointResponse = await _SagemakerClient.CreateEndpointAsync(createEndpointRequest);
                                            if (createEndpointResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                                            {
                                                alertTrigger.EndpointJobARN = createEndpointResponse.EndpointArn;
                                                alertTrigger.SagemakerStatus = SagemakerStatus.Deploying;
                                                alertTrigger.SagemakerErrorStage = SagemakerErrorStage.None;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    _accountContext.Alerts.Add(new Alert
                                    {
                                        AlertType = AlertType.SageMakerTrained,
                                        ExternalNotificationType = ExternalNotificationType.NONE,
                                        LinkedUserID = alertTrigger.LinkedLogInput.LinkedUserID,
                                        TimeStamp = DateTime.Now,
                                        Message = "A Machine Learning Model for " + alertTrigger.LinkedLogInput.Name + " has been trained sucessfully!"
                                    });
                                }
                            }
                            else if (describeTrainingJobResponse.TrainingJobArn.Equals(alertTrigger.TrainingJobARN) && describeTrainingJobResponse.TrainingJobStatus.Equals(TrainingJobStatus.InProgress))
                                alertTrigger.SagemakerStatus = SagemakerStatus.Training;
                            else if (describeTrainingJobResponse.TrainingJobArn.Equals(alertTrigger.TrainingJobARN) && describeTrainingJobResponse.TrainingJobStatus.Equals(TrainingJobStatus.Failed))
                            {
                                alertTrigger.SagemakerStatus = SagemakerStatus.Error;
                                alertTrigger.SagemakerErrorStage = SagemakerErrorStage.Training;
                            }
                        }
                        else if (alertTrigger.SagemakerStatus.Equals(SagemakerStatus.Deploying))
                        {
                            DescribeEndpointResponse response = await _SagemakerClient.DescribeEndpointAsync(new DescribeEndpointRequest
                            {
                                EndpointName = alertTrigger.EndpointName
                            });
                            if (response.EndpointArn.Equals(alertTrigger.EndpointJobARN) && response.EndpointStatus.Equals(EndpointStatus.InService))
                            {
                                alertTrigger.SagemakerStatus = SagemakerStatus.Ready;
                                alertTrigger.SagemakerErrorStage = SagemakerErrorStage.None;
                                _accountContext.Alerts.Add(new Alert
                                {
                                    AlertType = AlertType.SageMakerDeployed,
                                    ExternalNotificationType = ExternalNotificationType.NONE,
                                    LinkedUserID = alertTrigger.LinkedLogInput.LinkedUserID,
                                    TimeStamp = DateTime.Now,
                                    Message = "A Machine Learning Model for " + alertTrigger.LinkedLogInput.Name + " has been deployed sucessfully!"
                                });
                            }
                            else if (response.EndpointArn.Equals(alertTrigger.EndpointJobARN) && (response.EndpointStatus.Equals(EndpointStatus.OutOfService) || response.EndpointStatus.Equals(EndpointStatus.Failed)))
                            {
                                alertTrigger.SagemakerStatus = SagemakerStatus.Error;
                                alertTrigger.SagemakerErrorStage = SagemakerErrorStage.Deployment;
                            }
                            else if (response.EndpointArn.Equals(alertTrigger.EndpointJobARN))
                            {
                                alertTrigger.SagemakerStatus = SagemakerStatus.Deploying;
                                alertTrigger.SagemakerErrorStage = SagemakerErrorStage.None;
                            }
                        }
                        else if (alertTrigger.SagemakerStatus.Equals(SagemakerStatus.Tuning))
                        {
                            DescribeHyperParameterTuningJobResponse response = await _SagemakerClient.DescribeHyperParameterTuningJobAsync(new DescribeHyperParameterTuningJobRequest
                            {
                                HyperParameterTuningJobName = alertTrigger.HyperParameterTurningJobName
                            });
                            if (response.HyperParameterTuningJobArn.Equals(alertTrigger.HyperParameterTurningJobARN) && response.HyperParameterTuningJobStatus.Equals(HyperParameterTuningJobStatus.Completed))
                            {
                                alertTrigger.SagemakerStatus = SagemakerStatus.Trained;
                                alertTrigger.SagemakerErrorStage = SagemakerErrorStage.None;
                            }
                            else if (response.HyperParameterTuningJobArn.Equals(alertTrigger.HyperParameterTurningJobARN) && response.HyperParameterTuningJobStatus.Equals(HyperParameterTuningJobStatus.InProgress))
                            {
                                alertTrigger.SagemakerStatus = SagemakerStatus.Tuning;
                                alertTrigger.SagemakerErrorStage = SagemakerErrorStage.None;
                            }
                            else if (response.HyperParameterTuningJobArn.Equals(alertTrigger.HyperParameterTurningJobARN) && response.HyperParameterTuningJobStatus.Equals(HyperParameterTuningJobStatus.Failed))
                            {
                                alertTrigger.SagemakerStatus = SagemakerStatus.Error;
                                alertTrigger.SagemakerErrorStage = SagemakerErrorStage.Tuning;
                            }
                        }
                        else if (alertTrigger.SagemakerStatus.Equals(SagemakerStatus.Ready) || alertTrigger.SagemakerStatus.Equals(SagemakerStatus.None))
                        {
                            string dateTimeField = string.Empty, dbTableName = "dbo." + alertTrigger.LinkedLogInput.LinkedS3Bucket.Name.Replace("-", "_"), sqlQuery = string.Empty, condtionalOperator = string.Empty, Condtion = string.Empty;
                            bool alert = false;
                            switch (alertTrigger.CondtionType)
                            {
                                case "Equal":
                                    condtionalOperator = "=";
                                    Condtion = "'" + alertTrigger.Condtion + "'";
                                    break;
                                case "NotEqual":
                                    condtionalOperator = "!=";
                                    Condtion = "'" + alertTrigger.Condtion + "'";
                                    break;
                                case "Similar":
                                    condtionalOperator = "LIKE";
                                    Condtion = "'%" + alertTrigger.Condtion + "%'";
                                    break;
                                case "NotSimilar":
                                    condtionalOperator = "NOT LIKE";
                                    Condtion = "'%" + alertTrigger.Condtion + "%'";
                                    break;
                                case "LessThan":
                                    condtionalOperator = "<";
                                    Condtion = "'" + alertTrigger.Condtion + "'";
                                    break;
                                case "LessOrEqualThan":
                                    condtionalOperator = "<=";
                                    Condtion = "'" + alertTrigger.Condtion + "'";
                                    break;
                                case "MoreThan":
                                    condtionalOperator = ">";
                                    Condtion = "'" + alertTrigger.Condtion + "'";
                                    break;
                                case "MoreOrEqualThan":
                                    condtionalOperator = ">=";
                                    Condtion = "'" + alertTrigger.Condtion + "'";
                                    break;
                            }
                            if (alertTrigger.AlertTriggerType.Equals(AlertTriggerType.IPInsights) || alertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
                            {
                                List<GenericRecordHolder> recordsWithoutTimestamp = new List<GenericRecordHolder>();
                                List<WithTimestampRecordHolder> recordsWithTimestamp = new List<WithTimestampRecordHolder>();
                                using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
                                {
                                    connection.Open();
                                    if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer) && alertTrigger.AlertTriggerType.Equals(AlertTriggerType.IPInsights))
                                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY datetime ASC), " + alertTrigger.UserField + ", " + alertTrigger.IPAddressField + " FROM " + dbTableName + " WHERE " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " AND response = 200;";
                                    else if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer) && alertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
                                    {
                                        dateTimeField = "datetime";
                                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))),TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' ')), COUNT(" + alertTrigger.CondtionalField + ") FROM " + dbTableName + " WHERE " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " GROUP BY DATEPART(YEAR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(MONTH, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(DAY, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(HOUR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), (DATEPART(MINUTE, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))) / 60), " + alertTrigger.CondtionalField + ", TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '));";
                                    }
                                    else if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH) && alertTrigger.AlertTriggerType.Equals(AlertTriggerType.IPInsights))
                                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY year, month, day, time)" + alertTrigger.IPAddressField + " FROM " + dbTableName + " WHERE " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + ";";
                                    else if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH) && alertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
                                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY year, month, day, time), day,month,year,time, count(*) FROM " + dbTableName + " WHERE " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " BY year, month, day, time;";
                                    else if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SquidProxy) && alertTrigger.AlertTriggerType.Equals(AlertTriggerType.IPInsights))
                                    {
                                        dateTimeField = "timestamp";
                                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY " + dateTimeField + " ASC), " + alertTrigger.UserField + ", " + alertTrigger.IPAddressField + " FROM " + dbTableName + " WHERE " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + ";";
                                    }
                                    else if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SquidProxy) && alertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
                                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY timestamp), timestamp, count(*) FROM " + dbTableName + " WHERE " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " BY timestamp;";
                                    using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                                    {
                                        cmd.CommandTimeout = 0;
                                        using (SqlDataReader dr = cmd.ExecuteReader())
                                        {
                                            while (dr.Read())
                                            {
                                                if (Convert.ToInt32(dr.GetValue(0)) > alertTrigger.InferenceBookmark)
                                                {
                                                    alertTrigger.InferenceBookmark = Convert.ToInt32(dr.GetValue(0));
                                                    if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH) && alertTrigger.AlertTriggerType.Equals(AlertTriggerType.IPInsights))
                                                    {
                                                        recordsWithoutTimestamp.Add(new GenericRecordHolder
                                                        {
                                                            field1 = dr.GetValue(1).ToString().Substring(dr.GetValue(1).ToString().IndexOf("for") + 4, dr.GetValue(1).ToString().IndexOf("from") - 1),
                                                            field2 = dr.GetValue(1).ToString().Substring(dr.GetValue(1).ToString().IndexOf("from") + 5, dr.GetValue(1).ToString().IndexOf("port") - 1),
                                                        });
                                                    }
                                                    else if (alertTrigger.AlertTriggerType.Equals(AlertTriggerType.IPInsights))
                                                    {
                                                        recordsWithoutTimestamp.Add(new GenericRecordHolder
                                                        {
                                                            field1 = dr.GetValue(1).ToString(),
                                                            field2 = dr.GetValue(2).ToString(),
                                                        });
                                                    }
                                                    else if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer) && alertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
                                                    {
                                                        recordsWithTimestamp.Add(new WithTimestampRecordHolder
                                                        {
                                                            Timesetamp = Convert.ToDateTime(dr.GetValue(1).ToString()),
                                                            field = dr.GetValue(2).ToString(),
                                                        });
                                                    }
                                                    else if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH) && alertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
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
                                                            recordsWithTimestamp.Add(new WithTimestampRecordHolder
                                                            {
                                                                Timesetamp = Convert.ToDateTime(dr.GetValue(1).ToString() + "/0" + month + dr.GetValue(2).ToString() + "/" + dr.GetValue(3).ToString() + " " + dr.GetValue(4).ToString()),
                                                                field = dr.GetValue(2).ToString(),
                                                            });
                                                        }
                                                        else
                                                        {
                                                            recordsWithTimestamp.Add(new WithTimestampRecordHolder
                                                            {
                                                                Timesetamp = Convert.ToDateTime(dr.GetValue(1).ToString() + "/" + month + dr.GetValue(2).ToString() + "/" + dr.GetValue(3).ToString() + " " + dr.GetValue(4).ToString()),
                                                                field = dr.GetValue(2).ToString(),
                                                            });
                                                        }
                                                    }
                                                    else if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SquidProxy) && alertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
                                                    {
                                                        recordsWithTimestamp.Add(new WithTimestampRecordHolder
                                                        {
                                                            Timesetamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Convert.ToInt64(dr.GetValue(1))),
                                                            field = dr.GetValue(2).ToString(),
                                                        });
                                                    }
                                                    else
                                                    {
                                                        recordsWithTimestamp.Add(new WithTimestampRecordHolder
                                                        {
                                                            Timesetamp = DateTime.Parse(dr.GetValue(1).ToString(), null, DateTimeStyles.RoundtripKind),
                                                            field = dr.GetValue(2).ToString(),
                                                        });
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                if (recordsWithoutTimestamp.Count != 0 || recordsWithTimestamp.Count != 0)
                                {
                                    MemoryStream memoryStream = new MemoryStream();
                                    CsvConfiguration config = new CsvConfiguration(CultureInfo.CurrentCulture)
                                    {
                                        HasHeaderRecord = false
                                    };
                                    using (var streamWriter = new StreamWriter(memoryStream))
                                    {
                                        using (var csvWriter = new CsvWriter(streamWriter, config))
                                        {
                                            if (recordsWithTimestamp.Count == 0)
                                                csvWriter.WriteRecords(recordsWithoutTimestamp);
                                            else if (recordsWithoutTimestamp.Count == 0)
                                                csvWriter.WriteRecords(recordsWithTimestamp);
                                        }
                                    }
                                    InvokeEndpointResponse invokeEndpointResponse = await _SageMakerRuntimeClient.InvokeEndpointAsync(new InvokeEndpointRequest
                                    {
                                        Accept = "application/json",
                                        ContentType = "text/csv",
                                        EndpointName = alertTrigger.EndpointName,
                                        Body = new MemoryStream(memoryStream.ToArray())
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
                                        if (alertTrigger.AlertTriggerType.Equals(AlertTriggerType.IPInsights))
                                        {
                                            IPInsightsPredictions predictions = JsonConvert.DeserializeObject<IPInsightsPredictions>(json);
                                            for (int i = 0; i < predictions.Predictions.Count; i++)
                                            {
                                                IPInsightsPrediction prediction = predictions.Predictions[i];
                                                if (prediction.Dot_product < -1 || prediction.Dot_product >= 0)
                                                {
                                                    alert = true;
                                                    int fieldsCount = 0, counter = 0;
                                                    QuestionableEvent qe = new QuestionableEvent
                                                    {
                                                        IPAddressField = recordsWithoutTimestamp[i].field2,
                                                        UserField = recordsWithoutTimestamp[i].field1,
                                                        FullEventData = ""
                                                    };
                                                    using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
                                                    {
                                                        connection.Open();
                                                        using (SqlCommand cmd = new SqlCommand(@"SELECT count(*) FROM sys.columns WHERE object_id = OBJECT_ID(@TableName);", connection))
                                                        {
                                                            cmd.CommandTimeout = 0;
                                                            cmd.Parameters.AddWithValue("@TableName", dbTableName);
                                                            using (SqlDataReader dr = cmd.ExecuteReader())
                                                            {
                                                                while (dr.Read())
                                                                {
                                                                    fieldsCount = Convert.ToInt32(dr.GetValue(0));
                                                                }
                                                            }
                                                        }
                                                        using (SqlCommand cmd = new SqlCommand(@"SELECT * FROM " + dbTableName + " WHERE " + alertTrigger.UserField + " = " + qe.UserField + " AND " + alertTrigger.IPAddressField + " = " + qe.IPAddressField + " AND " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + ";", connection))
                                                        {
                                                            cmd.CommandTimeout = 0;
                                                            using (SqlDataReader dr = cmd.ExecuteReader())
                                                            {
                                                                while (dr.Read())
                                                                {
                                                                    if (fieldsCount < counter)
                                                                        qe.FullEventData = qe.FullEventData + dr.GetValue(counter).ToString() + " ";
                                                                }
                                                            }
                                                        }
                                                        if (string.IsNullOrEmpty(dateTimeField))
                                                        {
                                                            using (SqlCommand cmd = new SqlCommand(@"SELECT day,month,year,time FROM " + dbTableName + " WHERE " + alertTrigger.UserField + " = " + qe.UserField + " AND " + alertTrigger.IPAddressField + " = " + qe.IPAddressField + " AND " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + ";", connection))
                                                            {
                                                                cmd.CommandTimeout = 0;
                                                                using (SqlDataReader dr = cmd.ExecuteReader())
                                                                {
                                                                    while (dr.Read())
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
                                                                            qe.EventTimestamp = Convert.ToDateTime(dr.GetValue(0).ToString() + "/0" + month + dr.GetValue(1).ToString() + "/" + dr.GetValue(2).ToString() + " " + dr.GetValue(3).ToString());
                                                                        else
                                                                            qe.EventTimestamp = Convert.ToDateTime(dr.GetValue(0).ToString() + "/" + month + dr.GetValue(1).ToString() + "/" + dr.GetValue(2).ToString() + " " + dr.GetValue(3).ToString());
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            using (SqlCommand cmd = new SqlCommand(@"SELECT " + dateTimeField + " FROM " + dbTableName + " WHERE " + alertTrigger.UserField + " = " + qe.UserField + " AND " + alertTrigger.IPAddressField + " = " + qe.IPAddressField + " AND " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + ";", connection))
                                                            {
                                                                cmd.CommandTimeout = 0;
                                                                using (SqlDataReader dr = cmd.ExecuteReader())
                                                                {
                                                                    while (dr.Read())
                                                                    {
                                                                        if (alertTrigger.LinkedLogInput.LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                                                                            qe.EventTimestamp = Convert.ToDateTime(dr.GetValue(0).ToString());
                                                                        else if (alertTrigger.LinkedLogInput.LogInputCategory.Equals(LogInputCategory.SquidProxy))
                                                                            qe.EventTimestamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Convert.ToInt64(dr.GetValue(0)));
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                    foreach (User u in _accountContext.Users.ToList())
                                                    {
                                                        if (qe.UserField.Equals(u.Username))
                                                        {
                                                            qe.ReviewUserID = u.ID;
                                                            Alert a = new Alert
                                                            {
                                                                TimeStamp = DateTime.Now,
                                                                LinkedUserID = alertTrigger.LinkedLogInput.LinkedUserID,
                                                                LinkedObjectID = alertTrigger.LinkedLogInput.ID,
                                                                AlertType = AlertType.ReviewQuestionableEvent,
                                                                Message = "There is a event that needs your review"
                                                            };
                                                            if (u.LinkedSettings.CommmuicationOptions.Equals(CommmuicationOptions.EMAIL) && u.VerifiedEmailAddress)
                                                            {
                                                                SendEmailRequest SESrequest = new SendEmailRequest
                                                                {
                                                                    Source = Environment.GetEnvironmentVariable("SES_EMAIL_FROM-ADDRESS"),
                                                                    Destination = new Destination
                                                                    {
                                                                        ToAddresses = new List<string>
                        {
                            u.EmailAddress
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
                                                                                Data = "Hi " + u.Name + ",\r\n\nAn event needs your review.\r\nPlease login to SmartInsights to view more details.\r\n\n\nThis is a computer-generated email, please do not reply"
                                                                            }
                                                                        }
                                                                    }
                                                                };
                                                                SendEmailResponse response = await _SESClient.SendEmailAsync(SESrequest);
                                                                if (response.HttpStatusCode != HttpStatusCode.OK)
                                                                    a.ExternalNotificationType = ExternalNotificationType.EMAIL;
                                                            }
                                                            else if (u.LinkedSettings.CommmuicationOptions.Equals(CommmuicationOptions.SMS) && u.VerifiedPhoneNumber)
                                                            {
                                                                PublishRequest SNSrequest = new PublishRequest
                                                                {
                                                                    Message = "An event needs your review. Login to view more details.",
                                                                    PhoneNumber = "+65" + u.PhoneNumber
                                                                };
                                                                SNSrequest.MessageAttributes["AWS.SNS.SMS.SenderID"] = new MessageAttributeValue { StringValue = "SmartIS", DataType = "String" };
                                                                SNSrequest.MessageAttributes["AWS.SNS.SMS.SMSType"] = new MessageAttributeValue { StringValue = "Transactional", DataType = "String" };
                                                                PublishResponse response = await _SNSClient.PublishAsync(SNSrequest);
                                                                if (response.HttpStatusCode != HttpStatusCode.OK)
                                                                    a.ExternalNotificationType = ExternalNotificationType.SMS;
                                                            }
                                                            _accountContext.Alerts.Add(a);
                                                        }
                                                    }
                                                    _logContext.QuestionableEvents.Add(qe);
                                                    break;
                                                }
                                            }
                                        }
                                        else if (alertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
                                        {
                                            RandomCutForestPredictions predictions = JsonConvert.DeserializeObject<RandomCutForestPredictions>(json);
                                            foreach (RandomCutForestPrediction prediction in predictions.predictions)
                                            {
                                                if (prediction.score >= 2)
                                                {
                                                    alert = true;
                                                    break;
                                                }
                                            }
                                        }

                                    }
                                }
                            }
                            else
                            {
                                List<GenericCountHolder> records = new List<GenericCountHolder>();
                                using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
                                {
                                    connection.Open();
                                    if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer))
                                    {
                                        dateTimeField = "datetime";
                                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), COUNT(" + alertTrigger.CondtionalField + ") FROM " + dbTableName + " WHERE " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " GROUP BY DATEPART(YEAR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(MONTH, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(DAY, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(HOUR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), (DATEPART(MINUTE, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))) / 60), " + alertTrigger.CondtionalField + ", TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))";
                                    }
                                    else if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SSH))
                                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY year, month, day, time),count(*) FROM " + dbTableName + " WHERE " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " BY year, month, day, time;";
                                    else if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.SquidProxy))
                                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY timestamp), count(*) FROM " + dbTableName + " WHERE " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " BY timestamp;";
                                    using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                                    {
                                        cmd.CommandTimeout = 0;
                                        using (SqlDataReader dr = cmd.ExecuteReader())
                                        {
                                            while (dr.Read())
                                            {
                                                records.Add(new GenericCountHolder
                                                {
                                                    count = Convert.ToInt32(dr.GetValue(1).ToString())
                                                });
                                            }
                                        }
                                    }
                                }
                                foreach (GenericCountHolder r in records)
                                {
                                    if (alertTrigger.CondtionType.Equals("Equal"))
                                    {
                                        if (r.count == Convert.ToInt32(alertTrigger.Condtion))
                                        {
                                            alert = true;
                                            break;
                                        }
                                    }
                                    else if (alertTrigger.CondtionType.Equals("NotEqual"))
                                    {
                                        if (r.count != Convert.ToInt32(alertTrigger.Condtion))
                                        {
                                            alert = true;
                                            break;
                                        }
                                    }
                                    else if (alertTrigger.CondtionType.Equals("LessThan"))
                                    {
                                        if (r.count < Convert.ToInt32(alertTrigger.Condtion))
                                        {
                                            alert = true;
                                            break;
                                        }
                                    }
                                    else if (alertTrigger.CondtionType.Equals("LessOrEqualThan"))
                                    {
                                        if (r.count <= Convert.ToInt32(alertTrigger.Condtion))
                                        {
                                            alert = true;
                                            break;
                                        }
                                    }
                                    else if (alertTrigger.CondtionType.Equals("MoreThan"))
                                    {
                                        if (r.count > Convert.ToInt32(alertTrigger.Condtion))
                                        {
                                            alert = true;
                                            break;
                                        }
                                    }
                                    else if (alertTrigger.CondtionType.Equals("MoreOrEqualThan"))
                                    {
                                        if (r.count >= Convert.ToInt32(alertTrigger.Condtion))
                                        {
                                            alert = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (alert == true)
                            {
                                Alert a = new Alert
                                {
                                    TimeStamp = DateTime.Now,
                                    LinkedUserID = alertTrigger.LinkedLogInput.LinkedUserID,
                                    LinkedObjectID = alertTrigger.LinkedLogInput.ID
                                };
                                if (alertTrigger.AlertTriggerType.Equals(AlertTriggerType.CountByTimeStamp) || alertTrigger.AlertTriggerType.Equals(AlertTriggerType.CountAlone))
                                {
                                    a.Message = "The Metric Alert " + alertTrigger.Name + " has been triggered for " + alertTrigger.LinkedLogInput.Name + "!";
                                    a.AlertType = AlertType.MetricExceeded;
                                }
                                else
                                {
                                    a.Message = "The Machine Learning Model " + alertTrigger.Name + " has predicted suspicous actvitiy for " + alertTrigger.LinkedLogInput.Name + "!";
                                    a.AlertType = AlertType.SageMakerPredictionTriggered;
                                }
                                User u = _accountContext.Users.Find(alertTrigger.LinkedLogInput.LinkedUserID);
                                if (u.LinkedSettings.CommmuicationOptions.Equals(CommmuicationOptions.EMAIL) && u.VerifiedEmailAddress)
                                {
                                    SendEmailRequest SESrequest = new SendEmailRequest
                                    {
                                        Source = Environment.GetEnvironmentVariable("SES_EMAIL_FROM-ADDRESS"),
                                        Destination = new Destination
                                        {
                                            ToAddresses = new List<string>
                        {
                            u.EmailAddress
                        }
                                        },
                                        Message = new Message
                                        {
                                            Subject = new Content("Alert Trigger Activated"),
                                            Body = new Body
                                            {
                                                Text = new Content
                                                {
                                                    Charset = "UTF-8",
                                                    Data = "Hi " + u.Name + ",\r\n\nAn alert trigger has been activated for one of your log inputs.\r\nPlease login to SmartInsights to view more details.\r\n\n\nThis is a computer-generated email, please do not reply"
                                                }
                                            }
                                        }
                                    };
                                    SendEmailResponse response = await _SESClient.SendEmailAsync(SESrequest);
                                    if (response.HttpStatusCode != HttpStatusCode.OK)
                                        a.ExternalNotificationType = ExternalNotificationType.EMAIL;
                                }
                                else if (u.LinkedSettings.CommmuicationOptions.Equals(CommmuicationOptions.SMS) && u.VerifiedPhoneNumber)
                                {
                                    PublishRequest SNSrequest = new PublishRequest
                                    {
                                        Message = "An alert trigger has been activated for one of your log inputs. Login to view more details.",
                                        PhoneNumber = "+65" + u.PhoneNumber
                                    };
                                    SNSrequest.MessageAttributes["AWS.SNS.SMS.SenderID"] = new MessageAttributeValue { StringValue = "SmartIS", DataType = "String" };
                                    SNSrequest.MessageAttributes["AWS.SNS.SMS.SMSType"] = new MessageAttributeValue { StringValue = "Transactional", DataType = "String" };
                                    PublishResponse response = await _SNSClient.PublishAsync(SNSrequest);
                                    if (response.HttpStatusCode != HttpStatusCode.OK)
                                        a.ExternalNotificationType = ExternalNotificationType.SMS;
                                }
                                _accountContext.Alerts.Add(a);
                            }
                        }
                        _logContext.AlertTriggers.Update(alertTrigger);
                    }
                }
            }
            await _logContext.SaveChangesAsync();
            await _accountContext.SaveChangesAsync();
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
    class GenericCountHolder
    {
        public int count { get; set; }
    }
}
