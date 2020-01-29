using Amazon.Glue;
using Amazon.Glue.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SageMaker;
using Amazon.SageMaker.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsSecProject.Data;
using OpsSecProject.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace OpsSecProject.Services
{
    internal class ScopedUpdateService : IScopedUpdateService
    {
        private readonly ILogger _logger;
        private LogContext _context;
        private IAmazonGlue _GlueClient;
        private IAmazonSageMaker _SagemakerClient;

        public ScopedUpdateService(ILogger<ScopedSetupService> logger, LogContext context, IAmazonGlue GlueClient, IAmazonSageMaker SagemakerClient)
        {
            _logger = logger;
            _context = context;
            _GlueClient = GlueClient;
            _SagemakerClient = SagemakerClient;
        }

        public async Task DoWorkAsync()
        {
            foreach (LogInput input in await _context.LogInputs.ToListAsync())
            {
                if (input.InitialIngest == true)
                {
                    foreach (SagemakerConsolidatedEntity sagemaker in input.LinkedSagemakerEntities)
                    {
                        if (sagemaker.SagemakerStatus.Equals(SagemakerStatus.Training))
                        {
                            DescribeTrainingJobResponse response = await _SagemakerClient.DescribeTrainingJobAsync(new DescribeTrainingJobRequest
                            {
                                TrainingJobName = sagemaker.TrainingJobName
                            });
                            if (response.TrainingJobArn.Equals(sagemaker.TrainingJobARN) && response.TrainingJobStatus.Equals(TrainingJobStatus.Completed))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Trained;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.None;
                            }
                            else if (response.TrainingJobArn.Equals(sagemaker.TrainingJobARN) && response.TrainingJobStatus.Equals(TrainingJobStatus.InProgress))
                                sagemaker.SagemakerStatus = SagemakerStatus.Training;
                            else if (response.TrainingJobArn.Equals(sagemaker.TrainingJobARN) && response.TrainingJobStatus.Equals(TrainingJobStatus.Failed))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Error;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.Training;
                            }
                            _context.SagemakerConsolidatedEntities.Update(sagemaker);
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
                            _context.SagemakerConsolidatedEntities.Update(sagemaker);
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
                            _context.SagemakerConsolidatedEntities.Update(sagemaker);
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
                            } else if (response.TransformJobArn.Equals(sagemaker.BatchTransformJobARN) && response.TransformJobStatus.Equals(TransformJobStatus.Failed))
                            {
                                sagemaker.SagemakerStatus = SagemakerStatus.Error;
                                sagemaker.SagemakerErrorStage = SagemakerErrorStage.Transforming;
                            }
                                _context.SagemakerConsolidatedEntities.Update(sagemaker);
                        }
                    }
                }
                await _context.SaveChangesAsync();
            }
        }
    }
}
