using System.Collections.Generic;

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

        public readonly Dictionary<string, string> ErrorStatusCodeMessage = new Dictionary<string, string>
        {
            { "400", "You submitted a bad request" },
            { "401", "You need to logged in to view this page" },
            { "402", "Payment Required" },
            { "403", "You don't have permission to view this page" },
            { "404", "You requested something that doesn't exists" },
            { "405", "HTTP Method not allowed" },
            { "406", "Your request contains bad information that is not acceptable" },
            { "407", "Proxy Authentication Requried" },
            { "408", "Your request timed-out" },
            { "409", "Your request can't be processed right now" },
            { "410", "You requested something that doesn't exists anymore" },
            { "411", "Length Required" },
            { "412", "Your request is missing some conditions" },
            { "413", "Your request's payload is too large" },
            { "414", "Your request's URI is too large" },
            { "415", "Unsupported media type" },
            { "416", "Requested Range Not Satisfiable" },
            { "417", "Expectation Failed" },
            { "418", "I'm a teapot" },
            { "421", "You sent a misdirected request" },
            { "422", "Unprocessable Entity" },
            { "423", "Locked" },
            { "424", "Failed Dependency" },
            { "425", "Too Early" },
            { "426", "Upgrade Required" },
            { "428", "Your request can't be correlated" },
            { "429", "You sent too many requests" },
            { "431", "Your request is too large" },
            { "451", "Unavailable For Legal Reasons" },
            { "500", "Something went wrong on our end" },
            { "501", "Not Implemented" },
            { "502", "Bad Gateway" },
            { "503", "We can't handle this at the moment" },
            { "504", "Gateway Timeout" },
            { "505", "You sent an request using the wrong version of HTTP" },
            { "506", "Variant Also Negotiates" },
            { "507", "Insufficient Storage" },
            { "508", "Loop Detected" },
            { "510", "Not Extended" },
            { "511", "Network Authentication Required" }
        };
    }
}