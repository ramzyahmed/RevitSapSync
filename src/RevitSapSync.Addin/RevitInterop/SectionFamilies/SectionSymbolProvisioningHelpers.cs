using Autodesk.Revit.DB;

namespace RevitSapSync.RevitInterop.SectionFamilies;

/// <summary>Shared lookup/duplicate/parameter-set plumbing used by both beam and column provisioners.</summary>
internal static class SectionSymbolProvisioningHelpers
{
    public static FamilySymbol? FindSymbol(Document document, BuiltInCategory category, string familyName, Func<FamilySymbol, bool> predicate) =>
        new FilteredElementCollector(document)
            .OfCategory(category)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .Where(f => f.FamilyName == familyName)
            .FirstOrDefault(predicate);

    /// <summary>Finds a FamilySymbol by exact type name regardless of family - used for the NotFound-placeholder convention, which isn't scoped to one family.</summary>
    public static FamilySymbol? FindByTypeName(Document document, BuiltInCategory category, string typeName) =>
        new FilteredElementCollector(document)
            .WhereElementIsElementType()
            .OfCategory(category)
            .Cast<FamilySymbol>()
            .FirstOrDefault(x => x.Name == typeName);

    /// <summary>
    /// Case-insensitive fallback used by the reference for Pipe/Tube lookups: if a match
    /// exists only in lowercase, normalize its Name to uppercase in-place and return it.
    /// </summary>
    public static FamilySymbol? FindSymbolCaseInsensitiveFallback(Document document, BuiltInCategory category, string familyName, string dimensionSuffix, int? requiredXCount)
    {
        var match = FindSymbol(document, category, familyName, symbol =>
            symbol.Name.Contains(dimensionSuffix.ToLowerInvariant()) &&
            (requiredXCount is null || symbol.Name.Count(c => c == 'x') == requiredXCount));

        if (match is null)
            return null;

        using var transaction = new Transaction(document, "RevitSapSync: Normalize Family Type Name");
        transaction.Start();
        match.Name = match.Name.ToUpperInvariant();
        transaction.Commit();
        return match;
    }

    public static FamilySymbol DuplicateAndRename(Document document, BuiltInCategory category, string familyName, string newTypeName)
    {
        var baseSymbol = FindSymbol(document, category, familyName, _ => true)
            ?? throw new InvalidOperationException($"No base FamilySymbol found for family '{familyName}' in category {category}. The family must already be loaded into the project.");

        using var transaction = new Transaction(document, "RevitSapSync: Create Family Type");
        transaction.Start();
        var failureOptions = transaction.GetFailureHandlingOptions();
        failureOptions.SetFailuresPreprocessor(new SuppressWarningsFailuresPreprocessor());
        transaction.SetFailureHandlingOptions(failureOptions);

        FamilySymbol newSymbol;
        try
        {
            newSymbol = (FamilySymbol)baseSymbol.Duplicate(newTypeName);
        }
        catch (Exception ex) when (ex.Message.Contains("already in use"))
        {
            // Deliberately catches by message, not by type: Revit API validation failures
            // like this one come from Autodesk.Revit.Exceptions.ArgumentException - a type
            // of the same name as, but entirely unrelated to, System.ArgumentException, so
            // "catch (ArgumentException ...)" silently never matches it (confirmed live -
            // the fallback below never triggered until narrowed to the message instead).
            // A type named newTypeName already exists in this family - most likely created
            // by an earlier sync run against this same project (re-running the tool
            // repeatedly during testing is the normal workflow). Reuse it instead of
            // failing, matching the find-or-create pattern already used for NotFound
            // placeholders, rather than treating "it's already there" as an error.
            transaction.RollBack();
            return FindSymbol(document, category, familyName, s => s.Name == newTypeName)
                ?? throw new InvalidOperationException(
                    $"Revit reported the type name '{newTypeName}' as already in use in family '{familyName}', but no matching type could be found to reuse.");
        }

        transaction.Commit();
        return newSymbol;
    }

    public static void SetParameters(Document document, FamilySymbol symbol, ForgeTypeId displayUnit, params (string ParameterName, double Value)[] parameters)
    {
        using var transaction = new Transaction(document, "RevitSapSync: Set Family Type Parameters");
        transaction.Start();
        var failureOptions = transaction.GetFailureHandlingOptions();
        failureOptions.SetFailuresPreprocessor(new SuppressWarningsFailuresPreprocessor());
        transaction.SetFailureHandlingOptions(failureOptions);

        foreach (var (parameterName, value) in parameters)
            symbol.LookupParameter(parameterName)?.Set(UnitUtils.ConvertToInternalUnits(value, displayUnit));

        transaction.Commit();
    }
}
