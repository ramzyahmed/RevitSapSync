namespace RevitSapSync.Core.Options;

/// <summary>
/// Aggregates the per-run options a user configures once, up front, before a sync
/// session starts - shown/edited via the pre-flight options window rather than the
/// previous inline <c>new MappingOptions()</c>/<c>new GeometrySyncOptions()</c> defaults
/// baked into each command.
/// </summary>
public sealed class SyncSessionOptions
{
    public MappingOptions MappingOptions { get; init; } = new();
    public GeometrySyncOptions GeometrySyncOptions { get; init; } = new();

    /// <summary>
    /// When true, reading the SAP2000 model is restricted to whatever is currently
    /// selected in SAP2000, instead of the whole model - see
    /// <see cref="Services.FrameReadOptions.SelectedOnly"/>. Independent of
    /// <see cref="UseSelectedRevitElements"/> - all four combinations are valid (e.g.
    /// selected-SAP-only maps against the entire Revit model).
    /// </summary>
    public bool UseSelectedSapMembers { get; set; }

    /// <summary>Same as <see cref="UseSelectedSapMembers"/>, but for the Revit read.</summary>
    public bool UseSelectedRevitElements { get; set; }

    /// <summary>
    /// When true, every mutating step (section sync, geometry sync, creation, deletion)
    /// still runs its full detection/decision logic and opens its usual transaction group,
    /// but rolls that group back instead of assimilating it - so counts/reports reflect
    /// exactly what would happen, but nothing is actually persisted to the document.
    /// </summary>
    public bool PreviewOnly { get; set; }

    /// <summary>Update the Revit family/type (section) to match the SAP2000 section assignment on mapped members.</summary>
    public bool UpdateSections { get; set; } = true;

    /// <summary>Update the geometry (position/length/analytical alignment) of mapped members.</summary>
    public bool UpdateGeometry { get; set; } = true;

    /// <summary>Create new Revit structural framing elements for unmapped SAP beam-like frames.</summary>
    public bool CreateBeams { get; set; }

    /// <summary>Create new Revit structural column elements for unmapped SAP column-like frames.</summary>
    public bool CreateColumns { get; set; }

    /// <summary>Delete Revit structural members that have no corresponding SAP frame within the active scope.</summary>
    public bool DeleteUnmappedRevitMembers { get; set; }
}
