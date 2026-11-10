using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;

namespace RevitSapSync.Core.Services;

/// <summary>Pairs SAP frames with Revit frames by ID, falling back to geometry proximity.</summary>
public interface IFrameMappingEngine
{
    MappingResult Map(List<Frame> sapFrames, List<Frame> revitFrames, MappingOptions options);
}
