namespace DailyWingetNotify.Services;

internal static class AppPaths
{
    private const string AppDirectoryName = "DailyWingetNotify";

    public static string StateFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppDirectoryName, "state.json");
}

