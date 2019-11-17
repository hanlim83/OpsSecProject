namespace OpsSecProject.Models
{
    public class ErrorViewModel
    {

        public string RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public string ErrorStatusCode { get; set; }

        public bool ShowStatusCode => !string.IsNullOrEmpty(ErrorStatusCode);

        public string OriginalPath { get; set; }
        public bool ShowOriginalPath => !string.IsNullOrEmpty(OriginalPath);

    }
}