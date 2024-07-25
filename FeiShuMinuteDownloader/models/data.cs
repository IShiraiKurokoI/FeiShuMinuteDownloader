using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FeiShuMinuteDownloader.models.MinuteListApiResponse;

namespace FeiShuMinuteDownloader.models
{

    public class Data
    {
        public string timestamp { get; set; }
        public int size { get; set; }
        public bool has_more { get; set; }
        public bool has_delete_tag { get; set; }
        public Record[] list { get; set; }
    }
}
