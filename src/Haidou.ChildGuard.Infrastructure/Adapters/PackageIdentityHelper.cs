using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

public static class PackageIdentityHelper
{
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

    public static bool HasPackageIdentity()
    {
        try
        {
            int length = 0;
            int result = GetCurrentPackageFullName(ref length, null);
            return result != APPMODEL_ERROR_NO_PACKAGE && length > 0;
        }
        catch
        {
            return false;
        }
    }
}
