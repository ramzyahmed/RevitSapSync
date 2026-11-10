using SAP2000v1;

namespace RevitSapSync.Sap2000Interop;

public interface ISapConnectionProvider
{
    /// <summary>
    /// Returns the <see cref="cSapModel"/> of the currently running SAP2000 instance.
    /// Throws <see cref="InvalidOperationException"/> (not a raw <see cref="System.Runtime.InteropServices.COMException"/>)
    /// if no instance is running.
    /// </summary>
    cSapModel GetActiveModel();
}

public sealed class Sap2000ConnectionProvider : ISapConnectionProvider
{
    private const string ProgId = "CSI.SAP2000.API.SapObject";

    public cSapModel GetActiveModel()
    {
        object activeObject;
        try
        {
            activeObject = ComActiveObjectHelper.GetActiveObject(ProgId);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            throw new InvalidOperationException("No running SAP2000 instance found. Open SAP2000 with a model loaded and try again.", ex);
        }

        var sapApp = (cOAPI)activeObject;
        return sapApp.SapModel;
    }
}
