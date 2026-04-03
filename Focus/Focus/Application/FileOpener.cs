#nullable enable

using System;
using System.Diagnostics;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;

namespace Systems.Sanity.Focus.Application;

internal interface IFileOpener
{
    bool TryOpen(string filePath, out string? errorMessage);
}

internal sealed class DefaultFileOpener : IFileOpener
{
    public bool TryOpen(string filePath, out string? errorMessage)
    {
        try
        {
            ProcessStartInfo startInfo;
            if (OperatingSystem.IsWindows())
            {
                startInfo = new ProcessStartInfo(filePath)
                {
                    UseShellExecute = true
                };
            }
            else if (OperatingSystem.IsMacOS())
            {
                startInfo = new ProcessStartInfo("open")
                {
                    UseShellExecute = false
                };
                startInfo.ArgumentList.Add(filePath);
            }
            else if (OperatingSystem.IsLinux())
            {
                startInfo = new ProcessStartInfo("xdg-open")
                {
                    UseShellExecute = false
                };
                startInfo.ArgumentList.Add(filePath);
            }
            else
            {
                errorMessage = "Opening attachments is not supported on this operating system.";
                return false;
            }

            var process = Process.Start(startInfo);
            if (process == null)
            {
                errorMessage = "The attachment could not be opened.";
                return false;
            }

            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ExceptionDiagnostics.LogException(ex, "opening attachment");
            return false;
        }
    }
}
