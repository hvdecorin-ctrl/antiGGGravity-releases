using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.Overrides;

namespace antiGGGravity.Commands.Overrides
{
    [Transaction(TransactionMode.Manual)]
    public class DimFakeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Open the DimFake modeless window
            try
            {
                DimFakeView view = new DimFakeView(commandData);
                view.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class TextAuditCommand : IExternalCommand
    {
        private static TextAuditView _view;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (_view != null && _view.IsVisible)
                {
                    _view.Focus();
                    return Result.Succeeded;
                }

                var handler = new TextAuditHandler();
                var auditEvent = ExternalEvent.Create(handler);

                _view = new TextAuditView(auditEvent, handler);
                _view.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class TextUpperCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Collect TextNotes from selection or active view
            var selectedIds = uidoc.Selection.GetElementIds();
            List<TextNote> textNotes = new List<TextNote>();

            if (selectedIds.Count > 0)
            {
                foreach (ElementId id in selectedIds)
                {
                    Element elem = doc.GetElement(id);
                    if (elem is TextNote note) textNotes.Add(note);
                }
            }
            else
            {
                // If nothing selected, process active view? Or prompt?
                // Python script processes ALL in active view if nothing selected? 
                // Wait, Python script 'TextUpper' operates on selection. 
                // Actually, looking at 'Text Upper' script, it says 'process_textnotes_in_view' which collects from Active View!
                // "collector = DB.FilteredElementCollector(doc, active_view.Id).OfClass(DB.TextNote)"
                
                textNotes = new FilteredElementCollector(doc, doc.ActiveView.Id)
                    .OfClass(typeof(TextNote))
                    .Cast<TextNote>()
                    .ToList();
            }

            if (textNotes.Count == 0)
            {
                TaskDialog.Show("Text Upper", "No Text Notes found.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Smart Uppercase"))
            {
                t.Start();
                int count = 0;
                foreach (TextNote note in textNotes)
                {
                    string original = note.Text;
                    string updated = SmartUppercase(original);
                    if (original != updated)
                    {
                        note.Text = updated;
                        count++;
                    }
                }
                t.Commit();
                TaskDialog.Show("Text Upper", $"Updated {count} text notes.");
            }

            return Result.Succeeded;
        }

        private string SmartUppercase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 1. Initial Uppercase
            string s = text.ToUpper();

            // 2. Restore Products
            var products = new Dictionary<string, string>
            {
                { "HYSPAN", "hySPAN" },
                { "HYJOIST", "hyJOIST" }
            };
            foreach (var kvp in products)
            {
                s = Regex.Replace(s, $@"\b{kvp.Key}\b", kvp.Value, RegexOptions.IgnoreCase);
            }

            // 3. Normalize Degrees
            s = Regex.Replace(s, @"(\d+(?:\.\d+)?)\s*(?:DEG(?:REE|REES)?|°)\s*C\b", "$1°C", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"(\d+(?:\.\d+)?)\s*(?:DEG(?:REE|REES)?|°)\s*F\b", "$1°F", RegexOptions.IgnoreCase);

            // 4. Normalize M2/M3
            s = Regex.Replace(s, @"(\d+(?:\.\d+)?)\s*([A-Z]*)M2\b", "$1$2m²", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"(\d+(?:\.\d+)?)\s*([A-Z]*)M3\b", "$1$2m³", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"/M2\b", "/m²", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"/M3\b", "/m³", RegexOptions.IgnoreCase);

            // 5. Restore Units (mm, cm, m, kg, etc.)
            var units = new Dictionary<string, string>
            {
                { "MM", "mm" }, { "CM", "cm" }, { "M", "m" }, { "KM", "km" },
                { "KG", "kg" }, { "G", "g" }, { "MG", "mg" }, { "LB", "lb" },
                { "KN", "kN" }, { "N", "N" }, { "MN", "MN" },
                { "MPA", "MPa" }, { "KPA", "kPa" }, { "PA", "Pa" }
            };
            
            // Sort by length desc to match longer units first (e.g. MPA before PA)
            foreach (var kvp in units.OrderByDescending(x => x.Key.Length))
            {
                // Number + Unit (e.g. 100MM -> 100mm)
                s = Regex.Replace(s, $@"(\d+(?:\.\d+)?)(\s*){kvp.Key}(?![A-Z0-9°²³])", $"$1$2{kvp.Value}", RegexOptions.IgnoreCase);
                
                // Standalone Unit (e.g. "IN MM" -> "IN mm") - dangerous? regex uses \b
                s = Regex.Replace(s, $@"\b{kvp.Key}\b", kvp.Value, RegexOptions.IgnoreCase);
            }

            // 6. Dimensions (300X45 -> 300x45)
            return s;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class TextLeaderAlignCommand : IExternalCommand
    {
        private static TextLeaderAlignView _view;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (_view != null && _view.IsVisible)
                {
                    _view.Focus();
                    return Result.Succeeded;
                }

                var handler = new TextLeaderAlignHandler();
                var alignEvent = ExternalEvent.Create(handler);

                _view = new TextLeaderAlignView(alignEvent, handler, commandData.Application.ActiveUIDocument.Document);
                
                // Set initial owner to Revit
                try
                {
                    var process = System.Diagnostics.Process.GetCurrentProcess();
                    var wrapper = new System.Windows.Interop.WindowInteropHelper(_view);
                    wrapper.Owner = process.MainWindowHandle;
                }
                catch { }

                _view.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
