using OpsSecProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
