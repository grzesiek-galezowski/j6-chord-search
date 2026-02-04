using J6ChordSearcher.NChord;

namespace J6ChordSearcher.ViewModels;

public class ChordKeyboardViewModel
{
  public string ChordName { get; set; }
  public int[] NoteIndices { get; set; }

  public ChordKeyboardViewModel(string chordName)
  {
    ChordName = chordName;
    NoteIndices = ExtractNoteIndices(chordName);
  }

  private static int[] ExtractNoteIndices(string chordName)
  {
    try
    {
      var chord = new Chord(chordName);
      var noteIndices = new List<int>();

      // Get all notes from all qualities in the chord
      foreach (var quality in chord.Qualities)
      {
        var components = quality.GetComponents(chord.Root);
        noteIndices.AddRange(components);
      }

      // Normalize to 0-11 range and remove duplicates
      return noteIndices
        .Select(n => ((n % 12) + 12) % 12)
        .Distinct()
        .OrderBy(n => n)
        .ToArray();
    }
    catch
    {
      // If parsing fails, return empty array
      return Array.Empty<int>();
    }
  }
}
