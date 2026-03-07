using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace antiGGGravity.Commands.General.AutoDimension
{
    /// <summary>
    /// Automatically deletes problematic dimensions instead of showing a dialog.
    /// Matches the Python DimFailureSwallower class.
    /// </summary>
    public class DimFailureSwallower : IFailuresPreprocessor
    {
        public List<string> HadErrors { get; } = new();

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failures = failuresAccessor.GetFailureMessages();
            foreach (var f in failures)
            {
                try
                {
                    var sev = f.GetSeverity();
                    string desc = f.GetDescriptionText();

                    if (sev == FailureSeverity.Error)
                    {
                        HadErrors.Add(desc);
                        var ids = f.GetFailingElementIds();
                        if (ids != null && ids.Count > 0)
                            failuresAccessor.DeleteElements(ids.ToList());
                        else
                            failuresAccessor.ResolveFailure(f);
                    }
                    else if (sev == FailureSeverity.Warning)
                    {
                        failuresAccessor.DeleteWarning(f);
                    }
                }
                catch
                {
                    try { failuresAccessor.ResolveFailure(f); } catch { }
                }
            }
            return FailureProcessingResult.Continue;
        }
    }
}
