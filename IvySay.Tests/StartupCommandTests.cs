using IvySay;
using Xunit;

public class StartupCommandTests
{
    [Fact] public void EnabledForNewAndExistingPreferences()
    {
        Assert.True(new Preferences().StartOnLogin);
        Assert.True(Preferences.ParseExisting("{}").StartOnLogin);
        Assert.False(Preferences.ParseExisting("{\"StartOnLogin\":false}").StartOnLogin);
    }
    [Fact] public void QuotesAbsoluteExecutableAndUsesOnlyStartupArgument()
    {
        string path = Path.Combine(Path.GetFullPath("test directory"), "IvySay.exe");
        Assert.Equal("\"" + path + "\" --startup", StartupCommand.ForExecutable(path));
    }
    [Fact] public void RejectsHostAndMalformedPaths()
    {
        Assert.Null(StartupCommand.ForExecutable(null));
        Assert.Null(StartupCommand.ForExecutable("IvySay.exe"));
        Assert.Null(StartupCommand.ForExecutable(Path.GetFullPath("dotnet.exe")));
        Assert.Null(StartupCommand.ForExecutable(Path.GetFullPath("IvySay.exe") + "\" --malicious"));
        Assert.Null(StartupCommand.ForExecutable(Path.GetFullPath("IvySay.exe") + "\n"));
    }
}
