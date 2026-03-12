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
                            foreach (var note in selection) { note.Text = ProcessLines(note.Text, s => s.ToUpper()); count++; }
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

        public string GetName() => "Text Audit Handler";
    }
}
