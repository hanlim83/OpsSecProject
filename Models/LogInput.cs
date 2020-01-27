﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public enum LogInputCategory
    {
        SSH, ApacheWebServer, SquidProxy, WindowsEventLogs
    }
    public class LogInput
    {
        public int ID { get; set; }
        [Required]
        public string Name { get; set; }
        public LogInputCategory LogInputCategory { get; set; }
        public string ConfigurationJSON { get; set; }
        [Required]
        public bool InitialIngest { get; set; }
        [Required]
        public int LinkedUserID { get; set; }
        [Required]
        public int LinkedS3BucketID { get; set; }
        [Required]
        public virtual S3Bucket LinkedS3Bucket { get; set; }
        public virtual GlueConsolidatedEntity LinkedGlueEntity { get; set; }
        public virtual KinesisConsolidatedEntity LinkedKinesisConsolidatedEntity { get; set; }
        public virtual ICollection<SagemakerConsolidatedEntity> LinkedSagemakerEntities { get; set; }
    }
}
