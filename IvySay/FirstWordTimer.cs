using System.Globalization;

namespace IvySay;
// Receives characters only after SendInput has accepted their key events.
internal sealed class FirstWordTimer(Func<TimeSpan> elapsed)
{
    private bool inWord;
    public TimeSpan? FirstWord { get; private set; }
    public void Observe(char c)
    {
        if (FirstWord != null) return;
        var category = char.GetUnicodeCategory(c);
        bool wordChar = char.IsLetterOrDigit(c) || category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
        if (wordChar) inWord = true;
        else if (inWord && c is not ('\'' or '’' or '-')) FirstWord = elapsed();
    }
    public void Complete()
    {
        if (FirstWord == null && inWord) FirstWord = elapsed();
    }
}
