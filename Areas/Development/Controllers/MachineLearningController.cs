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
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Training([Bind("jobName", "s3InputUri", "s3OutputPath")]MachineLearningOverrallFormModel data)
        {
            CreateTrainingJobResponse createTrainingJobResponse = await _Sclient.CreateTrainingJobAsync(new CreateTrainingJobRequest
            {
                AlgorithmSpecification = new AlgorithmSpecification
                {
                    TrainingImage = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com:1",
                    TrainingInputMode = TrainingInputMode.File,
                    MetricDefinitions = new List<MetricDefinition>
                    {
                        new MetricDefinition
                        {
                            Name = "train:binary_classification_cross_entropy:epoch",
                            Regex = "#quality_metric: host=\\S+, epoch=\\S+, train binary_classification_cross_entropy <loss>=(\\S+)"
                        },
                        new MetricDefinition
                        {
                            Name = "train:binary_classification_accuracy",
                            Regex = "#quality_metric: host=\\S+, epoch=\\S+, batch=\\S+ train binary_classification_accuracy <score>=(\\S+)"
                        },
                        new MetricDefinition
                        {
                            Name = "train:binary_classification_cross_entropy",
                            Regex = "#quality_metric: host=\\S+, epoch=\\S+, batch=\\S+ train binary_classification_cross_entropy <loss>=(\\S+)"
                        },
                        new MetricDefinition
                        {
                            Name = "train:progress",
                            Regex = "#progress_metric: host=\\S+, completed (\\S+) %"
                        },
                        new MetricDefinition
                        {
                            Name = "validation:discriminator_auc",
                            Regex = "#quality_metric: host=\\S+, epoch=\\S+, validation discriminator_auc <score>=(\\S+)"
                        },
                        new MetricDefinition
                        {
                            Name = "train:throughput",
                            Regex = "#throughput_metric: host=\\S+, train throughput=(\\S+) records/second"
                        },
                        new MetricDefinition
                        {
                            Name = "train:binary_classification_accuracy:epoch",
                            Regex = "#quality_metric: host=\\S+, epoch=\\S+, train binary_classification_accuracy <score>=(\\S+)"
                        }
                    }
                },
                EnableManagedSpotTraining = false,
                EnableInterContainerTrafficEncryption = false,
                EnableNetworkIsolation = false,
                HyperParameters = new Dictionary<string, string>
                {
                    {"num_entity_vectors","20000"},
                    {"random_negative_sampling_rate","5"},
                    {"vector_dim","128"},
                    {"mini_batch_size","1000"},
                    {"epochs","5"},
                    {"learning_rate","0.01" }
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
                                S3Uri=data.s3InputUri
                            }
                        },
                        ContentType = "text/csv",
                        CompressionType = CompressionType.None,
                        RecordWrapperType = RecordWrapper.None
                    }
                },
                OutputDataConfig = new OutputDataConfig
                {
                    S3OutputPath = "s3://" + _context.S3Buckets.Find(2).Name + "/" + data.s3OutputPath
                },
                ResourceConfig = new ResourceConfig
                {
                    InstanceCount = 1,
                    InstanceType = TrainingInstanceType.MlM4Xlarge,
                    VolumeSizeInGB = 30
                },
                RoleArn = "arn:aws:iam::188363912800:role/service-role/AmazonSageMaker-ExecutionRole-20191023T101168",
                StoppingCondition = new StoppingCondition
                {
                    MaxRuntimeInSeconds = 86400
                },
                Tags = new List<Tag>
                {
                    new Tag
                    {
                        Key = "Project",
                        Value = "OSPJ"
                    }
                },
                TrainingJobName = data.jobName
            });
            if (createTrainingJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
            {
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
        [HttpPost]
        public async Task<IActionResult> Inferencing([Bind("configName", "endpointName")]MachineLearningOverrallFormModel data)
        {
            CreateEndpointConfigResponse createEndpointConfigResponse = await _Sclient.CreateEndpointConfigAsync(new CreateEndpointConfigRequest
            {
                DataCaptureConfig = new DataCaptureConfig
                {

                },
                EndpointConfigName = data.configName,
                ProductionVariants = new List<ProductionVariant>
                {

                },
                Tags = new List<Tag>
                {
                    new Tag
                    {
                        Key = "Project",
                        Value = "OSPJ"
                    }
                }
            });
            CreateEndpointResponse createEndpointResponse = await _Sclient.CreateEndpointAsync(new CreateEndpointRequest
            {
                EndpointConfigName = data.configName,
                EndpointName = data.endpointName,
                Tags = new List<Tag>
                {
                    new Tag
                    {
                        Key = "Project",
                        Value = "OSPJ"
                    }
                }
            });
            if (createEndpointConfigResponse.HttpStatusCode.Equals(HttpStatusCode.OK) && createEndpointResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
            {
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
        [HttpPost]
        public async Task<IActionResult> Tuning([Bind("jobName", "s3InputUri", "s3OutputPath")]MachineLearningOverrallFormModel data)
        {
            CreateHyperParameterTuningJobResponse createHyperParameterTuningJobResponse = await _Sclient.CreateHyperParameterTuningJobAsync(new CreateHyperParameterTuningJobRequest
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
                    TrainingJobEarlyStoppingType = TrainingJobEarlyStoppingType.Off,
                    TuningJobCompletionCriteria = new TuningJobCompletionCriteria
                    {
                        TargetObjectiveMetricValue = 0.988F
                    }
                },
                HyperParameterTuningJobName = data.jobName,
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
                        TrainingImage = "475088953585.dkr.ecr.ap-southeast-1.amazonaws.com:1",
                        TrainingInputMode = TrainingInputMode.File,
                        MetricDefinitions = new List<MetricDefinition>
                    {
                        new MetricDefinition
                        {
                            Name = "train:binary_classification_cross_entropy:epoch",
                            Regex = "#quality_metric: host=\\S+, epoch=\\S+, train binary_classification_cross_entropy <loss>=(\\S+)"
                        },
                        new MetricDefinition
                        {
                            Name = "train:binary_classification_accuracy",
                            Regex = "#quality_metric: host=\\S+, epoch=\\S+, batch=\\S+ train binary_classification_accuracy <score>=(\\S+)"
                        },
                        new MetricDefinition
                        {
                            Name = "train:binary_classification_cross_entropy",
                            Regex = "#quality_metric: host=\\S+, epoch=\\S+, batch=\\S+ train binary_classification_cross_entropy <loss>=(\\S+)"
                        },
                        new MetricDefinition
                        {
                            Name = "train:progress",
                            Regex = "#progress_metric: host=\\S+, completed (\\S+) %"
                        },
                        new MetricDefinition
                        {
                            Name = "validation:discriminator_auc",
                            Regex = "#quality_metric: host=\\S+, epoch=\\S+, validation discriminator_auc <score>=(\\S+)"
                        },
                        new MetricDefinition
                        {
                            Name = "train:throughput",
                            Regex = "#throughput_metric: host=\\S+, train throughput=(\\S+) records/second"
                        },
                        new MetricDefinition
                        {
                            Name = "train:binary_classification_accuracy:epoch",
                            Regex = "#quality_metric: host=\\S+, epoch=\\S+, train binary_classification_accuracy <score>=(\\S+)"
                        }
                    }
                    },
                    EnableManagedSpotTraining = false,
                    EnableInterContainerTrafficEncryption = false,
                    EnableNetworkIsolation = false,
                    StaticHyperParameters = new Dictionary<string, string>
                    {
                        {"num_entity_vectors","20000"},
                        {"random_negative_sampling_rate","5"},
                        {"vector_dim","128"},
                        {"mini_batch_size","1000"},
                        {"epochs","5"},
                        {"learning_rate","0.01" }
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
                                    S3Uri = data.s3InputUri
                                }
                            },
                            ContentType = "text/csv",
                            CompressionType = CompressionType.None,
                            RecordWrapperType = RecordWrapper.None
                        }
                    },
                    OutputDataConfig = new OutputDataConfig
                    {
                        S3OutputPath = "s3://" + _context.S3Buckets.Find(2).Name + "/" + data.s3OutputPath
                    },
                    ResourceConfig = new ResourceConfig
                    {
                        InstanceCount = 1,
                        InstanceType = TrainingInstanceType.MlM4Xlarge,
                        VolumeSizeInGB = 30
                    },
                    RoleArn = "arn:aws:iam::188363912800:role/service-role/AmazonSageMaker-ExecutionRole-20191023T101168",
                    StoppingCondition = new StoppingCondition
                    {
                        MaxRuntimeInSeconds = 86400
                    }
                }
            });
            if (createHyperParameterTuningJobResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
            {
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
        [HttpPost]
        public async Task<IActionResult> Transforming([Bind("jobName","modelName", "s3InputUri", "s3OutputPath")]MachineLearningOverrallFormModel data)
        {
            CreateTransformJobResponse createTransformJobResponse = await _Sclient.CreateTransformJobAsync(new CreateTransformJobRequest
            {
                TransformJobName = data.jobName,
                ModelName = data.modelName,
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
                            S3Uri = data.s3InputUri
                        }
                    }
                },
                TransformOutput = new TransformOutput
                {
                    AssembleWith = AssemblyType.Line,
                    Accept = "text/csv",
                    S3OutputPath = "s3://" + _context.S3Buckets.Find(2).Name + "/" + data.s3OutputPath
                },
                TransformResources = new TransformResources
                {
                    InstanceCount = 1,
                    InstanceType = TransformInstanceType.MlM4Xlarge
                }
            });
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
    }
}