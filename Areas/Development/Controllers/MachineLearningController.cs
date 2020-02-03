using Amazon.SageMaker;
using Amazon.SageMaker.Model;
using Amazon.SageMakerRuntime;
using Amazon.SageMakerRuntime.Model;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using OpsSecProject.Data;
using System.Net;
using System.Threading.Tasks;
using OpsSecProject.Areas.Development.Models;
using System;
using OpsSecProject.Models;
using System.Linq;
using System.IO;
using System.Text;

namespace OpsSecProject.Areas.Development.Controllers
{
    [Area("Development")]
    public class MachineLearningController : Controller
    {
        private readonly IAmazonSageMaker _Sclient;
        private readonly IAmazonSageMakerRuntime _SRClient;
        private readonly LogContext _context;
        public MachineLearningController(IAmazonSageMaker Sclient, IAmazonSageMakerRuntime SRClient, LogContext context)
        {
            _Sclient = Sclient;
            _SRClient = SRClient;
            _context = context;
        }
        public IActionResult Index()
        {
            return View(new MachineLearningViewModel
            {
                LogInput = _context.LogInputs.Find(1)
            });
        }
        [HttpPost]
        public async Task<IActionResult> TrainingIPInsights([Bind("s3InputPath")]MachineLearningViewModel data)
        {
            if (_context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Untrained))
            {
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
                                S3Uri = "s3://" + _context.S3Buckets.Find(3).Name + "/" + data.s3InputPath
                            }
                        },
                        ContentType = "text/csv"
                    }
                },
                    OutputDataConfig = new OutputDataConfig
                    {
                        S3OutputPath = "s3://" + _context.S3Buckets.Find(2).Name + "/" + _context.LogInputs.Find(1).Name + "/ipinsights/model/model.tar.gz"
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
                    TrainingJobName = _context.LogInputs.Find(1).Name + "Training" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    CheckpointConfig = new CheckpointConfig
                    {
                        S3Uri = "s3://" + _context.S3Buckets.Find(2).Name + "/checkpoint/" + _context.LogInputs.Find(1).Name + "Training" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")
                    }
                };
                CreateTrainingJobResponse createTrainingJobResponse = await _Sclient.CreateTrainingJobAsync(createTrainingJobRequest);
                if (createTrainingJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    SagemakerConsolidatedEntity entity = _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0);
                    entity.SagemakerStatus = SagemakerStatus.Training;
                    entity.TrainingJobName = createTrainingJobRequest.TrainingJobName;
                    entity.TrainingJobARN = createTrainingJobResponse.TrainingJobArn;
                    _context.SagemakerConsolidatedEntities.Update(entity);
                    await _context.SaveChangesAsync();
                    TempData["Alert"] = "Success";
                    TempData["Message"] = "Training Job Created with ARN: " + createTrainingJobResponse.TrainingJobArn;
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["Alert"] = "Warning";
                    TempData["Message"] = "Training Job Failed to create";
                    return RedirectToAction("Index");
                }
            }
            else
            {
                TempData["Alert"] = "Danger";
                TempData["Message"] = "Invaild State | Current State is " + _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
        public async Task<IActionResult> DeployingIPInsights()
        {
            if (_context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Trained))
            {
                CreateModelRequest createModelRequest = new CreateModelRequest
                {
                    EnableNetworkIsolation = false,
                    ModelName = _context.LogInputs.Find(1).Name + "Model" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    ExecutionRoleArn = Environment.GetEnvironmentVariable("SAGEMAKER_EXECUTION_ROLE"),
                    Tags = new List<Tag>
                {
                    new Tag
                    {
                        Key = "Project",
                        Value = "OSPJ"
                    }
                },
                    PrimaryContainer = new ContainerDefinition
                    {
                        Image = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com/ipinsights:1",
                        ModelDataUrl = "s3://" + _context.S3Buckets.Find(2).Name + "/" + _context.LogInputs.Find(1).Name + "/ipinsights/model/model.tar.gz"
                    }
                };
                CreateModelResponse createModelResponse = await _Sclient.CreateModelAsync(createModelRequest);
                CreateEndpointConfigRequest createEndpointConfigRequest = new CreateEndpointConfigRequest
                {
                    EndpointConfigName = _context.LogInputs.Find(1).Name + "EndpointConfig" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    ProductionVariants = new List<ProductionVariant>
                {
                    new ProductionVariant
                    {
                        VariantName = _context.LogInputs.Find(1).Name + "ProductionVariant" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                        ModelName = createModelRequest.ModelName,
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
                CreateEndpointRequest createEndpointRequest = new CreateEndpointRequest
                {
                    EndpointConfigName = _context.LogInputs.Find(1).Name + "EndpointConfig" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    EndpointName = _context.LogInputs.Find(1).Name + "EndpointName" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
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
                if (createModelResponse.HttpStatusCode.Equals(HttpStatusCode.OK) && createEndpointConfigResponse.HttpStatusCode.Equals(HttpStatusCode.OK) && createEndpointResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    SagemakerConsolidatedEntity entity = _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0);
                    entity.SagemakerStatus = SagemakerStatus.Deploying;
                    entity.EndpointConfigurationARN = createEndpointConfigResponse.EndpointConfigArn;
                    entity.EndpointConfigurationName = createEndpointConfigRequest.EndpointConfigName;
                    entity.EndpointJobARN = createEndpointResponse.EndpointArn;
                    entity.EndpointName = createEndpointRequest.EndpointName;
                    entity.CurrentModelName = createModelRequest.ModelName;
                    _context.SagemakerConsolidatedEntities.Update(entity);
                    await _context.SaveChangesAsync();
                    TempData["Alert"] = "Success";
                    TempData["Message"] = "Inference Endpoint Configuration and Inference Endpoint Deployment Jobs Created with ARNs: " + createEndpointConfigResponse.EndpointConfigArn + " and " + createEndpointResponse.EndpointArn;
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["Alert"] = "Warning";
                    TempData["Message"] = "Inference Endpoint Configuration and Inference Endpoint Deployment Jobs Failed to create";
                    return RedirectToAction("Index");
                }
            }
            else
            {
                TempData["Alert"] = "Danger";
                TempData["Message"] = "Invaild State | Current State is " + _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> TuningIPInsights([Bind("s3InputPath")]MachineLearningViewModel data)
        {
            if (_context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Trained) || _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Ready))
            {
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
                        TrainingJobEarlyStoppingType = TrainingJobEarlyStoppingType.Off
                    },
                    HyperParameterTuningJobName = _context.LogInputs.Find(1).Name + "HyperParameterTuning" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
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
                                    S3Uri = "s3://" + _context.S3Buckets.Find(3).Name + "/" + data.s3InputPath
                                }
                            },
                            ContentType = "text/csv",
                            CompressionType = CompressionType.None,
                            RecordWrapperType = RecordWrapper.None
                        }
                    },
                        OutputDataConfig = new OutputDataConfig
                        {
                            S3OutputPath = "s3://" + _context.S3Buckets.Find(2).Name + "/" + _context.LogInputs.Find(1).Name + "/tuning"
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
                        CheckpointConfig = new CheckpointConfig
                        {
                            S3Uri = "s3://" + _context.S3Buckets.Find(2).Name + "/checkpoint/" + _context.LogInputs.Find(1).Name + "HyperParameterTuning" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")
                        }
                    }
                };
                CreateHyperParameterTuningJobResponse createHyperParameterTuningJobResponse = await _Sclient.CreateHyperParameterTuningJobAsync(createHyperParameterTuningJobRequest);
                if (createHyperParameterTuningJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    SagemakerConsolidatedEntity entity = _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0);
                    entity.SagemakerStatus = SagemakerStatus.Tuning;
                    entity.HyperParameterTurningJobARN = createHyperParameterTuningJobResponse.HyperParameterTuningJobArn;
                    entity.HyperParameterTurningJobName = createHyperParameterTuningJobRequest.HyperParameterTuningJobName;
                    _context.SagemakerConsolidatedEntities.Update(entity);
                    await _context.SaveChangesAsync();
                    TempData["Alert"] = "Success";
                    TempData["Message"] = "Hyper Parameter Tuning Job Created with ARN: " + createHyperParameterTuningJobResponse.HyperParameterTuningJobArn;
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["Alert"] = "Warning";
                    TempData["Message"] = "Hyper Parameter Tuning Job Failed to create";
                    return RedirectToAction("Index");
                }
            }
            else
            {
                TempData["Alert"] = "Danger";
                TempData["Message"] = "Invaild State | Current State is " + _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> TransformingIPInsights([Bind("s3InputPath")]MachineLearningViewModel data)
        {
            if (_context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Ready))
            {
                CreateTransformJobRequest createTransformJobRequest = new CreateTransformJobRequest
                {
                    TransformJobName = _context.LogInputs.Find(1).Name + "BatchTransform" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    ModelName = _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).CurrentModelName,
                    TransformInput = new TransformInput
                    {
                        CompressionType = CompressionType.None,
                        ContentType = "text/csv",
                        SplitType = SplitType.Line,
                        DataSource = new TransformDataSource
                        {
                            S3DataSource = new TransformS3DataSource
                            {
                                S3DataType = S3DataType.S3Prefix,
                                S3Uri = "s3://" + _context.S3Buckets.Find(3).Name + "/" + data.s3InputPath
                            }
                        }
                    },
                    TransformOutput = new TransformOutput
                    {
                        AssembleWith = AssemblyType.Line,
                        Accept = "text/csv",
                        S3OutputPath = "s3://" + _context.S3Buckets.Find(2).Name + "/" + _context.LogInputs.Find(1).Name + "/transform"
                    },
                    TransformResources = new TransformResources
                    {
                        InstanceCount = 1,
                        InstanceType = TransformInstanceType.MlM4Xlarge
                    }
                };
                CreateTransformJobResponse createTransformJobResponse = await _Sclient.CreateTransformJobAsync(createTransformJobRequest);
                if (createTransformJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    TempData["Alert"] = "Success";
                    TempData["Message"] = "Transform Job Created with ARN: " + createTransformJobResponse.TransformJobArn;
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["Alert"] = "Warning";
                    TempData["Message"] = "Transform Job Failed to create";
                    return RedirectToAction("Index");
                }
            }
            else
            {
                TempData["Alert"] = "Danger";
                TempData["Message"] = "Invaild State | Current State is " + _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> Inferencing([Bind("predictionInputContent", "algoritithmChoice")]MachineLearningViewModel data)
        {
            if (_context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Ready))
            {
                InvokeEndpointRequest invokeEndpointRequest = new InvokeEndpointRequest
                {
                    ContentType = "text/csv",
                    Accept = "text/csv",
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(data.predictionInputContent))
                };
                if (data.algoritithmChoice.Equals(AlgoritithmChoice.IPInsights))
                {
                    invokeEndpointRequest.EndpointName = _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).EndpointName;
                    invokeEndpointRequest.TargetModel = _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).CurrentModelName;
                }
                else if (data.algoritithmChoice.Equals(AlgoritithmChoice.RandomCutForest))
                {
                    invokeEndpointRequest.EndpointName = _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(1).EndpointName;
                    invokeEndpointRequest.TargetModel = _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(1).CurrentModelName;
                }
                InvokeEndpointResponse invokeEndpointResponse = await _SRClient.InvokeEndpointAsync(invokeEndpointRequest);
                if (invokeEndpointResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    using (StreamReader reader = new StreamReader(invokeEndpointResponse.Body))
                    {
                        TempData["Alert"] = "Success";
                        TempData["Message"] = "Endpoint returned result : " + reader.ReadToEnd();
                        return RedirectToAction("Index");
                    }
                }
                else
                {
                    TempData["Alert"] = "Warning";
                    TempData["Message"] = "Endpoint failed to invoke";
                    return RedirectToAction("Index");
                }
            }
            else
            {
                TempData["Alert"] = "Danger";
                if (data.algoritithmChoice.Equals(AlgoritithmChoice.IPInsights))
                    TempData["Message"] = "Invaild State | Current State is " + _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.ToString();
                else if (data.algoritithmChoice.Equals(AlgoritithmChoice.RandomCutForest))
                    TempData["Message"] = "Invaild State | Current State is " + _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(1).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> TrainingRandomCutForest([Bind("s3InputPath")]MachineLearningViewModel data)
        {
            if (_context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Untrained))
            {
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
                                S3DataType = S3DataType.ManifestFile,
                                S3Uri = "s3://" + _context.S3Buckets.Find(3).Name + "/" + data.s3InputPath
                            }
                        },
                        ContentType = "text/csv"
                    }
                },
                    OutputDataConfig = new OutputDataConfig
                    {
                        S3OutputPath = "s3://" + _context.S3Buckets.Find(2).Name + "/" + _context.LogInputs.Find(1).Name + "/randomcutforest/model/model.tar.gz"
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
                    TrainingJobName = _context.LogInputs.Find(1).Name + "Training" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    CheckpointConfig = new CheckpointConfig
                    {
                        S3Uri = "s3://" + _context.S3Buckets.Find(2).Name + "/checkpoint/" + _context.LogInputs.Find(1).Name + "Training" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")
                    }
                };
                CreateTrainingJobResponse createTrainingJobResponse = await _Sclient.CreateTrainingJobAsync(createTrainingJobRequest);
                if (createTrainingJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    SagemakerConsolidatedEntity entity = _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(1);
                    entity.SagemakerStatus = SagemakerStatus.Training;
                    entity.TrainingJobName = createTrainingJobRequest.TrainingJobName;
                    entity.TrainingJobARN = createTrainingJobResponse.TrainingJobArn;
                    _context.SagemakerConsolidatedEntities.Update(entity);
                    await _context.SaveChangesAsync();
                    TempData["Alert"] = "Success";
                    TempData["Message"] = "Training Job Created with ARN: " + createTrainingJobResponse.TrainingJobArn;
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["Alert"] = "Warning";
                    TempData["Message"] = "Training Job Failed to create";
                    return RedirectToAction("Index");
                }
            }
            else
            {
                TempData["Alert"] = "Danger";
                TempData["Message"] = "Invaild State | Current State is " + _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
        public async Task<IActionResult> DeployingRandomCutForest()
        {
            if (_context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Trained))
            {
                CreateModelRequest createModelRequest = new CreateModelRequest
                {
                    EnableNetworkIsolation = false,
                    ModelName = _context.LogInputs.Find(1).Name + "Model" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    ExecutionRoleArn = Environment.GetEnvironmentVariable("SAGEMAKER_EXECUTION_ROLE"),
                    Tags = new List<Tag>
                {
                    new Tag
                    {
                        Key = "Project",
                        Value = "OSPJ"
                    }
                },
                    PrimaryContainer = new ContainerDefinition
                    {
                        Image = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com/ipinsights:1",
                        ModelDataUrl = "s3://" + _context.S3Buckets.Find(2).Name + "/" + _context.LogInputs.Find(1).Name + "/randomcutforest/model/model.tar.gz"
                    }
                };
                CreateModelResponse createModelResponse = await _Sclient.CreateModelAsync(createModelRequest);
                CreateEndpointConfigRequest createEndpointConfigRequest = new CreateEndpointConfigRequest
                {
                    EndpointConfigName = _context.LogInputs.Find(1).Name + "EndpointConfig" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    ProductionVariants = new List<ProductionVariant>
                {
                    new ProductionVariant
                    {
                        VariantName = _context.LogInputs.Find(1).Name + "ProductionVariant" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                        ModelName = createModelRequest.ModelName,
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
                CreateEndpointRequest createEndpointRequest = new CreateEndpointRequest
                {
                    EndpointConfigName = _context.LogInputs.Find(1).Name + "EndpointConfig" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    EndpointName = _context.LogInputs.Find(1).Name + "EndpointName" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
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
                if (createModelResponse.HttpStatusCode.Equals(HttpStatusCode.OK) && createEndpointConfigResponse.HttpStatusCode.Equals(HttpStatusCode.OK) && createEndpointResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    SagemakerConsolidatedEntity entity = _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(1);
                    entity.SagemakerStatus = SagemakerStatus.Deploying;
                    entity.EndpointConfigurationARN = createEndpointConfigResponse.EndpointConfigArn;
                    entity.EndpointConfigurationName = createEndpointConfigRequest.EndpointConfigName;
                    entity.EndpointJobARN = createEndpointResponse.EndpointArn;
                    entity.EndpointName = createEndpointRequest.EndpointName;
                    entity.CurrentModelName = createModelRequest.ModelName;
                    _context.SagemakerConsolidatedEntities.Update(entity);
                    await _context.SaveChangesAsync();
                    TempData["Alert"] = "Success";
                    TempData["Message"] = "Inference Endpoint Configuration and Inference Endpoint Deployment Jobs Created with ARNs: " + createEndpointConfigResponse.EndpointConfigArn + " and " + createEndpointResponse.EndpointArn;
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["Alert"] = "Warning";
                    TempData["Message"] = "Inference Endpoint Configuration and Inference Endpoint Deployment Jobs Failed to create";
                    return RedirectToAction("Index");
                }
            }
            else
            {
                TempData["Alert"] = "Danger";
                TempData["Message"] = "Invaild State | Current State is " + _context.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
    }
}