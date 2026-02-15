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
            XYZ min = box.Min;
            XYZ max = box.Max;

            // Transform logic from python script (simplified)
            // Python: proj(pt), vtm(pt)
            // PickBox returns points in world coordinates but bounded by screen? 
            // The python script logic projects these points onto the view plane.
            
            XYZ right = activeView.RightDirection;
            XYZ up = activeView.UpDirection;
            XYZ origin = activeView.Origin;

            // Logic to convert PickBox to View Plane Curves
            // Simplified: PickBox Min/Max are usually correct for Plan Views in World Coords.
            // For general views, we need to ensure we are creating a rectangle on the view plane.
            
            // Assume Plan View for now or simple projection
            double z = origin.Z; 
            XYZ p1 = new XYZ(min.X, min.Y, z);
            XYZ p2 = new XYZ(max.X, min.Y, z);
            XYZ p3 = new XYZ(max.X, max.Y, z);
            XYZ p4 = new XYZ(min.X, max.Y, z);

            return new List<Curve>
            {
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3),
                Line.CreateBound(p3, p4),
                Line.CreateBound(p4, p1)
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
