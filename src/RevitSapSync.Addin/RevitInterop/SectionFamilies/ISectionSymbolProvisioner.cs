using Autodesk.Revit.DB;
using Frame = RevitSapSync.Core.Models.Frame;

namespace RevitSapSync.RevitInterop.SectionFamilies;

/// <summary>
/// Finds (or creates) the Revit <see cref="FamilySymbol"/> matching a SAP-sourced
/// <see cref="Frame"/>'s classified/normalized section. Reimplements
/// <c>RevitUtils.createRvtFamilySectionForBeam</c>/<c>ForColumn</c>. Returns null when the
/// shape isn't recognized - callers fall back to a "NotFound" placeholder symbol, exactly
/// as the reference does, rather than failing the whole sync.
/// <para>
/// Opens its own <see cref="Transaction"/>(s) as needed (matching the reference, which
/// duplicates/renames/sets parameters each inside their own transaction) - call this
/// <b>before</b> starting the main mapped-element update transaction(s), the same
/// provision-all-symbols-first phase the reference uses via
/// <c>createRvtFamilySectionForAllBeam</c>/<c>ForColumn</c>, not nested inside them.
/// </para>
/// </summary>
public interface ISectionSymbolProvisioner
{
    FamilySymbol? Provision(Document document, Frame frame, ForgeTypeId displayUnit);
}
