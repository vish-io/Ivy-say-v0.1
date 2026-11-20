using IvySay;
using Xunit;

public class FirstWordTimerTests
{
    [Fact] public void MeasuresFirstCompletedWordAcrossFragments()
    {
        TimeSpan now = TimeSpan.FromMilliseconds(100);
        var timer = new FirstWordTimer(() => now);
        foreach (char c in "  Hel") timer.Observe(c);
        now = TimeSpan.FromMilliseconds(300);
        foreach (char c in "lo ") timer.Observe(c);
        now = TimeSpan.FromMilliseconds(500);
        foreach (char c in "world") timer.Observe(c);
        timer.Complete();
        Assert.Equal(TimeSpan.FromMilliseconds(300), timer.FirstWord);
    }
    [Fact] public void SingleWordCompletesAtEndOfInput()
    {
        TimeSpan now = TimeSpan.FromMilliseconds(250);
        var timer = new FirstWordTimer(() => now);
        foreach (char c in "Hello") timer.Observe(c);
        Assert.Null(timer.FirstWord);
        now = TimeSpan.FromMilliseconds(400);
        timer.Complete();
        Assert.Equal(TimeSpan.FromMilliseconds(400), timer.FirstWord);
    }
    [Fact] public void NoWordDoesNotProduceAValue()
    {
        var timer = new FirstWordTimer(() => TimeSpan.FromSeconds(1));
        foreach (char c in " .!? ") timer.Observe(c);
        timer.Complete();
        Assert.Null(timer.FirstWord);
    }
}
