using System.Runtime.InteropServices;

namespace RevitSapSync.Sap2000Interop;

/// <summary>
/// Reimplementation of <c>Marshal.GetActiveObject</c>, which is only available on .NET
/// Framework - it was dropped from CoreCLR, so a plain P/Invoke shim over
/// ole32/oleaut32 is needed to attach to a running SAP2000 instance from net8.0. Verified
/// against the CSI-shipped SAP2000v1.dll interop assembly (see the Milestone 1 COM
/// interop spike).
/// </summary>
internal static class ComActiveObjectHelper
{
    [DllImport("ole32.dll")]
    private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string lpszProgID, out Guid pclsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    public static object GetActiveObject(string progId)
    {
        int hr = CLSIDFromProgID(progId, out Guid clsid);
        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);

        hr = GetActiveObject(ref clsid, IntPtr.Zero, out object activeObject);
        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);

        return activeObject;
    }
}
