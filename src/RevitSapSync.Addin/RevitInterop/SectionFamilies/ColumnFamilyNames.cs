namespace RevitSapSync.RevitInterop.SectionFamilies;

/// <summary>
/// Real family/parameter name constants for the AECOM structural column family library,
/// extracted verbatim from the reference implementation's <c>Frame.RevitFamily.COL_*</c>
/// nested classes (<c>ClassesDefintions.cs</c>, the active <c>#if AEC_Revit_2023</c>
/// branch) - not placeholders.
/// </summary>
internal static class ColumnFamilyNames
{
    internal static class BusI
    {
        public const string FamilyName = "ACM_SE_SCOL_BUS-I";
        public const string TotalDepth = "H";
        public const string FlangeWidth = "B";
        public const string WebThick = "tw";
        public const string FlangeThick = "tf";
    }

    internal static class Hea { public const string FamilyName = "ACM_SE_SCOL_HEA"; public const string Library = "euro"; }
    internal static class Heaa { public const string FamilyName = "ACM_SE_SCOL_HEAA"; public const string Library = "euro"; }
    internal static class Heb { public const string FamilyName = "ACM_SE_SCOL_HEB"; public const string Library = "euro"; }
    internal static class Hem { public const string FamilyName = "ACM_SE_SCOL_HEM"; public const string Library = "euro"; }
    internal static class Ipe { public const string FamilyName = "ACM_SE_SCOL_IPE"; public const string Library = "euro"; }
    internal static class Ipea { public const string FamilyName = "ACM_SE_SCOL_IPEA"; public const string Library = "euro"; }
    internal static class Ipeo { public const string FamilyName = "ACM_SE_SCOL_IPEO"; public const string Library = "euro"; }
    internal static class W { public const string FamilyName = "ACM_SE_SCOL_W"; public const string Library = "AISC"; }
    internal static class Ub { public const string FamilyName = "ACM_SE_SCOL_UB"; public const string Library = "BSShapes"; }
    internal static class Uc { public const string FamilyName = "ACM_SE_SCOL_UC"; public const string Library = "BSShapes"; }

    internal static class LEqual { public const string FamilyName = "ACM_SE_SCOL_L_EQUAL"; }
    internal static class LUnequal { public const string FamilyName = "ACM_SE_SCOL_L_UNEQUAL"; }
    internal static class TwoLEqualAngle { public const string FamilyName = "ACM_SE_SCOL_2L_EQUAL"; }

    internal static class Pipe
    {
        public const string FamilyName = "ACM_SE_SCOL_PIPE";
        public const string OuterDiameter = "d";
        public const string Thickness = "tw";
    }

    internal static class Tube
    {
        public const string FamilyName = "ACM_SE_SCOL_TUBE";
        public const string WebDepth = "d";
        public const string FlangeWidth = "bf";
        public const string WebThick = "tw";
        public const string FlangeThick = "tf";
        public const string OuterRadius = "ri";
        public const string InnerRadius = "ra";

        /// <summary>Reference hardcodes both corner radii to this constant (mm) rather than deriving them from SAP data.</summary>
        public const double DefaultRadius = 4.0;
    }

    internal static class Upn { public const string FamilyName = "ACM_SE_SCOL_UPN"; }

    internal static class BusITapered
    {
        public const string FamilyName = "ACM_SE_SCOL_BUS-I-Tapered";
        public const string StartWebDepth = "H_Bott";
        public const string EndWebDepth = "H_Top";
        public const string StartFlangeWidth = "Bf_Bott";
        public const string EndFlangeWidth = "Bf_Top";
        public const string WebThick = "tw";
        public const string FlangeThick = "tf";
    }

    internal static class BusPipeTapered
    {
        public const string FamilyName = "ACM_SE_SCOL_BUS-PIPE-Tapered";
        public const string StartInnerDiameter = "Bott Inner Dia";
        public const string StartOuterDiameter = "Bott Outer Dia";
        public const string EndInnerDiameter = "Top Inner Dia";
        public const string EndOuterDiameter = "Top Outer Dia";
    }

    internal static class BusTubeTapered
    {
        public const string FamilyName = "ACM_SE_SCOL_BUS-TUBE-Tapered";
        public const string StartWebDepth = "Bottom h";
        public const string StartWebThick = "Bottom tw";
        public const string StartFlangeWidth = "Bottom b";
        public const string StartFlangeThick = "Bottom tf";
        public const string EndWebDepth = "Top h";
        public const string EndWebThick = "Top tw";
        public const string EndFlangeWidth = "Top b";
        public const string EndFlangeThick = "Top tf";
    }

    internal static class ConcreteRectangular
    {
        public const string FamilyName = "ACM_SE_CCL_Rectangular";
        // Reference note: these two parameter names are swapped relative to the beam
        // version's ConcreteRectangular (Width param actually holds "Overall Depth" and
        // vice versa) - preserved exactly as extracted, not "corrected", since it reflects
        // the real family's parameter setup.
        public const string Width = "ACM_SE_Overall Depth";
        public const string Depth = "ACM_SE_Overall Width";
    }

    internal static class ConcreteCircular
    {
        public const string FamilyName = "ACM_SE_CCL_Circular";
        public const string Diameter = "ACM_SE_Overall Diameter";
    }
}
