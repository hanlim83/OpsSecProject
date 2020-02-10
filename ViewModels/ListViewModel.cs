using OpsSecProject.Models;
using System.Collections.Generic;

namespace OpsSecProject.ViewModels
{
    public class ListViewModel
    {
        public List<ApacheWebLog> results { get; set; }
        public List<ApacheWebLog> charts { get; set; }
        public List<ApacheWebLog> count { get; set; }
        public List<ApacheWebLog> groupBy { get; set; }


    }
}
