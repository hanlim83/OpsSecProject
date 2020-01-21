using OpsSecProject.Models;
using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Areas.Development.Models
{
    public enum AlgoritithmChoice
    {
        IPInsights,RandomCutForest
    }
    public class MachineLearningViewModel
    {
        public string s3InputPath { get; set; }
        public string predictionInputContent { get; set; }
        public LogInput LogInput { get; set; }
        public AlgoritithmChoice algoritithmChoice { get; set; }
    }
}
