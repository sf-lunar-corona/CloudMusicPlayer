using System.Globalization;

namespace CloudMusicPlayer.Converters;

public class TimeSpanToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan timeSpan)
        {
            return timeSpan.TotalHours >= 1
                ? timeSpan.ToString(@"h\:mm\:ss")
                : timeSpan.ToString(@"m\:ss");
        }
        return "0:00";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
