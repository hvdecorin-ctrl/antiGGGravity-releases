using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.Management;

namespace antiGGGravity.Commands.Management
{
    // ===================================================================================
    // SHEET MAKER
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class SheetMakerCommand : BaseCommand
    {

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            SheetMakerView view = new SheetMakerView(commandData);
            view.ShowDialog();
            return Result.Succeeded;
        }
    }

    // ===================================================================================
    // ALIGN SCHEMATIC
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class AlignSchematicCommand : BaseCommand
    {

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            AlignSchematicView view = new AlignSchematicView(commandData);
            view.ShowDialog();
            return Result.Succeeded;
        }
    }


}
