using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PrintVault3D.Converters
{
    /// <summary>
    /// Converts category index to a distinct color from the predefined palette.
    /// Creates visual distinction between different categories in the UI.
    /// </summary>
    public class CategoryColorConverter : IValueConverter
    {
        // Predefined color palette matching Colors.xaml CategoryColors
        private static readonly System.Windows.Media.Color[] CategoryColors =
        {
            System.Windows.Media.Color.FromRgb(0x39, 0xD0, 0xD8), // Cyan
            System.Windows.Media.Color.FromRgb(0xA3, 0x71, 0xF7), // Purple
            System.Windows.Media.Color.FromRgb(0x3F, 0xB9, 0x50), // Green
            System.Windows.Media.Color.FromRgb(0xF7, 0x81, 0x66), // Orange
            System.Windows.Media.Color.FromRgb(0x58, 0xA6, 0xFF), // Blue
            System.Windows.Media.Color.FromRgb(0xF0, 0xC1, 0x4B), // Yellow
            System.Windows.Media.Color.FromRgb(0xDB, 0x61, 0xA2), // Pink
            System.Windows.Media.Color.FromRgb(0xF8, 0x51, 0x49), // Red
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int index = 0;
            
            if (value is int intValue)
            {
                index = intValue;
            }
            else if (value is string strValue)
            {
                // Use string hash to generate consistent color for category names
                index = Math.Abs(strValue.GetHashCode());
            }
            
            var color = CategoryColors[index % CategoryColors.Length];
            return new SolidColorBrush(color);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
