namespace SysGlance.Services;

/// <summary>
/// What the Settings window needs from the updater (owned by MonitorWindow).
/// </summary>
internal sealed record UpdateHooks(
    Func<UpdateService.UpdateInfo?> Available,
    Func<Task> CheckNow,
    Action ShowWindow);
