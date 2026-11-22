namespace IvySay;

// Send a completed text in one submission. The OS can accept only part of it;
// never retry automatically because that could duplicate text or execute commands.
internal static class BatchInsertion
{
    public static void Send(string text, Action validate, Func<string, uint> sendEvents, Action<char>? onAccepted = null)
    {
        validate();
        if (text.Length == 0) return;
        uint expected = checked((uint)text.Length * 2);
        uint accepted = sendEvents(text);
        if (accepted != expected) throw new InvalidOperationException("Windows did not accept the whole insertion. Some text may have been inserted; check the target before retrying.");
        if (onAccepted != null) foreach (char c in text) onAccepted(c);
    }
}
