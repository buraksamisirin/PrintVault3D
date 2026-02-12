using System;
using System.Globalization;
using System.Windows.Data;

namespace PrintVault3D.Converters;

public class TicksToTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long ticks)
        {
            if (ticks == 0) return "-";
            
            var timeSpan = TimeSpan.FromTicks(ticks);
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
            }
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        }
        return "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
