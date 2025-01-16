using System.Runtime.InteropServices;

namespace Systems.Sanity.Focus.Infrastructure;

public class OsInfo
{
    public static bool IsWindows()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
}