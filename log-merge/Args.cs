﻿using System;
using System.Collections.Generic;
using System.Text;

namespace log_merge
{
    public class Args
    {
        public List<string> LogFilenames { get; set; }
        public string StartPattern { get; set; }
        public bool NoColor { get; set; }
        public bool ShowHelp { get; set; }
    }
}
