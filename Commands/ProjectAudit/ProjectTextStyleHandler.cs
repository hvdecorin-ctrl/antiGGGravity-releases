using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text.RegularExpressions;

namespace antiGGGravity.Commands.ProjectAudit
{
    public enum ApplicationScope
    {
        Selection,
        ActiveView,
        EntireProject
    }

    public enum TextToolMode
    {
        Align,
        Convert,
        UpperCase
    }

    public class ProjectTextStyleHandler : IExternalEventHandler
    {
        public TextToolMode OperationMode { get; set; } = TextToolMode.Align;
        public bool AlignLeft { get; set; }
        public bool LeaderTopLeft { get; set; }
        public bool LeaderTopRight { get; set; }
        public double? FilterTextSize { get; set; }
        public List<ElementId> SourceStyleIds { get; set; } = new List<ElementId>();
        public ElementId TargetStyleId { get; set; }
        public bool DeleteSources { get; set; }
        public ApplicationScope Scope { get; set; } = ApplicationScope.ActiveView;
        public Action<string> StatusCallback { get; set; }
        public Action OperationCompleted { get; set; }

        // UpperCase Source Flags
        public bool ConvertTextNotes { get; set; }
        public bool ConvertSheetNames { get; set; }
        public bool ConvertInstanceComments { get; set; }
        public bool ConvertTypeComments { get; set; }
        public bool ConvertTypeMarks { get; set; }
        public bool ConvertDescriptions { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (OperationMode == TextToolMode.UpperCase)
            {
                ExecuteUpperCase(uidoc, doc);
                return;
            }

            // 1. Collect targets based on Scope
            List<TextNote> targets = new List<TextNote>();
// ... (omitting lines for brevity in instruction, will replace fully below)

            if (Scope == ApplicationScope.EntireProject)
            {
                targets = new FilteredElementCollector(doc)
                    .OfClass(typeof(TextNote))
                    .Cast<TextNote>()
                    .ToList();
            }
            else if (Scope == ApplicationScope.ActiveView)
            {
                targets = new FilteredElementCollector(doc, doc.ActiveView.Id)
                    .OfClass(typeof(TextNote))
                    .Cast<TextNote>()
                    .ToList();
            }
            else // Default to Selection
            {
                var selection = uidoc.Selection.GetElementIds();
                if (selection.Count > 0)
                {
                    targets = selection.Select(id => doc.GetElement(id)).OfType<TextNote>().ToList();
                }
            }

            if (targets.Count == 0)
            {
                StatusCallback?.Invoke("No Text Notes found for the selected scope.");
                return;
            }

            // 2. Process
            using (Transaction t = new Transaction(doc, "Project TextStyle"))
            {
                t.Start();
                int modified = 0;

                foreach (TextNote note in targets)
                {
                    bool noteChanged = false;

                    if (OperationMode == TextToolMode.Convert)
                    {
                        // Mode: Convert (Style swap)
                        if (SourceStyleIds != null && SourceStyleIds.Contains(note.GetTypeId()))
                        {
                            if (TargetStyleId != null && TargetStyleId != ElementId.InvalidElementId)
                            {
                                if (note.GetTypeId() != TargetStyleId)
                                {
                                    note.ChangeTypeId(TargetStyleId);
                                    noteChanged = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Mode: Align (Existing logic)
                        // 2a. Filter by Text Size if requested
                        if (FilterTextSize.HasValue)
                        {
                            var type = doc.GetElement(note.GetTypeId()) as TextNoteType;
                            if (type != null)
                            {
                                double size = type.get_Parameter(BuiltInParameter.TEXT_SIZE).AsDouble();
                                if (Math.Abs(size - FilterTextSize.Value) > 0.00003) 
                                    continue;
                            }
                        }

                        // 2b. Apply Align Left
                        if (AlignLeft)
                        {
                            if (note.HorizontalAlignment != HorizontalTextAlignment.Left)
                            {
                                note.HorizontalAlignment = HorizontalTextAlignment.Left;
                                noteChanged = true;
                            }
                        }

                        // 2c. Apply Leader Attachments
                        if (LeaderTopLeft || LeaderTopRight)
                        {
                            var leaders = note.GetLeaders();
                            if (leaders.Count > 0)
                            {
                                dynamic dNote = note;
                                if (LeaderTopLeft && dNote.LeaderLeftAttachment != 0)
                                {
                                    dNote.LeaderLeftAttachment = 0; // TopLine
                                    noteChanged = true;
                                }
                                if (LeaderTopRight && dNote.LeaderRightAttachment != 0)
                                {
                                    dNote.LeaderRightAttachment = 0; // TopLine
                                    noteChanged = true;
                                }
                            }
                        }
                    }

                    if (noteChanged) modified++;
                }

                // 3. Optional: Delete Source Styles if requested
                if (OperationMode == TextToolMode.Convert && DeleteSources && SourceStyleIds != null && SourceStyleIds.Count > 0)
                {
                    foreach (ElementId id in SourceStyleIds)
                    {
                        // Don't delete the target style!
                        if (id == TargetStyleId) continue;

                        try
                        {
                            doc.Delete(id);
                        }
                        catch { /* Skip styles that can't be deleted, e.g. internal defaults */ }
                    }
                }

                t.Commit();
                OperationCompleted?.Invoke();
                string op = OperationMode == TextToolMode.Convert ? "converted" : "modified";
                StatusCallback?.Invoke($"Successfully {op} {modified} elements.");
            }
        }

        public string GetName() => "Project TextStyle Handler";

        #region Smart Uppercase Logic

        private static readonly Dictionary<string, string> UNIT_PATTERNS = new Dictionary<string, string>
        {
            {"mm", "mm"}, {"cm", "cm"}, {"m", "m"}, {"km", "km"},
            {"m2", "m²"}, {"cm2", "cm²"}, {"mm2", "mm²"}, {"km2", "km²"}, {"ft2", "ft²"}, {"in2", "in²"},
            {"m3", "m³"}, {"cm3", "cm³"}, {"mm3", "mm³"}, {"km3", "km³"}, {"ft3", "ft³"}, {"in3", "in³"},
            {"kn", "kN"}, {"n", "N"}, {"mn", "MN"},
            {"mpa", "MPa"}, {"kpa", "kPa"}, {"pa", "Pa"},
            {"kg", "kg"}, {"g", "g"}, {"mg", "mg"}, {"t", "t"}, {"lb", "lb"},
            {"°c", "°C"}, {"deg c", "°C"}, {"°f", "°F"}, {"deg f", "°F"}
        };

        private static readonly Dictionary<string, string> SPECIAL_PRODUCTS = new Dictionary<string, string>
        {
            {"hyspan", "hySPAN"},
            {"hyjoist", "hyJOIST"},
        };

        private static readonly Regex RE_DEG_C = new Regex(@"(\d+(?:\.\d+)?)\s*(?:DEG(?:REE|REES)?|°)\s*C\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RE_DEG_F = new Regex(@"(\d+(?:\.\d+)?)\s*(?:DEG(?:REE|REES)?|°)\s*F\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RE_M2 = new Regex(@"(\d+(?:\.\d+)?)\s*([A-Z]*)M2\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RE_M3 = new Regex(@"(\d+(?:\.\d+)?)\s*([A-Z]*)M3\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RE_SLASH_M2 = new Regex(@"/M2\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RE_SLASH_M3 = new Regex(@"/M3\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly List<(Regex pNum, Regex pStand, string canonical)> RE_UNITS = new List<(Regex, Regex, string)>();
        private static readonly Regex RE_DIMENSION = new Regex(@"(\d+)(?:\s*X\s*\d+)+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly List<(Regex reg, string val)> RE_PRODUCTS = new List<(Regex, string)>();

        static ProjectTextStyleHandler()
        {
            // Products
            foreach (var kvp in SPECIAL_PRODUCTS)
            {
                RE_PRODUCTS.Add((new Regex(@"\b" + Regex.Escape(kvp.Key).ToUpper() + @"\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), kvp.Value));
            }

            // Units
            var sortedKeys = UNIT_PATTERNS.Keys.OrderByDescending(k => k.Length).ToList();
            foreach (var k in sortedKeys)
            {
                string canonical = UNIT_PATTERNS[k];
                string uUpper = k.ToUpper();

                // 1. Number + Unit
                var pNum = new Regex(@"(\d+(?:\.\d+)?)(\s*)" + Regex.Escape(uUpper) + @"(?![A-Z0-9°²³])", RegexOptions.Compiled);

                // 2. Standalone Unit (len > 1)
                Regex pStand = null;
                if (k.Length > 1)
                {
                    pStand = new Regex(@"\b" + Regex.Escape(uUpper) + @"\b", RegexOptions.Compiled);
                }

                RE_UNITS.Add((pNum, pStand, canonical));
            }
        }

        public string SmartUppercase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 1. Uppercase everything initially
            string s = text.ToUpper();

            // 2. Restore special product names
            foreach (var item in RE_PRODUCTS)
            {
                s = item.reg.Replace(s, item.val);
            }

            // 3. Normalize degrees
            s = RE_DEG_C.Replace(s, "$1°C");
            s = RE_DEG_F.Replace(s, "$1°F");

            // 4. Normalize M2/M3 superscripts
            s = RE_M2.Replace(s, "$1$2m²");
            s = RE_M3.Replace(s, "$1$2m³");
            s = RE_SLASH_M2.Replace(s, "/m²");
            s = RE_SLASH_M3.Replace(s, "/m³");

            // 5. Restore units
            foreach (var item in RE_UNITS)
            {
                // Ensure we only match if there's a number followed by the unit string
                // The negative lookahead (?![A-Z0-9]) ensures we don't match the 'G' in 'LONG'
                s = item.pNum.Replace(s, "$1$2" + item.canonical);
                if (item.pStand != null)
                {
                    s = item.pStand.Replace(s, item.canonical);
                }
            }

            // 6. Dimensions (300 x 65) - Only lowercase the 'x', preserve spacing
            s = RE_DIMENSION.Replace(s, m => m.Value.Replace("X", "x"));
            
            // 7. Standalone ' X ' (e.g., 200 CRS x 2000)
            s = s.Replace(" X ", " x ");

            return s;
        }

        #endregion

        #region Upper Case Execution

        private void ExecuteUpperCase(UIDocument uidoc, Document doc)
        {
            // 1. Collect Scope Elements
            List<Element> scopeElements = new List<Element>();
            if (Scope == ApplicationScope.EntireProject)
            {
                scopeElements = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToList();
            }
            else if (Scope == ApplicationScope.ActiveView)
            {
                scopeElements = new FilteredElementCollector(doc, doc.ActiveView.Id).WhereElementIsNotElementType().ToList();
            }
            else // Selection
            {
                var selection = uidoc.Selection.GetElementIds();
                scopeElements = selection.Select(id => doc.GetElement(id)).ToList();
            }

            if (scopeElements.Count == 0 && Scope != ApplicationScope.EntireProject)
            {
                StatusCallback?.Invoke("No elements selected for conversion.");
                return;
            }

            // 2. Filter Types for Type conversion
            List<ElementType> scopeTypes = new List<ElementType>();
            if (Scope == ApplicationScope.EntireProject)
            {
                scopeTypes = new FilteredElementCollector(doc).WhereElementIsElementType().Cast<ElementType>().ToList();
            }
            else
            {
                HashSet<ElementId> typeIds = new HashSet<ElementId>();
                foreach (var e in scopeElements)
                {
                    if (e is ElementType et) typeIds.Add(et.Id);
                    else
                    {
                        ElementId tid = e.GetTypeId();
                        if (tid != ElementId.InvalidElementId) typeIds.Add(tid);
                    }
                }
                scopeTypes = typeIds.Select(id => doc.GetElement(id)).OfType<ElementType>().ToList();
            }

            // 3. Define Tasks
            int noteUpdates = 0;
            int sheetUpdates = 0;
            int commentUpdates = 0;
            int typeCommentUpdates = 0;
            int typeMarkUpdates = 0;
            int descUpdates = 0;

            using (Transaction t = new Transaction(doc, "Smart Uppercase"))
            {
                t.Start();

                foreach (var elem in scopeElements)
                {
                    // Text Notes
                    if (ConvertTextNotes && elem is TextNote note)
                    {
                        string original = note.Text;
                        if (!string.IsNullOrEmpty(original))
                        {
                            string updated = SmartUppercase(original);
                            if (original != updated)
                            {
                                note.Text = updated;
                                noteUpdates++;
                            }
                        }
                    }

                    // Sheet Names
                    if (ConvertSheetNames && elem is ViewSheet sheet)
                    {
                        var p = sheet.get_Parameter(BuiltInParameter.SHEET_NAME);
                        if (p != null && !p.IsReadOnly)
                        {
                            string original = p.AsString();
                            if (!string.IsNullOrEmpty(original))
                            {
                                string updated = SmartUppercase(original);
                                if (original != updated)
                                {
                                    p.Set(updated);
                                    sheetUpdates++;
                                }
                            }
                        }
                    }

                    // Instance Comments
                    if (ConvertInstanceComments)
                    {
                        var p = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                        if (p != null && !p.IsReadOnly)
                        {
                            string original = p.AsString();
                            if (!string.IsNullOrEmpty(original))
                            {
                                string updated = SmartUppercase(original);
                                if (original != updated)
                                {
                                    p.Set(updated);
                                    commentUpdates++;
                                }
                            }
                        }
                    }
                }

                // Type conversions
                foreach (var type in scopeTypes)
                {
                    if (ConvertTypeComments)
                    {
                        var p = type.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS);
                        if (p != null && !p.IsReadOnly)
                        {
                            string original = p.AsString();
                            if (!string.IsNullOrEmpty(original))
                            {
                                string updated = SmartUppercase(original);
                                if (original != updated)
                                {
                                    p.Set(updated);
                                    typeCommentUpdates++;
                                }
                            }
                        }
                    }

                    if (ConvertTypeMarks)
                    {
                        var p = type.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                        if (p != null && !p.IsReadOnly)
                        {
                            string original = p.AsString();
                            if (!string.IsNullOrEmpty(original))
                            {
                                string updated = SmartUppercase(original);
                                if (original != updated)
                                {
                                    p.Set(updated);
                                    typeMarkUpdates++;
                                }
                            }
                        }
                    }

                    if (ConvertDescriptions)
                    {
                        var p = type.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);
                        if (p != null && !p.IsReadOnly)
                        {
                            string original = p.AsString();
                            if (!string.IsNullOrEmpty(original))
                            {
                                string updated = SmartUppercase(original);
                                if (original != updated)
                                {
                                    p.Set(updated);
                                    descUpdates++;
                                }
                            }
                        }
                    }
                }

                t.Commit();
            }

            int total = noteUpdates + sheetUpdates + commentUpdates + typeCommentUpdates + typeMarkUpdates + descUpdates;
            StatusCallback?.Invoke($"Converted {total} items to Upper Case.");
            OperationCompleted?.Invoke();
        }

        #endregion
    }
}
