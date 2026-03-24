using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Principal;
using Microsoft.Win32;

namespace SuperVPNSingle;

internal static class Program
{
    private const string AppName = "SuperVPN";
    private const string AppExeName = "super_vpn.exe";
    private const string LauncherExeName = "SuperVPN_Single.exe";
    private const string UninstallerExeName = "uninstall.exe";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SuperVPN";

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--uninstall", StringComparison.OrdinalIgnoreCase))
        {
            UninstallCurrentApp();
            return;
        }

        string appDir = ResolveInstallDirectory();
        string appExe = Path.Combine(appDir, AppExeName);

        if (!File.Exists(appExe))
        {
            Directory.CreateDirectory(appDir);
            ExtractEmbeddedPayload(appDir);
        }

        EnsureLocalLauncherFiles(appDir);
        CreateOrUpdateShortcuts(appDir, appExe);
        WriteUninstallEntry(appDir);

        if (File.Exists(appExe))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = appExe,
                WorkingDirectory = appDir,
                UseShellExecute = true
            });
        }
    }

    private static void UninstallCurrentApp()
    {
        string installDir = ResolveInstallDirectory();

        RemoveShortcuts();
        RemoveUninstallEntry();

        try
        {
            if (Directory.Exists(installDir))
            {
                foreach (string filePath in Directory.GetFiles(installDir, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                }
            }
        }
        catch
        {
            // Best-effort cleanup before delayed removal.
        }

        string cmdFile = Path.Combine(Path.GetTempPath(), $"{AppName}_cleanup_{Guid.NewGuid():N}.cmd");
        string cmdText = string.Join(Environment.NewLine,
            "@echo off",
            "ping 127.0.0.1 -n 3 > nul",
            $"taskkill /F /IM {AppExeName} >nul 2>nul",
            $"rmdir /S /Q \"{installDir}\"",
            "del /F /Q \"%~f0\"");

        File.WriteAllText(cmdFile, cmdText);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{cmdFile}\"",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    private static string ResolveInstallDirectory()
    {
        string? existing = GetExistingInstallLocation();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        if (IsElevated())
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return Path.Combine(programFiles, AppName);
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, AppName);
    }

    private static string? GetExistingInstallLocation()
    {
        foreach (RegistryKey root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using RegistryKey? key = root.OpenSubKey(UninstallKeyPath);
            string? location = key?.GetValue("InstallLocation") as string;
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }
        }

        return null;
    }

    private static bool IsElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void EnsureLocalLauncherFiles(string appDir)
    {
        string? currentExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
        {
            return;
        }

        try
        {
            string launcherCopyPath = Path.Combine(appDir, LauncherExeName);
            if (!Path.GetFullPath(currentExe).Equals(Path.GetFullPath(launcherCopyPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(currentExe, launcherCopyPath, overwrite: true);
            }

            string uninstallerPath = Path.Combine(appDir, UninstallerExeName);
            File.Copy(launcherCopyPath, uninstallerPath, overwrite: true);
        }
        catch
        {
            // Ignore copy failures (for example, missing elevation in Program Files).
        }
    }

    private static void WriteUninstallEntry(string appDir)
    {
        bool machineInstall = appDir.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase);
        RegistryKey root = machineInstall ? Registry.LocalMachine : Registry.CurrentUser;

        try
        {
            using RegistryKey key = root.CreateSubKey(UninstallKeyPath);

            string appExe = Path.Combine(appDir, AppExeName);
            string uninstallerExe = Path.Combine(appDir, UninstallerExeName);
            string uninstallCmd = $"\"{uninstallerExe}\" --uninstall";

            key.SetValue("DisplayName", AppName);
            key.SetValue("Publisher", AppName);
            key.SetValue("DisplayVersion", "1.0");
            key.SetValue("InstallLocation", appDir);
            key.SetValue("DisplayIcon", appExe);
            key.SetValue("UninstallString", uninstallCmd);
            key.SetValue("QuietUninstallString", uninstallCmd);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        catch
        {
            // Ignore registry write errors to keep app launch functional.
        }
    }

    private static void RemoveUninstallEntry()
    {
        foreach (RegistryKey root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                root.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
            }
            catch
            {
                // Ignore registry cleanup errors to keep uninstall resilient.
            }
        }
    }

    private static void CreateOrUpdateShortcuts(string appDir, string appExe)
    {
        string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SuperVPN.lnk");
        string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "SuperVPN");
        string startMenuShortcut = Path.Combine(startMenuDir, "SuperVPN.lnk");
        string uninstallShortcut = Path.Combine(startMenuDir, "Uninstall SuperVPN.lnk");

        Directory.CreateDirectory(startMenuDir);

        CreateShortcut(desktopShortcut, appExe, appDir, appExe);
        CreateShortcut(startMenuShortcut, appExe, appDir, appExe);
        CreateShortcut(uninstallShortcut, Path.Combine(appDir, UninstallerExeName), appDir, appExe, "--uninstall");
    }

    private static void RemoveShortcuts()
    {
        string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SuperVPN.lnk");
        string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "SuperVPN");

        TryDeleteFile(desktopShortcut);
        TryDeleteFile(Path.Combine(startMenuDir, "SuperVPN.lnk"));
        TryDeleteFile(Path.Combine(startMenuDir, "Uninstall SuperVPN.lnk"));

        try
        {
            if (Directory.Exists(startMenuDir) && Directory.GetFileSystemEntries(startMenuDir).Length == 0)
            {
                Directory.Delete(startMenuDir);
            }
        }
        catch
        {
            // Ignore shortcut folder cleanup errors.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string iconPath, string? arguments = null)
    {
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return;
        }

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.IconLocation = iconPath;
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            shortcut.Arguments = arguments;
        }
        shortcut.Save();
    }

    private static void ExtractEmbeddedPayload(string destinationDirectory)
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PayloadZip");
        if (stream is null)
        {
            throw new InvalidOperationException("Embedded payload not found.");
        }

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                string dirPath = Path.Combine(destinationDirectory, entry.FullName);
                Directory.CreateDirectory(dirPath);
                continue;
            }

            string targetPath = Path.Combine(destinationDirectory, entry.FullName);
            string? targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }
}
