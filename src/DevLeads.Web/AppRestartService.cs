using System.Diagnostics;

namespace DevLeads.Web;

/// <summary>
/// Full-process restart so the app picks up the latest code. Spawns a detached
/// supervisor script that waits for this process to exit, rebuilds the project, and
/// relaunches it with the same working directory and environment (env vars are
/// inherited, so URLs and connection strings carry over). If the rebuild fails, the
/// supervisor relaunches the previous binary unchanged so the app always comes back.
/// </summary>
public sealed class AppRestartService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IHostEnvironment _env;
    private readonly ILogger<AppRestartService> _log;

    public AppRestartService(IHostApplicationLifetime lifetime, IHostEnvironment env, ILogger<AppRestartService> log)
    {
        _lifetime = lifetime;
        _env = env;
        _log = log;
    }

    /// <summary>Schedules the restart. Returns an error message, or null when underway.</summary>
    public string? Restart()
    {
        if (OperatingSystem.IsWindows())
            return "Automatic restart is only supported on Linux/macOS hosts — restart the process manually.";

        var projectDir = _env.ContentRootPath;
        var hasProject = Directory.EnumerateFiles(projectDir, "*.csproj").Any();
        var logPath = Path.Combine(projectDir, "App_Data", "restart.log");

        // Relaunch the previous binary if the rebuild fails. When hosted as
        // `dotnet App.dll`, ProcessPath is the dotnet muxer — re-add the dll argument.
        var processPath = Environment.ProcessPath ?? "dotnet";
        var fallback = Path.GetFileNameWithoutExtension(processPath) == "dotnet"
            ? $"exec \"{processPath}\" \"{Environment.GetCommandLineArgs()[0]}\""
            : $"exec \"{processPath}\"";

        // If URLs come from the environment the app was started with --no-launch-profile;
        // otherwise let launchSettings apply again exactly like the original `dotnet run`.
        var noProfile = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"))
            ? "" : "--no-launch-profile ";

        var relaunch = hasProject
            ? $"""
               if dotnet build -v q >> "{logPath}" 2>&1; then
                 exec dotnet run --no-build {noProfile}>> "{logPath}" 2>&1
               else
                 echo "$(date -u) build FAILED — relaunching previous binary" >> "{logPath}"
                 {fallback} >> "{logPath}" 2>&1
               fi
               """
            : $"{fallback} >> \"{logPath}\" 2>&1"; // published: no csproj, plain relaunch

        var script = $"""
            echo "$(date -u) restart requested (pid {Environment.ProcessId})" >> "{logPath}"
            for i in $(seq 1 240); do kill -0 {Environment.ProcessId} 2>/dev/null || break; sleep 0.5; done
            cd "{projectDir}" || exit 1
            {relaunch}
            """;

        try
        {
            // setsid detaches the supervisor from our session so it survives this
            // process's exit (and any SIGHUP sent to our process group).
            var psi = new ProcessStartInfo
            {
                FileName = "setsid",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };
            psi.ArgumentList.Add("bash");
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(script);
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to spawn restart supervisor");
            return "Could not start the restart supervisor: " + ex.Message;
        }

        _log.LogWarning("Restart requested — shutting down for supervised relaunch.");

        // Give the UI response / circuit a moment to flush before stopping the host.
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            _lifetime.StopApplication();
        });

        return null;
    }
}
