using System;
using Autodesk.Revit.UI;
using antiGGGravity.Commands.General;
using antiGGGravity.StructuralRebar;

namespace antiGGGravity.Commands.Rebar
{
    public class RebarPaletteEventHandler : IExternalEventHandler
    {
        public string CommandToPost { get; set; }

        public void Execute(UIApplication app)
        {
            if (string.IsNullOrEmpty(CommandToPost)) return;

            try
            {
                switch (CommandToPost)
                {
                    case "SetObscured": new SetObscuredCommand().Run(app); break;
                    case "SetUnobscured": new SetUnobscuredCommand().Run(app); break;
                    case "ShowRebar": new ShowRebarCommand().Run(app); break;
                    case "HideRebar": new HideRebarCommand().Run(app); break;
                    case "ShowByHostOnly": new ShowRebarByHostOnlyCommand().Run(app); break;
                    case "ShowByHost": new ShowRebarByHostCommand().Run(app); break;
                    case "HideByHost": new HideRebarByHostCommand().Run(app); break;
                    case "SelectByHost": new SelectRebarCommand().Run(app); break;
                    case "DeleteByHost": new SelectDeleteRebarCommand().Run(app); break;
                    case "RebarCranked": new RebarCrankCommand().Run(app); break;
                    case "RebarSplit": new RebarSplitCommand().Run(app); break;
                    case "QuickPick": new PickElementsCommand().Run(app); break;
                    case "QuickFilter": new antiGGGravity.Commands.Overrides.QuickFilterCommand().Run(app); break;
                    case "BeamRebar": new BeamRebarCommand().Run(app); break;
                    case "FoundationRebar": new FoundationRebarCommand().Run(app); break;
                    case "WallRebar": new WallRebarCommand().Run(app); break;
                    case "ColumnRebar": new ColumnRebarCommand().Run(app); break;
                    default:
                        TaskDialog.Show("Rebar Palette", $"Unknown command: {CommandToPost}");
                        break;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Rebar Palette Error", $"Failed to execute {CommandToPost}.\n{ex.Message}");
            }

            CommandToPost = null;
        }

        public string GetName() => "Rebar Palette Event Handler";
    }
}
