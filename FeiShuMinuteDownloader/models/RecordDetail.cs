using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeiShuMinuteDownloader.models
{
    public class RecordDetail
    {
        public int code { get; set; }
        public string msg { get; set; }
        public Data data { get; set; }

        public class Data
        {
            public int deleted_by_admin { get; set; }
            public int timeaxis_status { get; set; }
            public bool can_create_clip { get; set; }
            public int summary_status { get; set; }
            public int speaker_ai_status { get; set; }
            public Export_Options export_options { get; set; }
            public bool can_comment { get; set; }
            public Ws_Config ws_config { get; set; }
            public int scheduler_execute_delta_time { get; set; }
            public int last_edit_version { get; set; }
            public int agenda_status { get; set; }
            public bool is_ai_analyst_summary { get; set; }
            public Diarization_Options diarization_options { get; set; }
            public int object_version { get; set; }
            public int object_status { get; set; }
            public Download_Options download_options { get; set; }
            public int scheduler_type { get; set; }
            public int review_status { get; set; }
            public Video_Info video_info { get; set; }
        }

        public class Export_Options
        {
            public bool enable { get; set; }
            public string status { get; set; }
            public string tip { get; set; }
        }

        public class Ws_Config
        {
            public int http_interval { get; set; }
            public bool ws_enable { get; set; }
            public int heartbeat_interval { get; set; }
        }

        public class Diarization_Options
        {
            public int percent { get; set; }
            public int left_min { get; set; }
            public string tip { get; set; }
            public string editor_name { get; set; }
            public int block_clear_date { get; set; }
            public int status { get; set; }
            public int left_second { get; set; }
            public string trigger_user_id { get; set; }
            public string editor_uid { get; set; }
            public bool dira_rematch_block { get; set; }
        }

        public class Download_Options
        {
            public string tip { get; set; }
            public bool enable { get; set; }
            public string status { get; set; }
        }

        public class Video_Info
        {
            public string vid { get; set; }
            public string video_cover { get; set; }
            public string video_download_url { get; set; }
            public string video_url { get; set; }
            public string audio_url { get; set; }
        }
    }

}
