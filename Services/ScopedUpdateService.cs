using Amazon.Glue;
using Amazon.Glue.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SageMaker;
using Amazon.SageMaker.Model;
using Amazon.SageMakerRuntime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsSecProject.Data;
using OpsSecProject.Models;
using System;
using System.Collections.Generic;
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
                    foreach (SagemakerConsolidatedEntity sagemaker in input.LinkedSagemakerEntities)
                    {
                        if (sagemaker.SagemakerStatus.Equals(SagemakerStatus.Training))
                        {
                            DescribeTrainingJobResponse describeTrainingJobResponse = await _SagemakerClient.DescribeTrainingJobAsync(new DescribeTrainingJobRequest
                            {
                                TrainingJobName = sagemaker.TrainingJobName
                            });
                            if (describeTrainingJobResponse.TrainingJobArn.Equals(sagemaker.TrainingJobARN) && describeTrainingJobResponse.TrainingJobStatus.Equals(TrainingJobStatus.Completed))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Trained;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.None;
                                if (_accountContext.Settings.Where(s => s.LinkedUserID.Equals(sagemaker.LinkedLogInput.LinkedUserID)).FirstOrDefault().AutoDeploy == true)
                                {
                                    sagemaker.CurrentModelName = sagemaker.LinkedLogInput.Name.Replace(" ", "-") + "Model" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                                    sagemaker.EndpointConfigurationName = sagemaker.LinkedLogInput.Name.Replace(" ", "-") + "EndpointConfig" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                                    sagemaker.EndpointName = sagemaker.LinkedLogInput.Name.Replace(" ", "-") + "EndpointName" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                                    CreateModelRequest createModelRequest = new CreateModelRequest
                                    {
                                        EnableNetworkIsolation = false,
                                        ModelName = sagemaker.CurrentModelName,
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
                                    if (sagemaker.SagemakerAlgorithm.Equals(SagemakerAlgorithm.IP_Insights))
                                        createModelRequest.PrimaryContainer = new ContainerDefinition
                                        {
                                            Image = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com/ipinsights:1",
                                            ModelDataUrl = describeTrainingJobResponse.OutputDataConfig.S3OutputPath + "/" + describeTrainingJobResponse.TrainingJobName + "/output/model.tar.gz"
                                        };
                                    else if (sagemaker.SagemakerAlgorithm.Equals(SagemakerAlgorithm.Random_Cut_Forest))
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
                                            EndpointConfigName = sagemaker.EndpointConfigurationName,
                                            ProductionVariants = new List<ProductionVariant>
                                            {
                                                new ProductionVariant
                                                {
                                                    VariantName = sagemaker.LinkedLogInput.Name.Replace(" ", "-") + "ProductionVariant" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                                                    ModelName = sagemaker.CurrentModelName,
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
                                            sagemaker.EndpointConfigurationARN = createEndpointConfigResponse.EndpointConfigArn;
                                            CreateEndpointRequest createEndpointRequest = new CreateEndpointRequest
                                            {
                                                EndpointConfigName = sagemaker.EndpointConfigurationName,
                                                EndpointName = sagemaker.EndpointName,
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
                                                sagemaker.EndpointJobARN = createEndpointResponse.EndpointArn;
                                                sagemaker.SagemakerStatus = SagemakerStatus.Deploying;
                                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.None;
                                            }
                                        }
                                    }
                                } else
                                {
                                    _accountContext.Alerts.Add(new Alert
                                    {
                                        AlertType = AlertType.SageMakerTrained,
                                        ExternalNotificationType = ExternalNotificationType.NONE,
                                        LinkedUserID = sagemaker.LinkedLogInput.LinkedUserID,
                                        TimeStamp = DateTime.Now,
                                        Message = "A Machine Learning Model for "+sagemaker.LinkedLogInput.Name+" has been trained sucessfully!"
                                    });
                                }
                            }
                            else if (describeTrainingJobResponse.TrainingJobArn.Equals(sagemaker.TrainingJobARN) && describeTrainingJobResponse.TrainingJobStatus.Equals(TrainingJobStatus.InProgress))
                                sagemaker.SagemakerStatus = SagemakerStatus.Training;
                            else if (describeTrainingJobResponse.TrainingJobArn.Equals(sagemaker.TrainingJobARN) && describeTrainingJobResponse.TrainingJobStatus.Equals(TrainingJobStatus.Failed))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Error;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.Training;
                            }
                            _logContext.SagemakerConsolidatedEntities.Update(sagemaker);
                        }
                        else if (sagemaker.SagemakerStatus.Equals(SagemakerStatus.Deploying))
                        {
                            DescribeEndpointResponse response = await _SagemakerClient.DescribeEndpointAsync(new DescribeEndpointRequest
                            {
                                EndpointName = sagemaker.EndpointName
                            });
                            if (response.EndpointArn.Equals(sagemaker.EndpointJobARN) && response.EndpointStatus.Equals(EndpointStatus.InService))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Ready;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.None;
                                _accountContext.Alerts.Add(new Alert
                                {
                                    AlertType = AlertType.SageMakerDeployed,
                                    ExternalNotificationType = ExternalNotificationType.NONE,
                                    LinkedUserID = sagemaker.LinkedLogInput.LinkedUserID,
                                    TimeStamp = DateTime.Now,
                                    Message = "A Machine Learning Model for " + sagemaker.LinkedLogInput.Name + " has been deployed sucessfully!"
                                });
                            }
                            else if (response.EndpointArn.Equals(sagemaker.EndpointJobARN) && (response.EndpointStatus.Equals(EndpointStatus.OutOfService) || response.EndpointStatus.Equals(EndpointStatus.Failed)))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Error;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.Deployment;
                            }
                            else if (response.EndpointArn.Equals(sagemaker.EndpointJobARN) && (response.EndpointStatus.Equals(EndpointStatus.RollingBack) || response.EndpointStatus.Equals(EndpointStatus.Deleting)))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Reversing;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.None;
                            }
                            else if (response.EndpointArn.Equals(sagemaker.EndpointJobARN))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Deploying;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.None;
                            }
                            _logContext.SagemakerConsolidatedEntities.Update(sagemaker);
                        }
                        else if (sagemaker.SagemakerStatus.Equals(SagemakerStatus.Tuning))
                        {
                            DescribeHyperParameterTuningJobResponse response = await _SagemakerClient.DescribeHyperParameterTuningJobAsync(new DescribeHyperParameterTuningJobRequest
                            {
                                HyperParameterTuningJobName = sagemaker.HyperParameterTurningJobName
                            });
                            if (response.HyperParameterTuningJobArn.Equals(sagemaker.HyperParameterTurningJobARN) && response.HyperParameterTuningJobStatus.Equals(HyperParameterTuningJobStatus.Completed))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Trained;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.None;
                            }
                            else if (response.HyperParameterTuningJobArn.Equals(sagemaker.HyperParameterTurningJobARN) && response.HyperParameterTuningJobStatus.Equals(HyperParameterTuningJobStatus.InProgress))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Tuning;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.None;
                            }
                            else if (response.HyperParameterTuningJobArn.Equals(sagemaker.HyperParameterTurningJobARN) && response.HyperParameterTuningJobStatus.Equals(HyperParameterTuningJobStatus.Failed))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Error;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.Tuning;
                            }
                            _logContext.SagemakerConsolidatedEntities.Update(sagemaker);
                        }
                        else if (sagemaker.SagemakerStatus.Equals(SagemakerStatus.Transforming))
                        {
                            DescribeTransformJobResponse response = await _SagemakerClient.DescribeTransformJobAsync(new DescribeTransformJobRequest
                            {
                                TransformJobName = sagemaker.BatchTransformJobName
                            });
                            if (response.TransformJobArn.Equals(sagemaker.BatchTransformJobARN) && response.TransformJobStatus.Equals(TransformJobStatus.Completed))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Ready;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.None;
                            }
                            else if (response.TransformJobArn.Equals(sagemaker.BatchTransformJobARN) && response.TransformJobStatus.Equals(TransformJobStatus.InProgress))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Transforming;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.None;
                            }
                            else if (response.TransformJobArn.Equals(sagemaker.BatchTransformJobARN) && response.TransformJobStatus.Equals(TransformJobStatus.Failed))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Error;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.Transforming;
                            }
                            _logContext.SagemakerConsolidatedEntities.Update(sagemaker);
                        }
                    }
                }
            }
            await _logContext.SaveChangesAsync();
            await _accountContext.SaveChangesAsync();
        }
    }
}
