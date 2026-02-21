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
    // DUPLICATE SHEETS
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class DuplicateSheetsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                DuplicateSheetsView view = new DuplicateSheetsView(commandData);
                view.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    // ===================================================================================
    // ALIGN SCHEMATIC
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class AlignSchematicCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                AlignSchematicView view = new AlignSchematicView(commandData);
                view.ShowDialog();
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
