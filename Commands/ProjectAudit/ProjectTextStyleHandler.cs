using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System; // Added for Action<string>

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
        Convert
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

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Collect targets based on Scope
            List<TextNote> targets = new List<TextNote>();

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
    }
}
