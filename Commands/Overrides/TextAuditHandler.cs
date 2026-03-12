using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.Overrides
{
    public class TextAuditHandler : IExternalEventHandler
    {
        public enum AuditAction
        {
            Merge,
            MergeParagraphs,
            RemoveReturns,
            RemoveExtraSpaces,
            ToLower,
            ToUpper,
            ToTitle
        }

        public AuditAction CurrentAction { get; set; }
        public Action<string> StatusCallback { get; set; }

        private static readonly Dictionary<string, string> Products = new Dictionary<string, string>
        {
            { "HYSPAN", "hySPAN" },
            { "HYJOIST", "hyJOIST" }
        };

        private static readonly Dictionary<string, string> Units = new Dictionary<string, string>
        {
            { "MM", "mm" }, { "CM", "cm" }, { "M", "m" }, { "KM", "km" },
            { "KG", "kg" }, { "G", "g" }, { "MG", "mg" }, { "LB", "lb" },
            { "KN", "kN" }, { "N", "N" }, { "MN", "MN" },
            { "MPA", "MPa" }, { "KPA", "kPa" }, { "PA", "Pa" }
        };

        private static readonly Regex BulletRegex = new Regex(
            @"^(\s*" +
            @"(?:" +
            @"[\u2022\u2023\u25E6\u2043\u2219\-\*\+\•\●\◦]+" +
            @"|(?:\d+|[ivxlcIVXLC]+|[a-zA-Z])" +
            @"[.)]?" +
            @")\s+" +
            @")(.*)$",
            RegexOptions.Compiled);

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            var selection = uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .OfType<TextNote>()
                .ToList();

            if (selection.Count == 0 && (CurrentAction == AuditAction.Merge || CurrentAction == AuditAction.MergeParagraphs))
            {
                StatusCallback?.Invoke("Please select at least two Text Notes to merge.");
                return;
            }

            if (selection.Count == 0)
            {
                // Fallback to active view if nothing selected for non-merge operations
                selection = new FilteredElementCollector(doc, doc.ActiveView.Id)
                    .OfClass(typeof(TextNote))
                    .Cast<TextNote>()
                    .ToList();
            }

            if (selection.Count == 0)
            {
                StatusCallback?.Invoke("No Text Notes found in selection or active view.");
                return;
            }

            try
            {
                using (Transaction t = new Transaction(doc, "Text Audit: " + CurrentAction.ToString()))
                {
                    t.Start();
                    int count = 0;

                    switch (CurrentAction)
                    {
                        case AuditAction.Merge:
                            DoMerge(doc, selection, false);
                            count = selection.Count;
                            break;
                        case AuditAction.MergeParagraphs:
                            DoMerge(doc, selection, true);
                            count = selection.Count;
                            break;
                        case AuditAction.RemoveReturns:
                            foreach (var note in selection)
                            {
                                string oldText = note.Text;
                                note.Text = oldText.Replace("\r", " ").Replace("\n", " ");
                                count++;
                            }
                            break;
                        case AuditAction.RemoveExtraSpaces:
                            foreach (var note in selection)
                            {
                                string oldText = note.Text;
                                note.Text = CleanExtraSpaces(oldText);
                                count++;
                            }
                            break;
                        case AuditAction.ToLower:
                            foreach (var note in selection) { note.Text = ProcessLines(note.Text, s => s.ToLower()); count++; }
                            break;
                        case AuditAction.ToUpper:
                            foreach (var note in selection) { note.Text = ProcessLines(note.Text, s => SmartUppercase(s)); count++; }
                            break;
                        case AuditAction.ToTitle:
                            var textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
                            foreach (var note in selection) { note.Text = ProcessLines(note.Text, s => textInfo.ToTitleCase(s.ToLower())); count++; }
                            break;
                    }

                    t.Commit();
                    StatusCallback?.Invoke($"Successfully processed {count} items.");
                }
            }
            catch (Exception ex)
            {
                StatusCallback?.Invoke("Error: " + ex.Message);
            }
        }

        private void DoMerge(Document doc, List<TextNote> notes, bool preserveParagraphs)
        {
            if (notes.Count < 2) return;

            // Sort by Y descending (top to bottom)
            var sorted = notes.OrderByDescending(n => n.Coord.Y).ToList();
            TextNote mainNote = sorted[0];
            double originalWidth = mainNote.Width;

            string separator = preserveParagraphs ? "\r\n\r\n" : " ";

            for (int i = 1; i < sorted.Count; i++)
            {
                string textToAppend = sorted[i].Text;
                
                if (preserveParagraphs)
                {
                    if (!textToAppend.StartsWith("\r\n\r\n"))
                        mainNote.Text += separator + textToAppend;
                    else
                        mainNote.Text += textToAppend;
                }
                else
                {
                    if (!textToAppend.StartsWith(" "))
                        mainNote.Text += separator + textToAppend;
                    else
                        mainNote.Text += textToAppend;
                }

                doc.Delete(sorted[i].Id);
            }

            mainNote.Width = originalWidth;
        }

        private string CleanExtraSpaces(string text)
        {
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            List<string> cleaned = new List<string>();

            foreach (var line in lines)
            {
                // Preserve marker logic from Python
                var match = Regex.Match(line, @"^[ \t]*(\*|-|\d+\.|[a-zA-Z]\.)\s");
                if (match.Success)
                {
                    string marker = match.Value;
                    string content = Regex.Replace(line.Substring(marker.Length), @" +", " ").Trim();
                    cleaned.Add(marker + content);
                }
                else
                {
                    cleaned.Add(Regex.Replace(line, @" +", " ").Trim());
                }
            }
            return string.Join("\r\n", cleaned);
        }

        private string ProcessLines(string text, Func<string, string> caseFunc)
        {
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var processed = lines.Select(line => ChangeCasePreservingMarker(line, caseFunc));
            return string.Join("\r\n", processed);
        }

        private string ChangeCasePreservingMarker(string line, Func<string, string> caseFunc)
        {
            var match = BulletRegex.Match(line);
            if (match.Success)
            {
                string prefix = match.Groups[1].Value;
                string content = match.Groups[2].Value;
                return prefix + caseFunc(content);
            }
            return caseFunc(line);
        }

        private string SmartUppercase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 1. Initial Uppercase
            string s = text.ToUpper();

            // 2. Restore Products
            foreach (var kvp in Products)
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

            // 5. Restore Units
            foreach (var kvp in Units.OrderByDescending(x => x.Key.Length))
            {
                s = Regex.Replace(s, $@"(\d+(?:\.\d+)?)(\s*){kvp.Key}(?![A-Z0-9°²³])", $"$1$2{kvp.Value}", RegexOptions.IgnoreCase);
                s = Regex.Replace(s, $@"\b{kvp.Key}\b", kvp.Value, RegexOptions.IgnoreCase);
            }

            // 6. Dimensions (300X45 -> 300x45)
            s = Regex.Replace(s, @"(\d+)(?:\s*X\s*\d+)+", m => m.Value.Replace("X", "x").Replace(" ", ""), RegexOptions.IgnoreCase);

            return s;
        }

        public string GetName() => "Text Audit Handler";
    }
}
