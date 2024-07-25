using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FeiShuMinuteDownloader.models.MinuteListApiResponse;

namespace FeiShuMinuteDownloader.models
{
    public class Record
    {
        public int status { get; set; }
        public string owner_name { get; set; }
        public int object_type { get; set; }
        public int expire_time { get; set; }
        public bool is_encrypt_key_deleted { get; set; }
        public int review_status { get; set; }
        public int space_type { get; set; }
        public string video_cover { get; set; }
        public int duration { get; set; }
        public long share_time { get; set; }
        public string object_token { get; set; }
        public long owner_id { get; set; }
        public bool is_owner { get; set; }
        public int scheduler_type { get; set; }
        public bool is_local_tenant { get; set; }
        public long time { get; set; }
        public long scheduler_execute_timestamp { get; set; }
        public bool is_recording_device { get; set; }
        public string topic { get; set; }
        public int scheduler_execute_delta_time { get; set; }
        public bool is_risk { get; set; }
        public bool is_local_unit { get; set; }
        public long create_time { get; set; }
        public bool video_downloadable { get; set; }
        public bool show_external_tag { get; set; }
        public Display_Tag display_tag { get; set; }
        public string meeting_id { get; set; }
        public string url { get; set; }
        public long start_time { get; set; }
        public long stop_time { get; set; }
        public string media_type { get; set; }
        public long open_time { get; set; }
    }
}
