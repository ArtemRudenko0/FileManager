using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace FileManager
{
    internal class SpaceToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is long freeSpace && values[1] is long totalSize)
            {
                if (totalSize == 0) return 0;
                double usedSpacePercentage = (double)(totalSize - freeSpace) / totalSize; // Об'єм використаного місця
                return 200 * usedSpacePercentage; // Ширина прогресу на основі проценту
            }
            return 0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
