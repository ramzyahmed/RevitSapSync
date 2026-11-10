using System.Diagnostics;
using Autodesk.Revit.DB;
using RevitSapSync.Core.Models;
using Frame = RevitSapSync.Core.Models.Frame;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Deletes Revit structural elements that have no corresponding SAP frame within the
/// active scope. The reference project never does this - its orphan-deletion code exists
/// but is fully commented out, replaced with isolating unmapped elements into a 3D view
/// and selection filter (which <see cref="SelectionFilterService"/> already covers here).
/// No confirmation step by design - gated purely by the caller's checkbox.
/// </summary>
public sealed class RevitElementDeletionEngine
{
    private const int ChunkSize = 1000;

    public ElementDeletionResult Delete(Document document, List<Frame> unmappedRevitFrames, bool previewOnly = false)
    {
        var deleted = new List<ElementReference>();
        var errors = new List<SyncError>();

        using var transactionGroup = new TransactionGroup(document, "RevitSapSync: Delete Unmapped Elements");
        transactionGroup.Start();

        foreach (var chunk in ChunkList(unmappedRevitFrames, ChunkSize))
        {
            using var transaction = new Transaction(document, "RevitSapSync: Delete Unmapped Elements Chunk");
            transaction.Start();
            var failureOptions = transaction.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new SuppressWarningsFailuresPreprocessor());
            transaction.SetFailureHandlingOptions(failureOptions);

            foreach (var frame in chunk)
            {
                var reference = frame.RevitElement;
                try
                {
                    document.Delete(new ElementId(reference!.IntegerId));
                    deleted.Add(reference);
                }
                catch (Exception ex)
                {
                    errors.Add(new SyncError { SapId = frame.Id, RevitElement = reference, Message = ex.Message });
                    Trace.TraceWarning($"RevitSapSync: failed to delete unmapped element {reference?.IntegerId}. {ex}");
                }
            }

            transaction.Commit();
        }

        if (previewOnly)
            transactionGroup.RollBack();
        else
            transactionGroup.Assimilate();

        return new ElementDeletionResult { Deleted = deleted, Errors = errors };
    }

    // Enumerable.Chunk is net6.0+ only and unavailable on net48; this is the portable equivalent.
    private static IEnumerable<List<T>> ChunkList<T>(List<T> source, int chunkSize)
    {
        for (int i = 0; i < source.Count; i += chunkSize)
            yield return source.GetRange(i, Math.Min(chunkSize, source.Count - i));
    }
}
