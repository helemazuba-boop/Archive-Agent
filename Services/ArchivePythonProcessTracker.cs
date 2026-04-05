using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace ArchiveAgent.Services;

internal static class ArchivePythonProcessTracker
{
    private static readonly ConcurrentDictionary<int, byte> RunningProcessIds = new();

    private sealed record TrackedProcessSnapshot(int ProcessId, long StartTimeUtcTicks);

    public static void Register(Process process, string? snapshotPath = null)
    {
        try
        {
            RunningProcessIds.TryAdd(process.Id, 0);
            SaveSnapshot(process, snapshotPath);
        }
        catch
        {
        }
    }

    public static void Unregister(Process process, string? snapshotPath = null)
    {
        try
        {
            RunningProcessIds.TryRemove(process.Id, out _);
            TryDeleteSnapshotIfMatches(process, snapshotPath);
        }
        catch
        {
        }
    }

    public static void CleanupPersistedProcess(string? snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath) || !File.Exists(snapshotPath))
        {
            return;
        }

        try
        {
            var snapshot = ReadSnapshot(snapshotPath);
            if (snapshot == null)
            {
                TryDeleteSnapshot(snapshotPath);
                return;
            }

            using var process = Process.GetProcessById(snapshot.ProcessId);
            if (process.HasExited)
            {
                TryDeleteSnapshot(snapshotPath);
                return;
            }

            if (process.StartTime.ToUniversalTime().Ticks != snapshot.StartTimeUtcTicks)
            {
                TryDeleteSnapshot(snapshotPath);
                return;
            }

            process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
        finally
        {
            TryDeleteSnapshot(snapshotPath);
        }
    }

    public static void CleanupTrackedProcesses()
    {
        var ids = RunningProcessIds.Keys.ToList();
        foreach (var processId in ids)
        {
            try
            {
                if (!RunningProcessIds.TryRemove(processId, out _))
                {
                    continue;
                }

                using var process = Process.GetProcessById(processId);
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            catch (NotSupportedException)
            {
            }
        }
    }

    private static void SaveSnapshot(Process process, string? snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath))
        {
            return;
        }

        try
        {
            var snapshot = new TrackedProcessSnapshot(process.Id, process.StartTime.ToUniversalTime().Ticks);
            var directory = Path.GetDirectoryName(snapshotPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(snapshotPath, JsonSerializer.Serialize(snapshot));
        }
        catch
        {
        }
    }

    private static TrackedProcessSnapshot? ReadSnapshot(string snapshotPath)
    {
        try
        {
            return JsonSerializer.Deserialize<TrackedProcessSnapshot>(File.ReadAllText(snapshotPath));
        }
        catch
        {
            return null;
        }
    }

    private static void TryDeleteSnapshotIfMatches(Process process, string? snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath) || !File.Exists(snapshotPath))
        {
            return;
        }

        try
        {
            var snapshot = ReadSnapshot(snapshotPath);
            if (snapshot == null || snapshot.ProcessId == process.Id)
            {
                TryDeleteSnapshot(snapshotPath);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteSnapshot(string? snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath))
        {
            return;
        }

        try
        {
            if (File.Exists(snapshotPath))
            {
                File.Delete(snapshotPath);
            }
        }
        catch
        {
        }
    }
}
