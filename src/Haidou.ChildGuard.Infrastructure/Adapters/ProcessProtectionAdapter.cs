using System;
using System.Runtime.InteropServices;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

/// <summary>
/// Win32 进程防强杀安全适配器（基于 Windows DACL 移除 PROCESS_TERMINATE 权限）
/// </summary>
public static class ProcessProtectionAdapter
{
    private const int SDDL_REVISION_1 = 1;
    private const int SE_KERNEL_OBJECT = 6;
    private const int DACL_SECURITY_INFORMATION = 0x00000004;

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
        string stringSecurityDescriptor,
        int stringSDRevision,
        out IntPtr securityDescriptor,
        out uint securityDescriptorSize);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetSecurityDescriptorDacl(
        IntPtr pSecurityDescriptor,
        out bool lpbDaclPresent,
        out IntPtr pDacl,
        out bool lpbDaclDefaulted);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern uint SetSecurityInfo(
        IntPtr handle,
        int objectType,
        int securityInformation,
        IntPtr psidOwner,
        IntPtr psidGroup,
        IntPtr pDacl,
        IntPtr pSacl);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    /// <summary>
    /// 开启当前进程抗强杀保护（任务管理器右键结束任务将提示'拒绝访问'）
    /// </summary>
    public static bool ProtectCurrentProcess()
    {
        try
        {
            // SDDL:
            // D: (DACL)
            // (D;;0x0001;;;WD) -> Deny PROCESS_TERMINATE (0x0001) for Everyone (WD)
            // (A;;0x1FFFFE;;;WD) -> Allow all other rights for Everyone (WD)
            string sddl = "D:(D;;0x0001;;;WD)(A;;0x1FFFFE;;;WD)";

            if (!ConvertStringSecurityDescriptorToSecurityDescriptor(sddl, SDDL_REVISION_1, out IntPtr pSD, out _))
            {
                return false;
            }

            try
            {
                if (GetSecurityDescriptorDacl(pSD, out bool present, out IntPtr pDacl, out _) && present && pDacl != IntPtr.Zero)
                {
                    uint result = SetSecurityInfo(
                        GetCurrentProcess(),
                        SE_KERNEL_OBJECT,
                        DACL_SECURITY_INFORMATION,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        pDacl,
                        IntPtr.Zero);

                    return result == 0;
                }
            }
            finally
            {
                LocalFree(pSD);
            }
        }
        catch
        {
            // 若系统策略限制，平滑降级，依赖伴生看门狗机制
        }
        return false;
    }

    /// <summary>
    /// 家长输入密码核销退出时，恢复默认 DACL 权限允许安全退出
    /// </summary>
    public static bool UnprotectCurrentProcess()
    {
        try
        {
            // 允许所有权限，解除 Terminate 拦截
            string sddl = "D:(A;;0x1FFFFF;;;WD)";

            if (!ConvertStringSecurityDescriptorToSecurityDescriptor(sddl, SDDL_REVISION_1, out IntPtr pSD, out _))
            {
                return false;
            }

            try
            {
                if (GetSecurityDescriptorDacl(pSD, out bool present, out IntPtr pDacl, out _) && present && pDacl != IntPtr.Zero)
                {
                    uint result = SetSecurityInfo(
                        GetCurrentProcess(),
                        SE_KERNEL_OBJECT,
                        DACL_SECURITY_INFORMATION,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        pDacl,
                        IntPtr.Zero);

                    return result == 0;
                }
            }
            finally
            {
                LocalFree(pSD);
            }
        }
        catch {}
        return false;
    }
}