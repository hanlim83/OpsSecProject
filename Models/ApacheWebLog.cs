﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.Models
{
    public class ApacheWebLog
    {
        public string host { get; set; }
        public string ident { get; set; }
        public string authuser { get; set; }
        public string datetime { get; set; }
        public string request { get; set; }
        public string response { get; set; }
        public int bytes { get; set; }
        public string referer { get; set; }
        public string agent { get; set; }
        public string COUNT { get; set; }

    }


}
