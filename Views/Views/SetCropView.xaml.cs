using System;
using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace antiGGGravity.Views.Views
{
    public partial class SetCropView : Window
    {
        private UIDocument _uidoc;
        public object Result { get; private set; }
        public bool IsDrawn { get; private set; }
        public bool IsConfirmed { get; private set; }

        public SetCropView(UIDocument uidoc)
        {
            InitializeComponent();
            _uidoc = uidoc;
        }

        private void Draw_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                PickedBox box = _uidoc.Selection.PickBox(PickBoxStyle.Directional, "Draw crop area");
                if (box != null)
                {
                    Result = GetCurvesFromBox(box);
                    IsDrawn = true;
                    IsConfirmed = true;
                    this.Close();
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                this.Show();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
                this.Show();
            }
        }

        private List<Curve> GetCurvesFromBox(PickedBox box)
        {
            View activeView = _uidoc.Document.ActiveView;
            XYZ origin = activeView.Origin;
            XYZ right = activeView.RightDirection;
            XYZ up = activeView.UpDirection;

            // Porting Python logic: proj(pt) and vtm(pt_v)
            XYZ Proj(XYZ pt)
            {
                XYZ vec = pt - origin;
                return new XYZ(vec.DotProduct(right), vec.DotProduct(up), 0);
            }

            XYZ Vtm(XYZ ptV)
            {
                return origin + (right * ptV.X) + (up * ptV.Y);
            }

            // Project Min and Max to View Plane coordinates
            XYZ pp1 = Proj(box.Min);
            XYZ pp3 = Proj(box.Max);

            // Create 4 points for the rectangle loop on the view plane
            XYZ v1 = Vtm(pp1);
            XYZ v2 = Vtm(new XYZ(pp3.X, pp1.Y, 0));
            XYZ v3 = Vtm(pp3);
            XYZ v4 = Vtm(new XYZ(pp1.X, pp3.Y, 0));

            return new List<Curve>
            {
                Line.CreateBound(v1, v2),
                Line.CreateBound(v2, v3),
                Line.CreateBound(v3, v4),
                Line.CreateBound(v4, v1)
            };
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            Result = "RESET";
            IsConfirmed = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            this.Close();
        }
    }
}
