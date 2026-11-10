using Autodesk.Revit.DB;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Dismisses non-fatal Revit warnings during bulk sync transactions so an unattended run
/// doesn't stall on dialogs (e.g. join/geometry-overlap warnings when moving structural
/// elements). Errors are left alone - only <see cref="FailureSeverity.Warning"/> is
/// suppressed. Clean equivalent of the reference implementation's per-transaction
/// warning-swallowing preprocessor.
/// </summary>
public sealed class SuppressWarningsFailuresPreprocessor : IFailuresPreprocessor
{
    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        foreach (var failure in failuresAccessor.GetFailureMessages())
        {
            if (failure.GetSeverity() == FailureSeverity.Warning)
                failuresAccessor.DeleteWarning(failure);
        }

        return FailureProcessingResult.Continue;
    }
}
