using Microsoft.Extensions.Logging;

namespace OpsSecProject.Models
{
    public class ErrorViewModel
    {

        public string RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public string ErrorStatusCode { get; set; }

        public bool ShowStatusCode => !string.IsNullOrEmpty(ErrorStatusCode);

        public string OriginalURL { get; set; }
        public bool ShowOriginalURL => !string.IsNullOrEmpty(OriginalURL);

    }
}