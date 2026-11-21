using IvySay;
using Xunit;

public class SettingsTargetGuardTests
{
    [Fact] public void ExternalTargetDoesNotDependOnSettingsWindow()
    {
        SettingsTargetGuard.Validate(null, null, false, false);
        SettingsTargetGuard.Validate(null, new object(), true, true);
    }
    [Fact] public void SettingsTargetRequiresSameActiveVisibleEditor()
    {
        var editor = new object();
        SettingsTargetGuard.Validate(editor, editor, true, true);
        Assert.Throws<InvalidOperationException>(() => SettingsTargetGuard.Validate(editor, new object(), true, true));
        Assert.Throws<InvalidOperationException>(() => SettingsTargetGuard.Validate(editor, editor, false, true));
        Assert.Throws<InvalidOperationException>(() => SettingsTargetGuard.Validate(editor, editor, true, false));
        Assert.Throws<InvalidOperationException>(() => SettingsTargetGuard.Validate(editor, null, true, true));
    }
}
