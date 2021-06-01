using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace BaseModel.Misc
{
    public class ConditionToStyleConverter : IValueConverter
    {
        public Style InstantFeedbackStyle { get; set; }
        public Style DefaultStyle { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? InstantFeedbackStyle : DefaultStyle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new InvalidOperationException();
        }
    }
}
