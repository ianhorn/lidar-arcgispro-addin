/*
 * Breaklines for the LAS dataset. The user chooses a line feature class (with Z values) in the dock pane;
 * LasDatasetService hands it to the LAS dataset tools as a Hard_Line surface constraint.
 */
namespace StaclidarAddin.Services
{
    public static class BreaklineService
    {
        /// <summary>The value-table row LasDatasetService hands to the LAS dataset tools' surface
        /// constraint parameter: feature class (quoted, since paths can contain spaces), height
        /// source (the features' own Z), and constraint type.</summary>
        public static string ToConstraintArgument(string featureClassPath) => $"'{featureClassPath}' Shape.Z Hard_Line";
    }
}
