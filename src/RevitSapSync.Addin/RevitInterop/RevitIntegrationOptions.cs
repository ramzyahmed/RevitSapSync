namespace RevitSapSync.RevitInterop;

/// <summary>
/// Project-specific Revit integration settings. Defaults match the reference
/// implementation's <c>HardCodedData</c> constants (confirmed real values, not
/// placeholders) for the AECOM structural family library.
/// </summary>
public sealed class RevitIntegrationOptions
{
    public string SapIdParameterName { get; set; } = "ACM_SE_SBM_SapID";
    public string MaterialParameterName { get; set; } = "ACM_SE_SBM_AnalysisMaterial";

    /// <summary>Steel density used for tonnage calculations in the validation report. Matches the reference's <c>HardCodedData.GamaSteel</c>.</summary>
    public double SteelDensityTonnesPerCubicMeter { get; set; } = 7.85;
}
