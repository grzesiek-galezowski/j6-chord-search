using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace J6ChordSearcher.Controls;

public class MiniKeyboard : Control
{
  public static readonly DependencyProperty NoteIndicesProperty =
    DependencyProperty.Register(
      nameof(NoteIndices),
      typeof(int[]),
      typeof(MiniKeyboard),
      new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

  public int[] NoteIndices
  {
    get => (int[])GetValue(NoteIndicesProperty);
    set => SetValue(NoteIndicesProperty, value);
  }

  protected override void OnRender(DrawingContext drawingContext)
  {
    base.OnRender(drawingContext);

    var width = ActualWidth;
    var height = ActualHeight;
    var keyWidth = width / 13.0;

    // Determine which notes are active
    var activeNotes = new HashSet<int>();
    if (NoteIndices != null)
    {
      foreach (var noteIndex in NoteIndices)
      {
        var normalized = ((noteIndex % 12) + 12) % 12;
        activeNotes.Add(normalized);
      }
    }

    // Black key positions (C#, D#, F#, G#, A#)
    int[] blackKeyPositions = { 1, 3, 6, 8, 10 };
    var blackKeySet = new HashSet<int>(blackKeyPositions);

    // Draw keys
    for (int i = 0; i < 13; i++)
    {
      var noteIndex = i % 12;
      var isBlack = blackKeySet.Contains(noteIndex);
      var isActive = activeNotes.Contains(noteIndex);

      var x = i * keyWidth;
      var rect = new Rect(x, 0, keyWidth, height);

      // Choose color
      Brush fillBrush;
      if (isActive)
      {
        fillBrush = new SolidColorBrush(Color.FromRgb(255, 102, 0)); // Orange
      }
      else if (isBlack)
      {
        fillBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)); // Dark gray
      }
      else
      {
        fillBrush = Brushes.White;
      }

      drawingContext.DrawRectangle(fillBrush, new Pen(Brushes.Gray, 0.5), rect);
    }
  }

  protected override Size MeasureOverride(Size constraint)
  {
    return new Size(130, 16.5);
  }
}
