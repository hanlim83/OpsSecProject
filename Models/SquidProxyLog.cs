namespace OpsSecProject.Models
{
    public class SquidProxyLog
    {
        public string timestamp { get; set; }
        public string destination_ip_address { get; set; }
        public string action { get; set; }
        public string http_status_Code { get; set; }
        public string bytes_in { get; set; }
        public string http_method { get; set; }
        public string requested_url { get; set; }
        public string user { get; set; }
        public string requested_url_domain { get; set; }
        public string content_type { get; set; }
        public string COUNT { get; set; }

    }


}
