namespace RevitSapSync.RevitInterop.SectionFamilies;

/// <summary>
/// Real family/parameter name constants for the AECOM structural framing (beam) family
/// library, extracted verbatim from the reference implementation's
/// <c>Frame.RevitFamily.BM_*</c> nested classes (<c>ClassesDefintions.cs</c>, the active
/// <c>#if AEC_Revit_2023</c> branch) - not placeholders.
/// </summary>
internal static class BeamFamilyNames
{
    internal static class BusI
    {
        public const string FamilyName = "ACM_SE_SBM_BUS-I";
        public const string TotalDepth = "H";
        public const string FlangeWidth = "bf";
        public const string WebThick = "tw";
        public const string FlangeThick = "tf";
    }

    internal static class Hea { public const string FamilyName = "ACM_SE_SBM_HEA"; public const string Library = "euro"; }
    internal static class Heaa { public const string FamilyName = "ACM_SE_SBM_HEAA"; public const string Library = "euro"; }
    internal static class Heb { public const string FamilyName = "ACM_SE_SBM_HEB"; public const string Library = "euro"; }
    internal static class Hem { public const string FamilyName = "ACM_SE_SBM_HEM"; public const string Library = "euro"; }
    internal static class Ipe { public const string FamilyName = "ACM_SE_SBM_IPE"; public const string Library = "euro"; }
    internal static class Ipea { public const string FamilyName = "ACM_SE_SBM_IPEA"; public const string Library = "euro"; }
    internal static class Ipeo { public const string FamilyName = "ACM_SE_SBM_IPEO"; public const string Library = "euro"; }
    internal static class W { public const string FamilyName = "ACM_SE_SBM_W"; public const string Library = "AISC"; }
    internal static class Ub { public const string FamilyName = "ACM_SE_SBM_UB"; public const string Library = "BSShapes"; }
    internal static class Uc { public const string FamilyName = "ACM_SE_SBM_UC"; public const string Library = "BSShapes"; }

    internal static class LEqual { public const string FamilyName = "ACM_SE_SBM_L_EQUAL"; }
    internal static class LUnequal { public const string FamilyName = "ACM_SE_SBM_L_UNEQUAL"; }
    internal static class TwoLEqualAngle { public const string FamilyName = "ACM_SE_SBM_2L_EQUAL"; }

    internal static class Pipe
    {
        public const string FamilyName = "ACM_SE_SBM_PIPE";
        public const string OuterDiameter = "d";
        public const string Thickness = "tw";
    }

    internal static class Tube
    {
        public const string FamilyName = "ACM_SE_SBM_TUBE";
        public const string WebDepth = "d";
        public const string FlangeWidth = "bf";
        public const string WebThick = "tw";
        public const string FlangeThick = "tf";
        public const string OuterRadius = "ri";
        public const string InnerRadius = "ra";

        /// <summary>Reference hardcodes both corner radii to this constant (mm) rather than deriving them from SAP data.</summary>
        public const double DefaultRadius = 1.0;
    }

    internal static class Upn { public const string FamilyName = "ACM_SE_SBM_UPN"; }

    internal static class BusITapered
    {
        public const string FamilyName = "ACM_SE_SBM_BUS-I-Tapered";
        public const string StartWebDepth = "H Web 2";
        public const string EndWebDepth = "H Web 1";
        public const string FlangeWidth = "fl Width";
        public const string WebThick = "Web Thickness";
        public const string FlangeThick = "fl Thickness";
    }

    internal static class BusPipeTapered
    {
        public const string FamilyName = "ACM_SE_SBM_BUS-PIPE-Tapered";
        public const string StartDiameter = "Start d";
        public const string EndDiameter = "End d";
        public const string StartThick = "Start t";
        public const string EndThick = "End t";
    }

    internal static class BusTubeFullyTapered
    {

        public static string FamilyName = "ACM_SE_SBM_Buit-up Fully Tapered Tube Frame";
        public static string StartTotalDepth = "ACM_SE_Second Depth";
        public static string EndTotalDepth = "ACM_SE_Overall Depth";
        public static string StartFlangeWidth = "ACM_SE_Second Width";
        public static string EndFlangeWidth = "ACM_SE_Overall Width";
        public static string FlangeThick = "ACM_SE_Flange Thickness";
        public static string WebThick = "ACM_SE_Web Thickness";

        //public const string FamilyName = "ACM_SE_SBM_Buit-up Tapered Tube Frame";
        //public const string StartTotalDepth = "ACM_SE_Second Depth";
        //public const string EndTotalDepth = "ACM_SE_Overall Depth";
        //public const string StartFlangeWidth = "ACM_SE_Overall Width";
        //public const string EndFlangeWidth = "ACM_SE_Overall Width";
        //public const string FlangeThick = "ACM_SE_Flange Thickness";
        //public const string WebThick = "ACM_SE_Web Thickness";
    }

    internal static class ConcreteRectangular
    {
        public const string FamilyName = "ACM_SE_CBM_Rectangular Beam";
        public const string Depth = "ACM_SE_Overall Depth";
        public const string Width = "ACM_SE_Overall Width";
    }
}
