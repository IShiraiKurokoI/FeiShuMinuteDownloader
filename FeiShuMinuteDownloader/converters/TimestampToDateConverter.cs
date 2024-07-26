using System;
using Microsoft.UI.Xaml.Data;

namespace FeiShuMinuteDownloader.converters
{
    public class TimestampToDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is long timestamp)
            {
                // 假设时间戳是秒级别的，如果是毫秒级别的请将DateTimeOffset.FromUnixTimeSeconds改为DateTimeOffset.FromUnixTimeMilliseconds
                var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                return dateTime.ToString("MM月dd日 HH:mm:ss");
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string dateTimeString && DateTime.TryParse(dateTimeString, out DateTime dateTime))
            {
                var offset = new DateTimeOffset(dateTime);
                return offset.ToUnixTimeSeconds();
            }
            return value;
        }
    }
}
