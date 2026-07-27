using System.Runtime.InteropServices;

namespace CspMultiplexer.App;

internal static class NativeMethods
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowCornerRound = 2;

    private const uint LabelSecurityInformation = 0x00000010;
    private const int SecurityFileObject = 1;

    // Medium, not High: a medium-integrity process cannot raise an object above its own
    // level. NR is the operative flag — it is what stops a low-IL / AppContainer process
    // reading the file. NW and NX come along for free and cost nothing.
    private const string MediumNoReadUpSddl = "S:(ML;;NRNWNX;;;ME)";

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int value,
        int size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(
        nint windowHandle,
        uint message,
        nuint wParam,
        nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint windowHandle);

    // HWND_BROADCAST is not usable here: it skips invisible owned windows, and in tray
    // mode the window is both — ShowInTaskbar="False" makes WPF give it a hidden owner.
    // EnumWindows sees every top-level window, and the property is how the right one
    // identifies itself across the process boundary.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetProp(nint windowHandle, string name, nint value);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint GetProp(nint windowHandle, string name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint RemoveProp(nint windowHandle, string name);

    internal delegate bool EnumWindowsCallback(nint windowHandle, nint parameter);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string sddl,
        uint revision,
        out nint securityDescriptor,
        out uint size);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSecurityDescriptorSacl(
        nint securityDescriptor,
        [MarshalAs(UnmanagedType.Bool)] out bool saclPresent,
        out nint sacl,
        [MarshalAs(UnmanagedType.Bool)] out bool saclDefaulted);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint SetNamedSecurityInfoW(
        string name,
        int objectType,
        uint securityInformation,
        nint owner,
        nint group,
        nint dacl,
        nint sacl);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint LocalFree(nint handle);

    internal static void ApplyRoundedCorners(nint windowHandle)
    {
        var preference = DwmWindowCornerRound;
        try
        {
            // On Windows 10 the attribute is unrecognised and the call returns a non-zero
            // HRESULT, which is the documented, accepted degradation to square corners.
            DwmSetWindowAttribute(
                windowHandle,
                DwmWindowCornerPreference,
                ref preference,
                sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // Pre-Windows 10 1803; square corners are acceptable.
        }
    }

    /// <summary>
    /// Best effort. The protected DACL is what actually closes the AppContainer hole;
    /// this narrows it further against a low-integrity process running as the same user.
    /// Managed FileSecurity CANNOT do this — it requests SACL_SECURITY_INFORMATION and
    /// fails with ERROR_PRIVILEGE_NOT_HELD. LABEL_SECURITY_INFORMATION needs only
    /// WRITE_OWNER, which the creator of the file has.
    /// </summary>
    internal static void TrySetMediumNoReadUpLabel(string path)
    {
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                MediumNoReadUpSddl,
                1,
                out var securityDescriptor,
                out _))
        {
            return;
        }

        try
        {
            if (GetSecurityDescriptorSacl(securityDescriptor, out _, out var sacl, out _))
            {
                _ = SetNamedSecurityInfoW(
                    path,
                    SecurityFileObject,
                    LabelSecurityInformation,
                    nint.Zero,
                    nint.Zero,
                    nint.Zero,
                    sacl);
            }
        }
        finally
        {
            LocalFree(securityDescriptor);
        }
    }
}
