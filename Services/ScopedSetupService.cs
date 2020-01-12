using Amazon.Glue;
using Amazon.Glue.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsSecProject.Data;
using System.Collections.Generic;
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
            bool aggergateBucketFound = false, sageMakerBucketFound = false;
            foreach (var bucket in listBucketResponse.Buckets)
            {
                if (bucket.BucketName.Equals("master-aggergated-ingest-data"))
                {
                    aggergateBucketFound = true;
                    if (_context.S3Buckets.Find(1) == null)
                        _context.S3Buckets.Add(new Models.S3Bucket
                        {
                            ID = 1,
                            Name = "master-aggergated-ingest-data"
                        });
                }
                if (bucket.BucketName.Equals("master-sagemaker-data"))
                {
                    sageMakerBucketFound = true;
                    if (_context.S3Buckets.Find(2) == null)
                        _context.S3Buckets.Add(new Models.S3Bucket
                        {
                            ID = 2,
                            Name = "master-sagemaker-data"
                        });
                }
                if (aggergateBucketFound && sageMakerBucketFound)
                    break;
            }
            if (!aggergateBucketFound && _context.S3Buckets.Find(1) == null)
            {
                PutBucketResponse putBucketResponse1 = await _S3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = "master-aggergated-ingest-data",
                    UseClientRegion = true,
                    CannedACL = S3CannedACL.Private
                });
                PutBucketTaggingResponse putBucketTaggingResponse1 = await _S3Client.PutBucketTaggingAsync(new PutBucketTaggingRequest
                {
                    BucketName = "master-aggergated-ingest-data",
                    TagSet = new List<Tag>
                    {
                        new Tag
                        {
                            Key = "Project",
                            Value = "OSPJ"
                        }
                    }
                });
                if (putBucketResponse1.HttpStatusCode.Equals(HttpStatusCode.OK) && putBucketTaggingResponse1.HttpStatusCode.Equals(HttpStatusCode.OK))
                    _context.S3Buckets.Add(new Models.S3Bucket
                    {
                        ID = 1,
                        Name = "master-aggergated-ingest-data"
                    });
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
                if (putBucketResponse2.HttpStatusCode.Equals(HttpStatusCode.OK) && putBucketTaggingResponse2.HttpStatusCode.Equals(HttpStatusCode.OK))
                    _context.S3Buckets.Add(new Models.S3Bucket
                    {
                        ID = 2,
                        Name = "master-sagemaker-data"
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
                _context.Database.CloseConnection();
            }
        }
    }
}
