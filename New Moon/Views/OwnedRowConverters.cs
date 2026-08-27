using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace P3FESTrainer.Views
{
    public class ItemTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value as string) == "Item" ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
