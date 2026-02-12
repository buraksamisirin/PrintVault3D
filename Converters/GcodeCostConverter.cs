using System.Globalization;
using System.Windows.Data;
using PrintVault3D.Models;

namespace PrintVault3D.Converters;

public class GcodeCostConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return string.Empty;

        if (values[0] is Gcode gcode && values[1] is decimal costPerKg)
        {
            var cost = gcode.CalculateEstimatedCost(costPerKg);
            if (cost.HasValue)
            {
                // Format explicitly as TL for Turkish context
                return $"{cost.Value:N2} ₺";
            }
        }

        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
