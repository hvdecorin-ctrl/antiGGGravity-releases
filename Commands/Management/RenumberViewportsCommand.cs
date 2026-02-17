using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.Management
{
    [Transaction(TransactionMode.Manual)]
    public class RenumberViewportsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            if (activeView.ViewType != ViewType.DrawingSheet)
            {
                TaskDialog.Show("Renumber", "Must be on a Sheet.");
                return Result.Cancelled;
            }

            // 1. Get Starting Number
            var inputWindow = new antiGGGravity.Views.Management.RenumberInputWindow();
            if (inputWindow.ShowDialog() != true)
            {
                return Result.Cancelled;
            }

            string currentNumber = inputWindow.InputValue;
            if (string.IsNullOrWhiteSpace(currentNumber)) return Result.Cancelled;

            // 2. Loop for Picking
            try
            {
                while (true)
                {
                    // Prompt to pick
                    Reference r = null;
                    try
                    {
                        r = uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, 
                            new ViewportSelectionFilter(), 
                            $"Select Viewport to be '{currentNumber}' (Esc to stop)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break; // User pressed Esc
                    }

                    Viewport vp = doc.GetElement(r.ElementId) as Viewport;
                    Parameter detailNumParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);

                    using (Transaction t = new Transaction(doc, "Renumber Viewport"))
                    {
                        t.Start();

                        // 3. Collision Handling
                        // Find if any OTHER viewport on this sheet has the same Detail Number
                        Viewport existingCollision = FindViewportWithDetailNumber(doc, activeView.Id, currentNumber);
                        
                        if (existingCollision != null && existingCollision.Id != vp.Id)
                        {
                            Parameter collisionParam = existingCollision.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                            // Rename valid existing viewport to temp
                            string tempName = $"{currentNumber}_temp_{Guid.NewGuid().ToString().Substring(0, 5)}";
                            collisionParam.Set(tempName);
                        }

                        // 4. Set the new number
                        detailNumParam.Set(currentNumber);
                        t.Commit();
                    }

                    // 5. Increment Number for next loop
                    currentNumber = IncrementNumber(currentNumber);
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        private Viewport FindViewportWithDetailNumber(Document doc, ElementId sheetId, string detailNumber)
        {
            var viewports = new FilteredElementCollector(doc, sheetId)
                            .OfClass(typeof(Viewport))
                            .Cast<Viewport>();

            foreach (var vp in viewports)
            {
                Parameter p = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (p != null && p.AsString() == detailNumber)
                {
                    return vp;
                }
            }
            return null;
        }

        private string IncrementNumber(string input)
        {
            // Regex to find the last number in the string
            var match = System.Text.RegularExpressions.Regex.Match(input, @"(\d+)(?!.*\d)");
            if (match.Success)
            {
                string numberStr = match.Groups[1].Value;
                int number = int.Parse(numberStr);
                int nextNumber = number + 1;
                
                // Replace the last number with the incremented one
                // Be careful to only replace the LAST instance
                int index = match.Groups[1].Index;
                int length = match.Groups[1].Length;
                
                return input.Substring(0, index) + nextNumber + input.Substring(index + length);
            }
            
            // Fallback: append "-1" if no number found
            return input + "-1";
        }

        public class ViewportSelectionFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Viewport;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
