using System.ComponentModel;
using System.Runtime.CompilerServices;
using J6ChordSearcher.Logic;

namespace J6ChordSearcher.ViewModels;

public class ProgressionViewModel : INotifyPropertyChanged
{
  private int _transposition;
  private List<string> _displayedChords;

  public ChordSet BaseProgression { get; }

  public int Transposition
  {
    get => _transposition;
    set
    {
      if (_transposition != value)
      {
        _transposition = value;
        OnPropertyChanged();
        UpdateDisplayedChords();
      }
    }
  }

  public List<string> DisplayedChords
  {
    get => _displayedChords;
    private set
    {
      _displayedChords = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(DisplayText));
    }
  }

  public string DisplayText
  {
    get
    {
      var chords = string.Join(", ", DisplayedChords.Select((c, i) => new List<int> { 1, 3, 6, 8, 10 }.Contains(i) ? $"[{c}]" : c));
      return $"{BaseProgression.Number}: {BaseProgression.Name} +{Transposition}: {chords}";
    }
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ProgressionViewModel(ChordSet baseProgression)
  {
    BaseProgression = baseProgression;
    _transposition = 0;
    _displayedChords = baseProgression.Chords.ToList();
  }

  private void UpdateDisplayedChords()
  {
    DisplayedChords = BaseProgression.Chords.Select(chord => TransposeChord(chord, Transposition)).ToList();
  }

  private string TransposeChord(string chord, int semitones)
  {
    var c = new NChord.Chord(chord);
    c.Transpose(semitones, "C#");
    return c.ChordName;
  }

  protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
