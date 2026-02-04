namespace J6ChordSearcher.ViewModels;

public class KeyboardKey
{
  public int KeyIndex { get; set; }
  public string ChordName { get; set; }
  public bool IsBlackKey { get; set; }
}

public class KeyboardMappingViewModel
{
  public List<KeyboardKey> Keys { get; set; }

  public KeyboardMappingViewModel(List<string> chords)
  {
    Keys = new List<KeyboardKey>();
    
    // Black key positions (indices 1, 3, 6, 8, 10 in the chord list map to black keys)
    var blackKeyIndices = new HashSet<int> { 1, 3, 6, 8, 10 };

    // Map 12 chords to 13 keys (first chord repeats on key 13)
    for (int i = 0; i < 13; i++)
    {
      var chordIndex = i % 12;
      Keys.Add(new KeyboardKey
      {
        KeyIndex = i,
        ChordName = chords[chordIndex],
        IsBlackKey = blackKeyIndices.Contains(chordIndex)
      });
    }
  }
}
