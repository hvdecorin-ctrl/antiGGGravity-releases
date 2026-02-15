using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Views.ProjectAudit
{
    public partial class FamilyDuplicatorView : Window
    {
        private readonly ExternalEvent _dupEvent;
        private readonly FamilyDuplicationHandler _handler;
        public ObservableCollection<DuplicationRow> Rows { get; set; } = new ObservableCollection<DuplicationRow>();

        public FamilyDuplicatorView(Document doc, ExternalEvent dupEvent, FamilyDuplicationHandler handler)
        {
            InitializeComponent();
            _dupEvent = dupEvent;
            _handler = handler;

            LoadBaseTypes(doc);
            UI_Grid.ItemsSource = Rows;
            
            // Allow pasting
            UI_Grid.PreviewKeyDown += UI_Grid_PreviewKeyDown;
        }

        private void LoadBaseTypes(Document doc)
        {
            var categories = new[]
            {
                BuiltInCategory.OST_StructuralFoundation,
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming
            };

            var types = new List<string>();
            foreach (var cat in categories)
            {
                var collector = new FilteredElementCollector(doc)
                    .OfCategory(cat)
                    .WhereElementIsElementType();

                foreach (Element ftype in collector)
                {
                    string name = ftype.Name;
                    string familyName = "";
                    if (ftype is FamilySymbol fs) familyName = fs.Family.Name;
                    
                    string fullName = string.IsNullOrEmpty(familyName) ? name : $"{familyName}:{name}";
                    types.Add(fullName);
                }
            }

            UI_Col_BaseType.ItemsSource = types.OrderBy(t => t).ToList();
        }

        private void UI_Grid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.V && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            {
                PasteFromClipboard();
                e.Handled = true;
            }
        }

        private void PasteFromClipboard()
        {
            string text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text)) return;

            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string[] parts = line.Split('\t');
                var row = new DuplicationRow();
                if (parts.Length > 0) row.TypeMark = parts[0];
                if (parts.Length > 1) row.TypeComment = parts[1];
                if (parts.Length > 2) row.Description = parts[2];
                if (parts.Length > 3) row.BaseType = parts[3];
                
                Rows.Add(row);
            }
        }

        private void UI_Btn_Clear_Click(object sender, RoutedEventArgs e)
        {
            Rows.Clear();
        }

        private void UI_Btn_Create_Click(object sender, RoutedEventArgs e)
        {
            var validRows = Rows.Where(r => !string.IsNullOrEmpty(r.TypeMark) && !string.IsNullOrEmpty(r.BaseType)).ToList();
            if (!validRows.Any())
            {
                UI_Status.Text = "No valid rows to process (Type Mark and Base Type are required).";
                return;
            }

            _handler.RowsToProcess = validRows;
            _dupEvent.Raise();
            UI_Status.Text = "Duplication started...";
        }

        private void UI_Btn_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class DuplicationRow
    {
        public string TypeMark { get; set; }
        public string TypeComment { get; set; }
        public string Description { get; set; }
        public string BaseType { get; set; }
    }

    public class FamilyDuplicationHandler : IExternalEventHandler
    {
        public List<DuplicationRow> RowsToProcess { get; set; } = new List<DuplicationRow>();

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            int success = 0;
            int failed = 0;

            using (Transaction t = new Transaction(doc, "Duplicate Families"))
            {
                t.Start();
                foreach (var row in RowsToProcess)
                {
                    try
                    {
                        // Find base type
                        ElementType baseType = FindElementType(doc, row.BaseType);
                        if (baseType == null) { failed++; continue; }

                        // Generate new name: Type Mark + Type Comment
                        string newName = string.IsNullOrEmpty(row.TypeComment) 
                            ? row.TypeMark 
                            : $"{row.TypeMark}-{row.TypeComment}";

                        // Duplicate
                        ElementType newType = baseType.Duplicate(newName) as ElementType;
                        if (newType == null) { failed++; continue; }

                        // Set parameters
                        newType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK)?.Set(row.TypeMark);
                        newType.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION)?.Set(row.Description);
                        newType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS)?.Set(row.TypeComment);

                        success++;
                    }
                    catch { failed++; }
                }
                t.Commit();
                TaskDialog.Show("Duplication Complete", $"Created {success} types. Failed: {failed}.");
            }
        }

        private ElementType FindElementType(Document doc, string fullName)
        {
            return new FilteredElementCollector(doc)
                .WhereElementIsElementType()
                .Cast<ElementType>()
                .FirstOrDefault(e => {
                    string name = e.Name;
                    string famName = "";
                    if (e is FamilySymbol fs) famName = fs.Family.Name;
                    string combined = string.IsNullOrEmpty(famName) ? name : $"{famName}:{name}";
                    return combined == fullName || name == fullName;
                });
        }

        public string GetName() => "Family Duplication Event Handler";
    }
}
