using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Amazon.SageMaker;
using Amazon.SageMaker.Model;
using Amazon.SageMakerRuntime;
using Amazon.SageMakerRuntime.Model;
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
        private IBackgroundTaskQueue _queue { get; }
        private readonly ILogger _logger;

        public InputsController(LogContext logContext, IBackgroundTaskQueue queue, ILogger<InputsController> logger, AccountContext accountContext, IAmazonSageMaker Sclient, IAmazonSageMakerRuntime SRClient)
        {
            _logContext = logContext;
            _queue = queue;
            _logger = logger;
            _accountContext = accountContext;
            _Sclient = Sclient;
            _SRClient = SRClient;
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
            return View(new InputMachineLearningViewModel
            {
                LogInput = await _logContext.LogInputs.FindAsync(InputID)
            });
        }

        [HttpPost]
        public async Task<IActionResult> TrainingIPInsights([Bind("LogInputID","s3InputPath")]InputMachineLearningViewModel data)
        {
            if (_logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Untrained))
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
                                S3Uri = "s3://" + _logContext.LogInputs.Find(data.LogInputID).LinkedS3Bucket.Name + "/" + data.s3InputPath
                            }
                        },
                        ContentType = "text/csv"
                    }
                },
                    OutputDataConfig = new OutputDataConfig
                    {
                        S3OutputPath = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + _logContext.LogInputs.Find(data.LogInputID).Name + "/ipinsights/model/model.tar.gz"
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
                    TrainingJobName = _logContext.LogInputs.Find(data.LogInputID).Name + "Training" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    CheckpointConfig = new CheckpointConfig
                    {
                        S3Uri = "s3://" + _logContext.S3Buckets.Find(2).Name + "/checkpoint/" + _logContext.LogInputs.Find(data.LogInputID).Name + "Training" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")
                    }
                };
                CreateTrainingJobResponse createTrainingJobResponse = await _Sclient.CreateTrainingJobAsync(createTrainingJobRequest);
                if (createTrainingJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    SagemakerConsolidatedEntity entity = _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0);
                    entity.SagemakerStatus = SagemakerStatus.Training;
                    entity.TrainingJobName = createTrainingJobRequest.TrainingJobName;
                    entity.TrainingJobARN = createTrainingJobResponse.TrainingJobArn;
                    _logContext.SagemakerConsolidatedEntities.Update(entity);
                    await _logContext.SaveChangesAsync();
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
                TempData["Message"] = "Invaild State | Current State is " + _logContext.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> DeployingIPInsights([Bind("LogInputID")]InputMachineLearningViewModel data)
        {
            if (_logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Trained))
            {
                CreateModelRequest createModelRequest = new CreateModelRequest
                {
                    EnableNetworkIsolation = false,
                    ModelName = _logContext.LogInputs.Find(data.LogInputID).Name + "Model" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
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
                        ModelDataUrl = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + _logContext.LogInputs.Find(data.LogInputID).Name + "/ipinsights/model/model.tar.gz"
                    }
                };
                CreateModelResponse createModelResponse = await _Sclient.CreateModelAsync(createModelRequest);
                CreateEndpointConfigRequest createEndpointConfigRequest = new CreateEndpointConfigRequest
                {
                    EndpointConfigName = _logContext.LogInputs.Find(data.LogInputID).Name + "EndpointConfig" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    ProductionVariants = new List<ProductionVariant>
                {
                    new ProductionVariant
                    {
                        VariantName = _logContext.LogInputs.Find(data.LogInputID).Name + "ProductionVariant" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
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
                    EndpointConfigName = _logContext.LogInputs.Find(data.LogInputID).Name + "EndpointConfig" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    EndpointName = _logContext.LogInputs.Find(data.LogInputID).Name + "EndpointName" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
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
                    SagemakerConsolidatedEntity entity = _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0);
                    entity.SagemakerStatus = SagemakerStatus.Deploying;
                    entity.EndpointConfigurationARN = createEndpointConfigResponse.EndpointConfigArn;
                    entity.EndpointConfigurationName = createEndpointConfigRequest.EndpointConfigName;
                    entity.EndpointJobARN = createEndpointResponse.EndpointArn;
                    entity.EndpointName = createEndpointRequest.EndpointName;
                    entity.ModelName = createModelRequest.ModelName;
                    _logContext.SagemakerConsolidatedEntities.Update(entity);
                    await _logContext.SaveChangesAsync();
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
                TempData["Message"] = "Invaild State | Current State is " + _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> TuningIPInsights([Bind("LogInputID","s3InputPath")]InputMachineLearningViewModel data)
        {
            if (_logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Trained) || _logContext.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Ready))
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
                    HyperParameterTuningJobName = _logContext.LogInputs.Find(data.LogInputID).Name + "HyperParameterTuning" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
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
                                    S3Uri = "s3://" + _logContext.LogInputs.Find(data.LogInputID).LinkedS3Bucket.Name + "/" + data.s3InputPath
                                }
                            },
                            ContentType = "text/csv",
                            CompressionType = CompressionType.None,
                            RecordWrapperType = RecordWrapper.None
                        }
                    },
                        OutputDataConfig = new OutputDataConfig
                        {
                            S3OutputPath = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + _logContext.LogInputs.Find(data.LogInputID).Name + "/tuning"
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
                            S3Uri = "s3://" + _logContext.S3Buckets.Find(2).Name + "/checkpoint/" + _logContext.LogInputs.Find(data.LogInputID).Name + "HyperParameterTuning" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")
                        }
                    }
                };
                CreateHyperParameterTuningJobResponse createHyperParameterTuningJobResponse = await _Sclient.CreateHyperParameterTuningJobAsync(createHyperParameterTuningJobRequest);
                if (createHyperParameterTuningJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    SagemakerConsolidatedEntity entity = _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0);
                    entity.SagemakerStatus = SagemakerStatus.Tuning;
                    entity.HyperParameterTurningJobARN = createHyperParameterTuningJobResponse.HyperParameterTuningJobArn;
                    entity.HyperParameterTurningJobName = createHyperParameterTuningJobRequest.HyperParameterTuningJobName;
                    _logContext.SagemakerConsolidatedEntities.Update(entity);
                    await _logContext.SaveChangesAsync();
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
                TempData["Message"] = "Invaild State | Current State is " + _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> TransformingIPInsights([Bind("LogInputID","s3InputPath")]InputMachineLearningViewModel data)
        {
            if (_logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Ready))
            {
                CreateTransformJobRequest createTransformJobRequest = new CreateTransformJobRequest
                {
                    TransformJobName = _logContext.LogInputs.Find(data.LogInputID).Name + "BatchTransform" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    ModelName = _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0).ModelName,
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
                                S3Uri = "s3://" + _logContext.LogInputs.Find(data.LogInputID).LinkedS3Bucket.Name + "/" + data.s3InputPath
                            }
                        }
                    },
                    TransformOutput = new TransformOutput
                    {
                        AssembleWith = AssemblyType.Line,
                        Accept = "text/csv",
                        S3OutputPath = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + _logContext.LogInputs.Find(data.LogInputID).Name + "/transform"
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
                TempData["Message"] = "Invaild State | Current State is " + _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> Inferencing([Bind("LogInputID","predictionInputContent", "algoritithmChoice")]InputMachineLearningViewModel data)
        {
            if (_logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.Equals(SagemakerStatus.Ready))
            {
                InvokeEndpointRequest invokeEndpointRequest = new InvokeEndpointRequest
                {
                    ContentType = "text/csv",
                    Accept = "text/csv",
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(data.predictionInputContent))
                };
                if (data.algoritithmChoice.Equals(AlgoritithmChoice.IPInsights))
                {
                    invokeEndpointRequest.EndpointName = _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0).EndpointName;
                    invokeEndpointRequest.TargetModel = _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0).ModelName;
                }
                else if (data.algoritithmChoice.Equals(AlgoritithmChoice.RandomCutForest))
                {
                    invokeEndpointRequest.EndpointName = _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(1).EndpointName;
                    invokeEndpointRequest.TargetModel = _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(1).ModelName;
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
                    TempData["Message"] = "Invaild State | Current State is " + _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.ToString();
                else if (data.algoritithmChoice.Equals(AlgoritithmChoice.RandomCutForest))
                    TempData["Message"] = "Invaild State | Current State is " + _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(1).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> TrainingRandomCutForest([Bind("LogInputID","s3InputPath")]InputMachineLearningViewModel data)
        {
            if (_logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(1).SagemakerStatus.Equals(SagemakerStatus.Untrained))
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
                                S3Uri = "s3://" + _logContext.LogInputs.Find(data.LogInputID).LinkedS3Bucket.Name + "/" + data.s3InputPath
                            }
                        },
                        ContentType = "text/csv"
                    }
                },
                    OutputDataConfig = new OutputDataConfig
                    {
                        S3OutputPath = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + _logContext.LogInputs.Find(data.LogInputID).Name + "/randomcutforest/model/model.tar.gz"
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
                    TrainingJobName = _logContext.LogInputs.Find(data.LogInputID).Name + "Training" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    CheckpointConfig = new CheckpointConfig
                    {
                        S3Uri = "s3://" + _logContext.S3Buckets.Find(2).Name + "/checkpoint/" + _logContext.LogInputs.Find(data.LogInputID).Name + "Training" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")
                    }
                };
                CreateTrainingJobResponse createTrainingJobResponse = await _Sclient.CreateTrainingJobAsync(createTrainingJobRequest);
                if (createTrainingJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    SagemakerConsolidatedEntity entity = _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(1);
                    entity.SagemakerStatus = SagemakerStatus.Training;
                    entity.TrainingJobName = createTrainingJobRequest.TrainingJobName;
                    entity.TrainingJobARN = createTrainingJobResponse.TrainingJobArn;
                    _logContext.SagemakerConsolidatedEntities.Update(entity);
                    await _logContext.SaveChangesAsync();
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
                TempData["Message"] = "Invaild State | Current State is " + _logContext.LogInputs.Find(1).LinkedSagemakerEntities.ElementAt(1).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> DeployingRandomCutForest([Bind("LogInputID")]InputMachineLearningViewModel data)
        {
            if (_logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(1).SagemakerStatus.Equals(SagemakerStatus.Trained))
            {
                CreateModelRequest createModelRequest = new CreateModelRequest
                {
                    EnableNetworkIsolation = false,
                    ModelName = _logContext.LogInputs.Find(data.LogInputID).Name + "Model" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
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
                        ModelDataUrl = "s3://" + _logContext.S3Buckets.Find(2).Name + "/" + _logContext.LogInputs.Find(data.LogInputID).Name + "/randomcutforest/model/model.tar.gz"
                    }
                };
                CreateModelResponse createModelResponse = await _Sclient.CreateModelAsync(createModelRequest);
                CreateEndpointConfigRequest createEndpointConfigRequest = new CreateEndpointConfigRequest
                {
                    EndpointConfigName = _logContext.LogInputs.Find(data.LogInputID).Name + "EndpointConfig" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    ProductionVariants = new List<ProductionVariant>
                {
                    new ProductionVariant
                    {
                        VariantName = _logContext.LogInputs.Find(data.LogInputID).Name + "ProductionVariant" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
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
                    EndpointConfigName = _logContext.LogInputs.Find(data.LogInputID).Name + "EndpointConfig" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                    EndpointName = _logContext.LogInputs.Find(data.LogInputID).Name + "EndpointName" + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
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
                    SagemakerConsolidatedEntity entity = _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(1);
                    entity.SagemakerStatus = SagemakerStatus.Deploying;
                    entity.EndpointConfigurationARN = createEndpointConfigResponse.EndpointConfigArn;
                    entity.EndpointConfigurationName = createEndpointConfigRequest.EndpointConfigName;
                    entity.EndpointJobARN = createEndpointResponse.EndpointArn;
                    entity.EndpointName = createEndpointRequest.EndpointName;
                    entity.ModelName = createModelRequest.ModelName;
                    _logContext.SagemakerConsolidatedEntities.Update(entity);
                    await _logContext.SaveChangesAsync();
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
                TempData["Message"] = "Invaild State | Current State is " + _logContext.LogInputs.Find(data.LogInputID).LinkedSagemakerEntities.ElementAt(0).SagemakerStatus.ToString();
                return RedirectToAction("Index");
            }
        }
    }
}