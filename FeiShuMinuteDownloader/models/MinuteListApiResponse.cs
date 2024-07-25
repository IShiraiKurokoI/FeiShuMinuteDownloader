using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeiShuMinuteDownloader.models
{
    public class MinuteListApiResponse
    {
        public int code { get; set; }
        public string msg { get; set; }
        public data data { get; set; }
    }
}
