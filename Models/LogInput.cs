﻿using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public class LogInput
    {
        public int ID { get; set; }
        [Required]
        public string Name { get; set; }
        public string FilePath { get; set; }
        // public string Pattern {get; set;}
        // public string {get; set;}
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
        public virtual SagemakerConsolidatedEntity LinkedSagemakerEntity { get; set; }
    }
}
