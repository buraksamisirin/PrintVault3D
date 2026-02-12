using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PrintVault3D.Converters;

/// <summary>
/// Converts a collection and an item to check if the item is in the collection.
/// </summary>
public class IsSelectedConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            return false;

        if (values[0] is IList collection && values[1] is not null)
        {
            return collection.Contains(values[1]);
        }

        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean to Visibility (for selection overlay).
/// </summary>
public class SelectionToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            return Visibility.Collapsed;

        if (values[0] is IList collection && values[1] is not null)
        {
            return collection.Contains(values[1]) ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// General-purpose Bool -> Visibility converter with support for
/// "truthy" values (bool, number, string, collection, etc.)
/// and optional inversion via ConverterParameter ("Inverse" or "Not").
/// Used as the global "BoolToVis" resource in XAML.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isTrue = value switch
        {
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            double d => Math.Abs(d) > double.Epsilon,
            float f => Math.Abs(f) > float.Epsilon,
            string s => !string.IsNullOrWhiteSpace(s),
            ICollection collection => collection.Count > 0,
            _ => value is not null
        };

        if (parameter is string p &&
            (string.Equals(p, "Inverse", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(p, "Not", StringComparison.OrdinalIgnoreCase)))
        {
            isTrue = !isTrue;
        }

        return isTrue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (targetType != typeof(bool))
            throw new NotSupportedException("BoolToVisibilityConverter only supports ConvertBack to bool.");

        return value is Visibility visibility && visibility == Visibility.Visible;
    }
}

/// <summary>
/// Compares two values for equality. Used for selection highlighting.
/// </summary>
public class EqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return false;

        if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            return false;

        return Equals(values[0], values[1]);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts null/empty string to Visibility.Collapsed, otherwise Visible.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNull = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
        
        // If parameter is "Inverse", show when null
        if (parameter is string p && string.Equals(p, "Inverse", StringComparison.OrdinalIgnoreCase))
        {
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a hex color string to SolidColorBrush.
/// </summary>
public class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hexColor && !string.IsNullOrEmpty(hexColor))
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
                return new System.Windows.Media.SolidColorBrush(color);
            }
            catch
            {
                // Return default color on parse error
            }
        }
        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
