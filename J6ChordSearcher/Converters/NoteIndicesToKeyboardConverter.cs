using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using J6ChordSearcher.ViewModels;

namespace J6ChordSearcher.Converters;

public class KeyboardMappingConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if (value is not KeyboardMappingViewModel mapping)
      return new Canvas();

    var canvas = new Canvas
    {
      Width = 400,
      Height = 53,
      Background = new SolidColorBrush(Color.FromRgb(26, 26, 26))
    };

    var keyWidth = 400.0 / 13.0;

    // Draw all keys
    foreach (var key in mapping.Keys)
    {
      var x = key.KeyIndex * keyWidth;

      // Key background
      var keyBorder = new Border
      {
        Width = keyWidth,
        Height = 53,
        BorderBrush = Brushes.Black,
        BorderThickness = new Thickness(1),
        Background = key.IsBlackKey 
          ? new SolidColorBrush(Color.FromRgb(60, 60, 60)) 
          : Brushes.White
      };

      // Chord name on key
      var textBlock = new TextBlock
      {
        Text = key.ChordName,
        Foreground = key.IsBlackKey ? Brushes.White : Brushes.Black,
        FontSize = 9,
        FontWeight = FontWeights.Bold,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(2)
      };

      keyBorder.Child = textBlock;
      Canvas.SetLeft(keyBorder, x);
      canvas.Children.Add(keyBorder);
    }

    return canvas;
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
