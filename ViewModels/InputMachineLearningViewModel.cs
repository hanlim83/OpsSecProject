using OpsSecProject.Models;
using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.ViewModels
{
    public enum AlgoritithmChoice
    {
        IPInsights,RandomCutForest
    }
    public class InputMachineLearningViewModel
    {
        public int LogInputID { get; set; }
        public string s3InputPath { get; set; }
        public string predictionInputContent { get; set; }
        public LogInput LogInput { get; set; }
        public AlgoritithmChoice algoritithmChoice { get; set; }
    }
}
