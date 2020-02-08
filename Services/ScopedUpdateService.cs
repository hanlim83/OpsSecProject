using Amazon.Glue;
using Amazon.Glue.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SageMaker;
using Amazon.SageMaker.Model;
using Amazon.SageMakerRuntime;
using Amazon.SageMakerRuntime.Model;
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

        public ScopedUpdateService(ILogger<ScopedSetupService> logger, LogContext logContext, IAmazonSageMaker SagemakerClient, IAmazonSageMakerRuntime SageMakerRuntimeClient, AccountContext accountContext)
        {
            _logger = logger;
            _logContext = logContext;
            _SagemakerClient = SagemakerClient;
            _SageMakerRuntimeClient = SageMakerRuntimeClient;
            _accountContext = accountContext;
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
                            else if (response.EndpointArn.Equals(alertTrigger.EndpointJobARN) && (response.EndpointStatus.Equals(EndpointStatus.RollingBack) || response.EndpointStatus.Equals(EndpointStatus.Deleting)))
                            {
                                alertTrigger.SagemakerStatus = SagemakerStatus.Reversing;
                                alertTrigger.SagemakerErrorStage = SagemakerErrorStage.None;
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
                        else if (alertTrigger.SagemakerStatus.Equals(SagemakerStatus.Transforming))
                        {
                            DescribeTransformJobResponse response = await _SagemakerClient.DescribeTransformJobAsync(new DescribeTransformJobRequest
                            {
                                TransformJobName = alertTrigger.BatchTransformJobName
                            });
                            if (response.TransformJobArn.Equals(alertTrigger.BatchTransformJobARN) && response.TransformJobStatus.Equals(TransformJobStatus.Completed))
                            {
                                alertTrigger.SagemakerStatus = SagemakerStatus.Ready;
                                alertTrigger.SagemakerErrorStage = SagemakerErrorStage.None;
                            }
                            else if (response.TransformJobArn.Equals(alertTrigger.BatchTransformJobARN) && response.TransformJobStatus.Equals(TransformJobStatus.InProgress))
                            {
                                alertTrigger.SagemakerStatus = SagemakerStatus.Transforming;
                                alertTrigger.SagemakerErrorStage = SagemakerErrorStage.None;
                            }
                            else if (response.TransformJobArn.Equals(alertTrigger.BatchTransformJobARN) && response.TransformJobStatus.Equals(TransformJobStatus.Failed))
                            {
                                alertTrigger.SagemakerStatus = SagemakerStatus.Error;
                                alertTrigger.SagemakerErrorStage = SagemakerErrorStage.Transforming;
                            }
                        }
                        else if (alertTrigger.SagemakerStatus.Equals(SagemakerStatus.Ready) || alertTrigger.SagemakerStatus.Equals(SagemakerStatus.None))
                        {
                            if (alertTrigger.AlertTriggerType.Equals(AlertTriggerType.IPInsights) || alertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
                            {
                                List<GenericRecordHolder> records = new List<GenericRecordHolder>();
                                using (SqlConnection connection = new SqlConnection(GetRdsConnectionString()))
                                {
                                    connection.Open();
                                    string dbTableName = "dbo." + alertTrigger.LinkedLogInput.LinkedS3Bucket.Name.Replace("-", "_");
                                    string sqlQuery = string.Empty, condtionalOperator = string.Empty, Condtion = string.Empty, dateTimeField = string.Empty;
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
                                    if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer) && alertTrigger.AlertTriggerType.Equals(AlertTriggerType.IPInsights))
                                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY datetime ASC), " + alertTrigger.UserField + ", " + alertTrigger.IPAddressField + " FROM " + dbTableName + " WHERE " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " AND response = 200;";
                                    else if (_logContext.LogInputs.Find(alertTrigger.LinkedLogInputID).LogInputCategory.Equals(LogInputCategory.ApacheWebServer) && alertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
                                    {
                                        dateTimeField = "datetime";
                                        sqlQuery = @"SELECT ROW_NUMBER() OVER(ORDER BY TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))),TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' ')), COUNT(" + alertTrigger.CondtionalField + ") FROM dbo.smartinsights_apache_web_logs WHERE " + alertTrigger.CondtionalField + " " + condtionalOperator + " " + Condtion + " GROUP BY DATEPART(YEAR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(MONTH, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(DAY, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), DATEPART(HOUR, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))), (DATEPART(MINUTE, TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))) / 60), " + alertTrigger.CondtionalField + ", TRY_CONVERT(DATETIME, STUFF(SUBSTRING(" + dateTimeField + ",0,18), 12, 1, ' '))";
                                    }
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
                                                    records.Add(new GenericRecordHolder
                                                    {
                                                        field1 = dr.GetValue(1).ToString(),
                                                        field2 = dr.GetValue(2).ToString(),
                                                    });
                                                }
                                            }
                                        }
                                    }
                                }
                                if (records.Count != 0)
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
                                            csvWriter.WriteRecords(records);
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
                                            foreach (IPInsightsPrediction prediction in predictions.Predictions)
                                            {

                                            }
                                        } else if (alertTrigger.AlertTriggerType.Equals(AlertTriggerType.RCF))
                                        {
                                            RandomCutForestPredictions predictions = JsonConvert.DeserializeObject<RandomCutForestPredictions>(json);
                                            foreach(RandomCutForestPrediction prediction in predictions.predictions)
                                            {

                                            }
                                        }

                                    }
                                }
                            }
                            else
                            {
                                
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
}
