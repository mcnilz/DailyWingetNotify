using DailyWingetNotify.Services;
using DailyWingetNotify.UI;

namespace DailyWingetNotify;

internal static class Program
{
    private const string MutexName = "DailyWingetNotify.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            return;
        }

        var stateStore = new StateStore(AppPaths.StateFilePath);
        var wingetUpdateService = new WingetUpdateService();
        var autostartService = new AutostartService(Environment.ProcessPath ?? AppContext.BaseDirectory);
        var systemLoadService = new SystemLoadService();
        var userPresenceService = new UserPresenceService();
        var scheduler = new DailyCheckScheduler(stateStore, systemLoadService);

        using var app = new TrayApplication(stateStore, wingetUpdateService, autostartService, scheduler, userPresenceService);
        app.Run();
    }
}
