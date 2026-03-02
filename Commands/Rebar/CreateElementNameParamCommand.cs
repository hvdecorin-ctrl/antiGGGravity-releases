using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.Rebar
{
    /// <summary>
    /// Tool 1: Creates the shared parameter "Element Name" and binds it to structural categories.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CreateElementNameParamCommand : BaseCommand
    {
        protected override bool RequiresLicense => false;

        private const string ParamName = "Element Name";
        private const string GroupName = "antiGGGravity";

        /// <summary>
        /// The structural categories that receive the "Element Name" parameter.
        /// </summary>
        private static readonly BuiltInCategory[] TargetCategories = new[]
        {
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_Stairs,
            BuiltInCategory.OST_GenericModel
        };

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument?.Document;

            if (doc == null)
            {
                TaskDialog.Show("Element Name", "Please open a project first.");
                return Result.Cancelled;
            }

            // Check if the parameter already exists in the project
            if (ParameterAlreadyExists(doc))
            {
                TaskDialog.Show("Element Name",
                    "The shared parameter \"Element Name\" already exists in this project.\n\n" +
                    "No action needed.");
                return Result.Succeeded;
            }

            // Create the shared parameter and bind to categories
            try
            {
                using (Transaction t = new Transaction(doc, "Create Element Name Parameter"))
                {
                    t.Start();
                    CreateAndBindParameter(doc, uiApp.Application);
                    t.Commit();
                }

                string categoryList = string.Join("\n", TargetCategories.Select(c =>
                {
                    var cat = Category.GetCategory(doc, c);
                    return cat != null ? $"  ✓ {cat.Name}" : null;
                }).Where(s => s != null));

                TaskDialog.Show("Element Name",
                    $"Successfully created shared parameter \"Element Name\" and bound to:\n\n{categoryList}\n\n" +
                    "You can now use Tool 2 to assign names.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Element Name Error", $"Failed to create parameter:\n\n{ex.Message}");
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        private bool ParameterAlreadyExists(Document doc)
        {
            // Check if any element in the target categories has the "Element Name" parameter
            foreach (var bic in TargetCategories)
            {
                var collector = new FilteredElementCollector(doc)
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .FirstElement();

                if (collector != null)
                {
                    var param = collector.LookupParameter(ParamName);
                    if (param != null) return true;
                }
            }

            // Also check shared parameter bindings directly
            BindingMap bindingMap = doc.ParameterBindings;
            var iterator = bindingMap.ForwardIterator();
            while (iterator.MoveNext())
            {
                if (iterator.Key.Name == ParamName) return true;
            }

            return false;
        }

        private void CreateAndBindParameter(Document doc, Autodesk.Revit.ApplicationServices.Application app)
        {
            // Get or create a temporary shared parameter file
            string originalFile = app.SharedParametersFilename;
            string tempFile = Path.Combine(Path.GetTempPath(), "antiGGGravity_SharedParams.txt");

            try
            {
                // Create temp shared param file if needed
                if (!File.Exists(tempFile))
                {
                    using (var fs = File.Create(tempFile)) { }
                }

                app.SharedParametersFilename = tempFile;
                DefinitionFile defFile = app.OpenSharedParameterFile();

                // Create or get the group
                DefinitionGroup group = defFile.Groups.get_Item(GroupName);
                if (group == null)
                {
                    group = defFile.Groups.Create(GroupName);
                }

                // Create or get the definition
                Definition definition = group.Definitions.get_Item(ParamName);
                if (definition == null)
                {
                    ExternalDefinitionCreationOptions options =
                        new ExternalDefinitionCreationOptions(ParamName, SpecTypeId.String.Text);
                    options.Description = "Element naming for rebar host mark assignment (TypeMark-Number)";
                    definition = group.Definitions.Create(options);
                }

                // Build the category set
                CategorySet categorySet = app.Create.NewCategorySet();
                foreach (var bic in TargetCategories)
                {
                    var cat = Category.GetCategory(doc, bic);
                    if (cat != null && cat.AllowsBoundParameters)
                    {
                        categorySet.Insert(cat);
                    }
                }

                // Create instance binding (each element gets its own value)
                InstanceBinding binding = app.Create.NewInstanceBinding(categorySet);

                // Add to project
                doc.ParameterBindings.Insert(definition, binding, GroupTypeId.IdentityData);
            }
            finally
            {
                // Restore original shared param file
                if (!string.IsNullOrEmpty(originalFile))
                {
                    app.SharedParametersFilename = originalFile;
                }
            }
        }
    }
}
