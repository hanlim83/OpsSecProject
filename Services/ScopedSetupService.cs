using Amazon.Glue;
using Amazon.Glue.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsSecProject.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace OpsSecProject.Services
{
    internal class ScopedSetupService : IScopedSetupService
    {
        private readonly ILogger _logger;
        private LogContext _context;
        private IAmazonS3 _S3Client;
        private IAmazonGlue _GlueClient;
        public ScopedSetupService(ILogger<ScopedSetupService> logger, LogContext context, IAmazonS3 S3Client, IAmazonGlue GlueClient)
        {
            _logger = logger;
            _context = context;
            _S3Client = S3Client;
            _GlueClient = GlueClient;
        }

        public async Task DoWorkAsync()
        {
            _context.Database.OpenConnection();
            _context.Database.ExecuteSqlCommand("SET IDENTITY_INSERT dbo.S3Buckets ON");
            ListBucketsResponse listBucketResponse = await _S3Client.ListBucketsAsync(new ListBucketsRequest());
            bool sageMakerBucketFound = false, apacheWebLogBucketFound = false, SSHLogBucketFound = false, windowsSecurityLogBucketFound = false, squidProxyLogBucketFound = false;
            foreach (var bucket in listBucketResponse.Buckets)
            {
                if (bucket.BucketName.Equals("master-sagemaker-data"))
                {
                    sageMakerBucketFound = true;
                    if (_context.S3Buckets.Find(1) == null)
                        _context.S3Buckets.Add(new Models.S3Bucket
                        {
                            ID = 1,
                            Name = "master-sagemaker-data"
                        });
                }
                else if (bucket.BucketName.Equals("smartinsights-apache-web-logs"))
                {
                    apacheWebLogBucketFound = true;
                    if (_context.S3Buckets.Find(2) == null)
                        _context.S3Buckets.Add(new Models.S3Bucket
                        {
                            ID = 2,
                            Name = "smartinsights-apache-web-logs"
                        });
                }
                else if (bucket.BucketName.Equals("smartinsights-ssh-logs"))
                {
                    SSHLogBucketFound = true;
                    if (_context.S3Buckets.Find(3) == null)
                        _context.S3Buckets.Add(new Models.S3Bucket
                        {
                            ID = 3,
                            Name = "smartinsights-ssh-logs"
                        });
                }
                else if (bucket.BucketName.Equals("smartinsights-windows-security-logs"))
                {
                    windowsSecurityLogBucketFound = true;
                    if (_context.S3Buckets.Find(4) == null)
                        _context.S3Buckets.Add(new Models.S3Bucket
                        {
                            ID = 4,
                            Name = "smartinsights-windows-security-logs"
                        });
                }
                else if (bucket.BucketName.Equals("smartinsights-squid-proxy-logs"))
                {
                    squidProxyLogBucketFound = true;
                    if (_context.S3Buckets.Find(5) == null)
                        _context.S3Buckets.Add(new Models.S3Bucket
                        {
                            ID = 5,
                            Name = "smartinsights-squid-proxy-logs"
                        });
                }
                if (sageMakerBucketFound && apacheWebLogBucketFound && SSHLogBucketFound && windowsSecurityLogBucketFound && squidProxyLogBucketFound)
                    break;
            }
            if (!sageMakerBucketFound && _context.S3Buckets.Find(2) == null)
            {
                PutBucketResponse putBucketResponse2 = await _S3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = "master-sagemaker-data",
                    UseClientRegion = true,
                    CannedACL = S3CannedACL.Private
                });
                PutBucketTaggingResponse putBucketTaggingResponse2 = await _S3Client.PutBucketTaggingAsync(new PutBucketTaggingRequest
                {
                    BucketName = "master-sagemaker-data",
                    TagSet = new List<Tag>
                    {
                        new Tag
                        {
                            Key = "Project",
                            Value = "OSPJ"
                        }
                    }
                });
                PutPublicAccessBlockResponse putPublicAccessBlockResponse2 = await _S3Client.PutPublicAccessBlockAsync(new PutPublicAccessBlockRequest
                {
                    BucketName = "master-sagemaker-data",
                    PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration
                    {
                        BlockPublicAcls = true,
                        BlockPublicPolicy = true,
                        IgnorePublicAcls = true,
                        RestrictPublicBuckets = true
                    }
                });
                if (putBucketResponse2.HttpStatusCode.Equals(HttpStatusCode.OK) && putPublicAccessBlockResponse2.HttpStatusCode.Equals(HttpStatusCode.OK))
                    _context.S3Buckets.Add(new Models.S3Bucket
                    {
                        ID = 1,
                        Name = "master-sagemaker-data"
                    });
            }
            if (!apacheWebLogBucketFound && _context.S3Buckets.Find(3) == null)
            {
                PutBucketResponse putBucketResponse3 = await _S3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = "smartinsights-apache-web-logs",
                    UseClientRegion = true,
                    CannedACL = S3CannedACL.Private
                });
                PutBucketTaggingResponse putBucketTaggingResponse3 = await _S3Client.PutBucketTaggingAsync(new PutBucketTaggingRequest
                {
                    BucketName = "smartinsights-apache-web-logs",
                    TagSet = new List<Tag>
                    {
                        new Tag
                        {
                            Key = "Project",
                            Value = "OSPJ"
                        }
                    }
                });
                PutPublicAccessBlockResponse putPublicAccessBlockResponse3 = await _S3Client.PutPublicAccessBlockAsync(new PutPublicAccessBlockRequest
                {
                    BucketName = "smartinsights-apache-web-logs",
                    PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration
                    {
                        BlockPublicAcls = true,
                        BlockPublicPolicy = true,
                        IgnorePublicAcls = true,
                        RestrictPublicBuckets = true
                    }
                });
                if (putBucketResponse3.HttpStatusCode.Equals(HttpStatusCode.OK) && putPublicAccessBlockResponse3.HttpStatusCode.Equals(HttpStatusCode.OK))
                    _context.S3Buckets.Add(new Models.S3Bucket
                    {
                        ID = 2,
                        Name = "smartinsights-apache-web-logs"
                    });
            }
            if (!SSHLogBucketFound && _context.S3Buckets.Find(4) == null)
            {
                PutBucketResponse putBucketResponse4 = await _S3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = "smartinsights-ssh-logs",
                    UseClientRegion = true,
                    CannedACL = S3CannedACL.Private
                });
                PutBucketTaggingResponse putBucketTaggingResponse4 = await _S3Client.PutBucketTaggingAsync(new PutBucketTaggingRequest
                {
                    BucketName = "smartinsights-ssh-logs",
                    TagSet = new List<Tag>
                    {
                        new Tag
                        {
                            Key = "Project",
                            Value = "OSPJ"
                        }
                    }
                });
                PutPublicAccessBlockResponse putPublicAccessBlockResponse4 = await _S3Client.PutPublicAccessBlockAsync(new PutPublicAccessBlockRequest
                {
                    BucketName = "smartinsights-ssh-logs",
                    PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration
                    {
                        BlockPublicAcls = true,
                        BlockPublicPolicy = true,
                        IgnorePublicAcls = true,
                        RestrictPublicBuckets = true
                    }
                });
                if (putBucketResponse4.HttpStatusCode.Equals(HttpStatusCode.OK) && putPublicAccessBlockResponse4.HttpStatusCode.Equals(HttpStatusCode.OK))
                    _context.S3Buckets.Add(new Models.S3Bucket
                    {
                        ID = 3,
                        Name = "smartinsights-ssh-logs"
                    });
            }
            if (!windowsSecurityLogBucketFound && _context.S3Buckets.Find(5) == null)
            {
                PutBucketResponse putBucketResponse5 = await _S3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = "smartinsights-windows-security-logs",
                    UseClientRegion = true,
                    CannedACL = S3CannedACL.Private
                });
                PutBucketTaggingResponse putBucketTaggingResponse5 = await _S3Client.PutBucketTaggingAsync(new PutBucketTaggingRequest
                {
                    BucketName = "smartinsights-windows-security-logs",
                    TagSet = new List<Tag>
                    {
                        new Tag
                        {
                            Key = "Project",
                            Value = "OSPJ"
                        }
                    }
                });
                PutPublicAccessBlockResponse putPublicAccessBlockResponse5 = await _S3Client.PutPublicAccessBlockAsync(new PutPublicAccessBlockRequest
                {
                    BucketName = "smartinsights-windows-security-logs",
                    PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration
                    {
                        BlockPublicAcls = true,
                        BlockPublicPolicy = true,
                        IgnorePublicAcls = true,
                        RestrictPublicBuckets = true
                    }
                });
                if (putBucketResponse5.HttpStatusCode.Equals(HttpStatusCode.OK) && putPublicAccessBlockResponse5.HttpStatusCode.Equals(HttpStatusCode.OK))
                    _context.S3Buckets.Add(new Models.S3Bucket
                    {
                        ID = 4,
                        Name = "smartinsights-windows-security-logs"
                    });
            }
            if (!squidProxyLogBucketFound && _context.S3Buckets.Find(6) == null)
            {
                PutBucketResponse putBucketResponse6 = await _S3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = "smartinsights-squid-proxy-logs",
                    UseClientRegion = true,
                    CannedACL = S3CannedACL.Private
                });
                PutBucketTaggingResponse putBucketTaggingResponse6 = await _S3Client.PutBucketTaggingAsync(new PutBucketTaggingRequest
                {
                    BucketName = "smartinsights-squid-proxy-logs",
                    TagSet = new List<Tag>
                    {
                        new Tag
                        {
                            Key = "Project",
                            Value = "OSPJ"
                        }
                    }
                });
                PutPublicAccessBlockResponse putPublicAccessBlockResponse6 = await _S3Client.PutPublicAccessBlockAsync(new PutPublicAccessBlockRequest
                {
                    BucketName = "smartinsights-squid-proxy-logs",
                    PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration
                    {
                        BlockPublicAcls = true,
                        BlockPublicPolicy = true,
                        IgnorePublicAcls = true,
                        RestrictPublicBuckets = true
                    }
                });
                if (putBucketResponse6.HttpStatusCode.Equals(HttpStatusCode.OK) && putPublicAccessBlockResponse6.HttpStatusCode.Equals(HttpStatusCode.OK))
                    _context.S3Buckets.Add(new Models.S3Bucket
                    {
                        ID = 5,
                        Name = "smartinsights-squid-proxy-logs"
                    });
            }
            await _context.SaveChangesAsync();
            _context.Database.ExecuteSqlCommand("SET IDENTITY_INSERT dbo.S3Buckets OFF");
            _context.Database.ExecuteSqlCommand("SET IDENTITY_INSERT dbo.GlueDatabases ON");
            try
            {
                GetDatabaseResponse getDatabaseResponse = await _GlueClient.GetDatabaseAsync(new GetDatabaseRequest
                {
                    Name = "master-database"
                });
                if (_context.GlueDatabases.Find(1) == null)
                    _context.GlueDatabases.Add(new Models.GlueDatabase
                    {
                        ID = 1,
                        Name = "master-database"
                    });
            }
            catch (EntityNotFoundException)
            {
                CreateDatabaseResponse createDatabaseResponse = await _GlueClient.CreateDatabaseAsync(new CreateDatabaseRequest
                {
                    DatabaseInput = new DatabaseInput
                    {
                        Name = "master-database"
                    }
                });
                if (createDatabaseResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                    _context.GlueDatabases.Add(new Models.GlueDatabase
                    {
                        ID = 1,
                        Name = "master-database"
                    });
            }
            finally
            {
                await _context.SaveChangesAsync();
                _context.Database.ExecuteSqlCommand("SET IDENTITY_INSERT dbo.GlueDatabases OFF");
            }
            GetConnectionsResponse getConnectionsResponse = await _GlueClient.GetConnectionsAsync(new GetConnectionsRequest
            {
                HidePassword = false
            });
            bool GlueDBConnectionNameMatch = false, GlueDBConnectionParamtetersMatch = false;
            foreach (Connection c in getConnectionsResponse.ConnectionList)
            {
                if (c.Name.Equals(Environment.GetEnvironmentVariable("GLUE_DB-CONNECTION_NAME")))
                    GlueDBConnectionNameMatch = true;
                c.ConnectionProperties.TryGetValue("JDBC_CONNECTION_URL", out string DBHost);
                c.ConnectionProperties.TryGetValue("USERNAME", out string DBUserName);
                c.ConnectionProperties.TryGetValue("PASSWORD", out string DBPassword);
                if (DBHost.Contains(Environment.GetEnvironmentVariable("RDS_HOSTNAME")) && DBUserName.Equals(Environment.GetEnvironmentVariable("RDS_USERNAME")) && DBPassword.Equals(Environment.GetEnvironmentVariable("RDS_PASSWORD")))
                    GlueDBConnectionParamtetersMatch = true;
                if (GlueDBConnectionNameMatch && GlueDBConnectionParamtetersMatch)
                    break;
            }
            if (!GlueDBConnectionParamtetersMatch)
            {
                if (GlueDBConnectionNameMatch)
                {
                    DeleteConnectionResponse deleteConnectionResponse = await _GlueClient.DeleteConnectionAsync(new DeleteConnectionRequest
                    {
                        ConnectionName = Environment.GetEnvironmentVariable("GLUE_DB-CONNECTION_NAME")
                    });
                    if (deleteConnectionResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                    {
                        await _GlueClient.CreateConnectionAsync(new CreateConnectionRequest
                        {
                            ConnectionInput = new ConnectionInput
                            {
                                ConnectionProperties = new Dictionary<string, string>()
                        {
                            { "JDBC_CONNECTION_URL", "jdbc:sqlserver://"+ Environment.GetEnvironmentVariable("RDS_HOSTNAME") + ":" + Environment.GetEnvironmentVariable("RDS_PORT") + ";databaseName=" + Environment.GetEnvironmentVariable("GLUE_INGESTION-DB_NAME")},
                            { "JDBC_ENFORCE_SSL", "false" },
                            { "USERNAME", Environment.GetEnvironmentVariable("RDS_USERNAME") },
                            { "PASSWORD", Environment.GetEnvironmentVariable("RDS_PASSWORD") },
                        },
                                ConnectionType = ConnectionType.JDBC,
                                Name = Environment.GetEnvironmentVariable("GLUE_DB-CONNECTION_NAME"),
                                PhysicalConnectionRequirements = new PhysicalConnectionRequirements
                                {
                                    AvailabilityZone = "ap-southeast-1c",
                                    SubnetId = "subnet-0daa6ec8e25a13077",
                                    SecurityGroupIdList = new List<string>
                            {
                                "sg-0e1e79f6d49b3ed11"
                            }
                                }
                            }
                        });
                    }
                }
                else
                {
                    await _GlueClient.CreateConnectionAsync(new CreateConnectionRequest
                    {
                        ConnectionInput = new ConnectionInput
                        {
                            ConnectionProperties = new Dictionary<string, string>()
                        {
                            { "JDBC_CONNECTION_URL", "jdbc:sqlserver://"+ Environment.GetEnvironmentVariable("RDS_HOSTNAME") + ":" + Environment.GetEnvironmentVariable("RDS_PORT") + ";databaseName=" + Environment.GetEnvironmentVariable("GLUE_INGESTION-DB_NAME")},
                            { "JDBC_ENFORCE_SSL", "false" },
                            { "USERNAME", Environment.GetEnvironmentVariable("RDS_USERNAME") },
                            { "PASSWORD", Environment.GetEnvironmentVariable("RDS_PASSWORD") },
                        },
                            ConnectionType = ConnectionType.JDBC,
                            Name = Environment.GetEnvironmentVariable("GLUE_DB-CONNECTION_NAME"),
                            PhysicalConnectionRequirements = new PhysicalConnectionRequirements
                            {
                                AvailabilityZone = "ap-southeast-1c",
                                SubnetId = "subnet-0daa6ec8e25a13077",
                                SecurityGroupIdList = new List<string>
                            {
                                "sg-0e1e79f6d49b3ed11"
                            }
                            }
                        }
                    });
                }
            }
            if (!_context.LogInputs.Any())
            {
                _context.Database.ExecuteSqlCommand("SET IDENTITY_INSERT dbo.LogInputs ON");
                _context.LogInputs.Add(new Models.LogInput
                {
                    ID = 1,
                    Name = "Apache Web Logs",
                    FirehoseStreamName = "SmartInsights-Apache-Web-Logs",
                    ConfigurationJSON = "",
                    LogInputCategory = Models.LogInputCategory.ApacheWebServer,
                    LinkedUserID = 1,
                    LinkedS3BucketID = _context.S3Buckets.Find(2).ID,
                    LinkedS3Bucket = _context.S3Buckets.Find(2),
                    InitialIngest = true
                });
                _context.LogInputs.Add(new Models.LogInput
                {
                    ID = 2,
                    Name = "SSH Session Logs",
                    FirehoseStreamName = "SmartInsights-SSH-Login-Logs",
                    ConfigurationJSON = "",
                    LogInputCategory = Models.LogInputCategory.SSH,
                    LinkedUserID = 1,
                    LinkedS3BucketID = _context.S3Buckets.Find(3).ID,
                    LinkedS3Bucket = _context.S3Buckets.Find(3),
                    InitialIngest = true
                });
                _context.LogInputs.Add(new Models.LogInput
                {
                    ID = 3,
                    Name = "Windows Security Logs",
                    FirehoseStreamName = "SmartInsights-Windows-Security-Logs",
                    ConfigurationJSON = "",
                    LogInputCategory = Models.LogInputCategory.WindowsEventLogs,
                    LinkedUserID = 1,
                    LinkedS3BucketID = _context.S3Buckets.Find(4).ID,
                    LinkedS3Bucket = _context.S3Buckets.Find(4),
                    InitialIngest = true
                });
                _context.LogInputs.Add(new Models.LogInput
                {
                    ID = 4,
                    Name = "Squid Proxy Server Logs",
                    FirehoseStreamName = "SmartInsights-Cisco-Squid-Proxy-Logs",
                    ConfigurationJSON = "",
                    LogInputCategory = Models.LogInputCategory.SquidProxy,
                    LinkedUserID = 1,
                    LinkedS3BucketID = _context.S3Buckets.Find(5).ID,
                    LinkedS3Bucket = _context.S3Buckets.Find(5),
                    InitialIngest = true
                });
                await _context.SaveChangesAsync();
                _context.Database.ExecuteSqlCommand("SET IDENTITY_INSERT dbo.LogInputs OFF");
                _context.GlueConsolidatedEntities.Add(new Models.GlueConsolidatedEntity
                {
                    CrawlerName = "Apache Web Logs",
                    LinkedLogInputID = _context.LogInputs.Find(1).ID,
                    JobName = "Apache Web Logs"
                });
                _context.GlueConsolidatedEntities.Add(new Models.GlueConsolidatedEntity
                {
                    CrawlerName = "SSH Logs",
                    LinkedLogInputID = _context.LogInputs.Find(2).ID,
                    JobName = "SSH Logs"
                });
                _context.GlueConsolidatedEntities.Add(new Models.GlueConsolidatedEntity
                {
                    CrawlerName = "Windows Security Logs",
                    LinkedLogInputID = _context.LogInputs.Find(3).ID,
                    JobName = "Windows Security Logs"
                });
                _context.GlueConsolidatedEntities.Add(new Models.GlueConsolidatedEntity
                {
                    CrawlerName = "Squid Proxy Logs",
                    LinkedLogInputID = _context.LogInputs.Find(4).ID,
                    JobName = "Squid Proxy Logs"
                });
                await _context.SaveChangesAsync();
                _context.AlertTriggers.Add(new Models.Trigger
                {
                    Name = "Test Trigger",
                    AlertTriggerType = Models.AlertTriggerType.IPInsights,
                    CondtionalField = "request",
                    CondtionType = "Similar",
                    Condtion = "/login_success",
                    LinkedLogInputID = 1
                });
                _context.QuestionableEvents.Add(new Models.QuestionableEvent
                {
                    FullEventData = "GET /login_success HTTP/1.0",
                    UserField = "Hansen",
                    IPAddressField = "58.182.250.32",
                    EventTimestamp = DateTime.Now,
                    LinkedAlertTriggerID = 1,
                    ReviewUserID = 1
                });
                await _context.SaveChangesAsync();
            }
            _context.Database.CloseConnection();
        }
    }
}
