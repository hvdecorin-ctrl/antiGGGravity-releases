using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Windows.Media;
using System.Windows.Shapes;
using antiGGGravity.StructuralRebar.DTO;
using antiGGGravity.StructuralRebar.Core.Geometry;
using antiGGGravity.StructuralRebar.Constants;

namespace antiGGGravity.StructuralRebar.UI.Panels
{
    public partial class BeamAdvancePanel : UserControl
    {
        private UIDocument _uiDoc;
        private Document _doc;
        private IRebarWindow _parentWindow;

        public ObservableCollection<string> BarTypes { get; set; } = new ObservableCollection<string>();
        
        // Expose underlying data directly to the grid
        public ObservableCollection<SupportOverride> SupportData { get; set; } = new ObservableCollection<SupportOverride>();
        public ObservableCollection<SpanOverride> SpanData { get; set; } = new ObservableCollection<SpanOverride>();
        
        private ElementId _targetBeamId = ElementId.InvalidElementId;
        public ElementId TargetBeamId => _targetBeamId;

        // Geometry cache for drawing
        private List<BeamSpanResolver.SupportInfo> _lastSupports = new List<BeamSpanResolver.SupportInfo>();
        private double _totalLength = 0;

        public BeamAdvancePanel(UIDocument uiDoc, IRebarWindow parentWindow)
        {
            InitializeComponent();
            _uiDoc = uiDoc;
            _doc = uiDoc?.Document;
            _parentWindow = parentWindow;

            try 
            {
                if (_doc != null) LoadBarTypes();
            }
            catch (Exception ex)
            {
                // Log or silently fail to avoid Revit crash
                System.Diagnostics.Debug.WriteLine("BeamAdvancePanel: LoadBarTypes failed: " + ex.Message);
            }
            
            this.DataContext = this;
            UI_Grid_Supports.ItemsSource = SupportData;
            UI_Grid_Spans.ItemsSource = SpanData;

            SupportData.CollectionChanged += (s, e) => {
                if (e.NewItems != null) foreach (SupportOverride item in e.NewItems) item.PropertyChanged += OnDataItemChanged;
                RedrawPreview();
            };
            SpanData.CollectionChanged += (s, e) => {
                if (e.NewItems != null) foreach (SpanOverride item in e.NewItems) item.PropertyChanged += OnDataItemChanged;
                RedrawPreview();
            };
        }

        private void OnDataItemChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.EndsWith("BarTypeName"))
            {
                if (sender is SupportOverride sup)
                {
                    if (e.PropertyName == nameof(SupportOverride.T2_BarTypeName))
                    {
                        if (!string.IsNullOrEmpty(sup.T2_BarTypeName) && sup.T2_Count == 0)
                            sup.T2_Count = 2;
                    }
                    else if (e.PropertyName == nameof(SupportOverride.T3_BarTypeName))
                    {
                        if (!string.IsNullOrEmpty(sup.T3_BarTypeName) && sup.T3_Count == 0)
                            sup.T3_Count = 2;
                    }
                }
                else if (sender is SpanOverride span)
                {
                    if (e.PropertyName == nameof(SpanOverride.B2_BarTypeName))
                    {
                        if (!string.IsNullOrEmpty(span.B2_BarTypeName) && span.B2_Count == 0)
                            span.B2_Count = 2;
                    }
                    else if (e.PropertyName == nameof(SpanOverride.B3_BarTypeName))
                    {
                        if (!string.IsNullOrEmpty(span.B3_BarTypeName) && span.B3_Count == 0)
                            span.B3_Count = 2;
                    }
                }
            }

            RedrawPreview();
        }

        private void UI_Canvas_Preview_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawPreview();
        }

        private void LoadBarTypes()
        {
            BarTypes.Clear();
            BarTypes.Add(""); // Allow null/empty selection

            var types = new FilteredElementCollector(_doc)
                .OfClass(typeof(Autodesk.Revit.DB.Structure.RebarBarType))
                .Cast<Autodesk.Revit.DB.Structure.RebarBarType>()
                .Select(b => b.Name)
                .OrderBy(n => n)
                .ToList();

            foreach (var t in types) BarTypes.Add(t);
        }

        private void UI_Button_Analyze_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.Close();

            try
            {
                // Prompts user to select a beam in Revit UI
                Reference extRef = _uiDoc.Selection.PickObject(ObjectType.Element, new BeamSelectionFilter(), "Please select a continuous beam");
                _targetBeamId = extRef.ElementId;
                
                FamilyInstance beam = _doc.GetElement(_targetBeamId) as FamilyInstance;
                
                // Ensure selection visually updates in Revit
                _uiDoc.Selection.SetElementIds(new List<ElementId>{_targetBeamId});

                // Analyze beam to find spans and supports
                AnalyzeBeamGeometry(beam);

                UI_Text_Status.Text = $"Analysis complete. Found {SupportData.Count} supports and {SpanData.Count} spans.";
                UI_Text_Status.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 139, 34)); // DarkGreen
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                UI_Text_Status.Text = "Selection cancelled.";
                UI_Text_Status.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 50, 50));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error analyzing beam: {ex.Message}", "Analysis Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UI_Text_Status.Text = "Analysis failed.";
            }
            finally
            {
                _parentWindow.ReShow();
            }
        }

        private void UI_Button_AddSupport_Click(object sender, RoutedEventArgs e)
        {
            if (_lastSupports == null || _totalLength <= 0 || _targetBeamId == ElementId.InvalidElementId) return;

            _parentWindow.Close();
            try
            {
                // Prompts user to select beams in Revit UI
                IList<Reference> refs = _uiDoc.Selection.PickObjects(ObjectType.Element, new BeamSelectionFilter(), "Select intersecting beams to act as supports");
                if (refs == null || refs.Count == 0) return;

                // We need the original chain's line to project the picked beam onto
                FamilyInstance hostBeam = _doc.GetElement(_targetBeamId) as FamilyInstance;
                var pool = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_StructuralFraming)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .ToList();

                var groups = BeamSpanResolver.GroupSelectedBeams(new List<FamilyInstance> { hostBeam }.Concat(pool).ToList());
                var targetChain = groups.FirstOrDefault(g => g.Any(b => b.Id == hostBeam.Id));
                if (targetChain == null || targetChain.Count == 0) return;

                XYZ firstEnd = (targetChain.First().Location as LocationCurve).Curve.GetEndPoint(0);
                XYZ lastEnd = (targetChain.Last().Location as LocationCurve).Curve.GetEndPoint(1);
                
                XYZ lineDir = (lastEnd - firstEnd).Normalize();
                XYZ linePerp = new XYZ(-lineDir.Y, lineDir.X, 0);
                XYZ beamStart2D = new XYZ(firstEnd.X, firstEnd.Y, 0);

                foreach (Reference extRef in refs)
                {
                    FamilyInstance pickedBeam = _doc.GetElement(extRef.ElementId) as FamilyInstance;
                    if (pickedBeam == null) continue;

                    BoundingBoxXYZ bbox = pickedBeam.get_BoundingBox(null);
                    if (bbox != null)
                    {
                        // Get corner projection min/max along our line
                        XYZ[] corners = {
                            new XYZ(bbox.Min.X, bbox.Min.Y, 0),
                            new XYZ(bbox.Max.X, bbox.Min.Y, 0),
                            new XYZ(bbox.Max.X, bbox.Max.Y, 0),
                            new XYZ(bbox.Min.X, bbox.Max.Y, 0)
                        };

                        double minAlong = double.MaxValue;
                        double maxAlong = double.MinValue;
                        foreach (var corner in corners)
                        {
                            double along = (corner - beamStart2D).DotProduct(lineDir);
                            if (along < minAlong) minAlong = along;
                            if (along > maxAlong) maxAlong = along;
                        }

                        double centerOffset = (minAlong + maxAlong) / 2.0;

                        // Create new support and inject
                        var newSupport = new BeamSpanResolver.SupportInfo
                        {
                            ElementId = pickedBeam.Id,
                            CenterOffset = centerOffset,
                            NearFaceOffset = minAlong,
                            FarFaceOffset = maxAlong,
                            SupportWidth = maxAlong - minAlong,
                            IsEndSupport = false
                        };

                        _lastSupports.Add(newSupport);
                    }
                }

                // Cleanup and sort again
                _lastSupports = _lastSupports.OrderBy(s => s.CenterOffset)
                                                .GroupBy(s => Math.Round(s.CenterOffset, 2))
                                                .Select(g => g.First())
                                                .ToList();

                double endTol = 100.0 / 304.8; 
                for (int i = 0; i < _lastSupports.Count; i++)
                {
                    var s = _lastSupports[i];
                    s.IsEndSupport = (s.CenterOffset < endTol) || (s.CenterOffset > _totalLength - endTol);
                    _lastSupports[i] = s;
                }

                PopulateUIFromSupports(targetChain);

                UI_Text_Status.Text = $"{refs.Count} support(s) processed. Now {SupportData.Count} supports and {SpanData.Count} spans.";
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                UI_Text_Status.Text = "Manual support selection cancelled.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding support: {ex.Message}", "Support Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _parentWindow.ReShow();
            }
        }

        private void AnalyzeBeamGeometry(FamilyInstance beam)
        {
            // First, find complete continuous chain
            var pool = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            var groups = BeamSpanResolver.GroupSelectedBeams(new List<FamilyInstance> { beam }.Concat(pool).ToList());
            
            // Find the chain our chosen beam belongs to
            var targetChain = groups.FirstOrDefault(g => g.Any(b => b.Id == beam.Id));
            if (targetChain == null || targetChain.Count == 0) return;

            // Resolve full geometry limits
            XYZ firstEnd = (targetChain.First().Location as LocationCurve).Curve.GetEndPoint(0);
            XYZ lastEnd = (targetChain.Last().Location as LocationCurve).Curve.GetEndPoint(1);
            var hostGeom = BeamGeometryModule.Read(_doc, targetChain.First());
            double width = hostGeom.Width;
            double minZ = hostGeom.SolidZMin - 2.0;
            double maxZ = hostGeom.SolidZMax + 2.0;

            var excludeIds = targetChain.Select(b => b.Id).ToList();

            _totalLength = firstEnd.DistanceTo(lastEnd);
            _lastSupports = BeamSpanResolver.FindSupportsAlongLine(_doc, firstEnd, lastEnd, width, excludeIds, minZ, maxZ);
            
            PopulateUIFromSupports(targetChain);
            
            UI_Button_AddSupport.IsEnabled = true;
        }

        private void PopulateUIFromSupports(List<FamilyInstance> targetChain)
        {
            List<BeamSpanResolver.SupportInfo> supports = _lastSupports;

            bool isStartCantileverGeom = supports.Count > 0 && supports[0].NearFaceOffset > 0.1;
            bool isEndCantileverGeom = supports.Count > 0 && supports.Last().FarFaceOffset < _totalLength - 0.1;

            // Populate Support Models
            SupportData.Clear();
            for (int i = 0; i < supports.Count; i++)
            {
                string name = $"Support {i + 1}";
                if (supports[i].IsEndSupport) name += " (End)";
                else name += " (Internal)";

                bool isCantilever = false;
                if (i == 0 && isStartCantileverGeom) isCantilever = true;
                if (i == supports.Count - 1 && isEndCantileverGeom) isCantilever = true;

                SupportData.Add(new SupportOverride
                {
                    SupportIndex = i,
                    SupportName = name,
                    IsCantilever = isCantilever,
                    CenterOffset = supports[i].CenterOffset,
                    NearFaceOffset = supports[i].NearFaceOffset,
                    FarFaceOffset = supports[i].FarFaceOffset,
                    SupportWidth = supports[i].SupportWidth,
                    IsEndSupport = supports[i].IsEndSupport,
                    T2_Count = 0,
                    T3_Count = 0
                });
            }

            // Populate Span Models (Count is strictly the number of spaces between supports)
            SpanData.Clear();
            int spanCount = Math.Max(0, supports.Count - 1);

            // If there's only one beam and no supports detected, it's 1 span
            if (spanCount == 0 && targetChain.Count > 0) spanCount = 1;

            for (int i = 0; i < spanCount; i++)
            {
                SpanData.Add(new SpanOverride
                {
                    SpanIndex = i,
                    SpanName = $"Span {i + 1}",
                    B2_Count = 0,
                    B3_Count = 0
                });
            }

            RedrawPreview();
        }

        private void RedrawPreview()
        {
            if (UI_Canvas_Preview == null || _totalLength <= 0) return;

            UI_Canvas_Preview.Children.Clear();

            double canvasW = UI_Canvas_Preview.ActualWidth;
            double canvasH = UI_Canvas_Preview.ActualHeight;
            if (canvasW <= 0 || canvasH <= 0) return;

            double scale = canvasW / _totalLength;
            double beamHeight = 40; // Pixels
            double yMid = canvasH / 2;
            double yTop = yMid - beamHeight / 2;
            double yBot = yMid + beamHeight / 2;

            // 1. Draw Beam Main Body (Concrete)
            System.Windows.Shapes.Rectangle beamRect = new System.Windows.Shapes.Rectangle
            {
                Width = canvasW,
                Height = beamHeight,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                Stroke = Brushes.LightGray,
                StrokeThickness = 1
            };
            Canvas.SetLeft(beamRect, 0);
            Canvas.SetTop(beamRect, yTop);
            UI_Canvas_Preview.Children.Add(beamRect);

            // 2. Draw Supports
            foreach (var sup in _lastSupports)
            {
                double xS = sup.NearFaceOffset * scale;
                double xE = sup.FarFaceOffset * scale;
                double w = Math.Max(5, xE - xS);

                System.Windows.Shapes.Rectangle supRect = new System.Windows.Shapes.Rectangle
                {
                    Width = w,
                    Height = 30,
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220)),
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(supRect, xS);
                Canvas.SetTop(supRect, yBot);
                UI_Canvas_Preview.Children.Add(supRect);

                // Support Label
                TextBlock txt = new TextBlock { Text = sup.IsEndSupport ? "E" : "I", FontSize = 9, Foreground = Brushes.Gray };
                Canvas.SetLeft(txt, xS + w / 2 - 3);
                Canvas.SetTop(txt, yBot + 32);
                UI_Canvas_Preview.Children.Add(txt);
            }

            bool isStartCantilever = SupportData.Count > 0 && SupportData[0].IsCantilever;
            bool isEndCantilever = SupportData.Count > 0 && SupportData.Last().IsCantilever;

            // 3. Draw Rebar (Elevation)
            var t2Segments = new List<(double Start, double End)>();
            var t3Segments = new List<(double Start, double End)>();
            var b2Segments = new List<(double Start, double End)>();
            var b3Segments = new List<(double Start, double End)>();

            // Correct span calculation including cantilevers
            var fullClearSpans = new List<(double Start, double End)>();
            double currentPos = 0;
            foreach (var sup in _lastSupports)
            {
                if (sup.NearFaceOffset > currentPos + 0.01)
                    fullClearSpans.Add((currentPos, sup.NearFaceOffset));
                currentPos = sup.FarFaceOffset;
            }
            if (_totalLength > currentPos + 0.01)
                fullClearSpans.Add((currentPos, _totalLength));

            // Populate top layer segments
            for (int i = 0; i < SupportData.Count; i++)
            {
                var over = SupportData[i];
                if (over.T2_Count > 0 && !string.IsNullOrEmpty(over.T2_BarTypeName))
                {
                    var seg = antiGGGravity.StructuralRebar.Core.Calculators.AdditionalBarCalculator.GetTopSegmentForSupport(i, _totalLength, fullClearSpans, isStartCantilever, isEndCantilever);
                    if (seg.HasValue) t2Segments.Add(seg.Value);
                }
                if (over.T3_Count > 0 && !string.IsNullOrEmpty(over.T3_BarTypeName))
                {
                    var seg = antiGGGravity.StructuralRebar.Core.Calculators.AdditionalBarCalculator.GetTopSegmentForSupport(i, _totalLength, fullClearSpans, isStartCantilever, isEndCantilever);
                    if (seg.HasValue) t3Segments.Add(seg.Value);
                }
            }

            // Populate bottom layer segments
            for (int i = 0; i < SpanData.Count; i++)
            {
                var over = SpanData[i];
                int targetSpanIdx = isStartCantilever ? i + 1 : i;

                if (over.B2_Count > 0 && !string.IsNullOrEmpty(over.B2_BarTypeName))
                {
                    var seg = antiGGGravity.StructuralRebar.Core.Calculators.AdditionalBarCalculator.GetBottomSegmentForSpan(targetSpanIdx, fullClearSpans, isStartCantilever, isEndCantilever, true);
                    if (seg.HasValue) b2Segments.Add(seg.Value);
                }
                if (over.B3_Count > 0 && !string.IsNullOrEmpty(over.B3_BarTypeName))
                {
                    var seg = antiGGGravity.StructuralRebar.Core.Calculators.AdditionalBarCalculator.GetBottomSegmentForSpan(targetSpanIdx, fullClearSpans, isStartCantilever, isEndCantilever, false);
                    if (seg.HasValue) b3Segments.Add(seg.Value);
                }
            }

            // Draw Merged Layers
            // T2 (Warm - OrangeRed)
            foreach (var m in MergeSegments(t2Segments)) DrawRawRebarLine(m.Start, m.End, scale, yTop, 6, Brushes.OrangeRed, true);
            // T3 (Warm - Gold)
            foreach (var m in MergeSegments(t3Segments)) DrawRawRebarLine(m.Start, m.End, scale, yTop, 11, Brushes.Gold, true);
            // B2 (Cold - ForestGreen)
            foreach (var m in MergeSegments(b2Segments)) DrawRawRebarLine(m.Start, m.End, scale, yBot, -6, Brushes.ForestGreen, false);
            // B3 (Cold - DeepSkyBlue)
            foreach (var m in MergeSegments(b3Segments)) DrawRawRebarLine(m.Start, m.End, scale, yBot, -11, Brushes.DeepSkyBlue, false);
        }

        private List<(double Start, double End)> MergeSegments(List<(double Start, double End)> segments)
        {
            if (segments.Count <= 1) return segments;
            var sorted = segments.OrderBy(s => s.Start).ToList();
            var merged = new List<(double Start, double End)>();
            var current = sorted[0];
            for (int i = 1; i < sorted.Count; i++)
            {
                if (current.End >= sorted[i].Start - 0.01) current = (current.Start, Math.Max(current.End, sorted[i].End));
                else { merged.Add(current); current = sorted[i]; }
            }
            merged.Add(current);
            return merged;
        }

        private void DrawRawRebarLine(double start, double end, double scale, double yRef, double yOff, Brush brush, bool isTop)
        {
            double x1 = start * scale;
            double x2 = end * scale;
            double y = yRef + yOff;

            System.Windows.Shapes.Line line = new System.Windows.Shapes.Line { X1 = x1, X2 = x2, Y1 = y, Y2 = y, Stroke = brush, StrokeThickness = 2 };
            UI_Canvas_Preview.Children.Add(line);

            if (isTop)
            {
                if (start < 0.1)
                {
                    System.Windows.Shapes.Line hook = new System.Windows.Shapes.Line { X1 = x1, X2 = x1, Y1 = y, Y2 = y + 10, Stroke = brush, StrokeThickness = 2 };
                    UI_Canvas_Preview.Children.Add(hook);
                }
                if (end > _totalLength - 0.1)
                {
                    System.Windows.Shapes.Line hook = new System.Windows.Shapes.Line { X1 = x2, X2 = x2, Y1 = y, Y2 = y + 10, Stroke = brush, StrokeThickness = 2 };
                    UI_Canvas_Preview.Children.Add(hook);
                }
            }
        }

        public void SaveSettings()
        {
            // Empty. The RebarEngine directly reads the Overrides DTO list from the main engine mapping.
            // Ideally RebarSuiteWindow collects state from panels right before Execute.
        }

        public void InjectDTO(RebarRequest request)
        {
            request.SupportOverrides = SupportData.ToList();
            request.SpanOverrides = SpanData.ToList();
            request.HostType = ElementHostType.BeamAdvance;
        }
    }

    /// <summary>
    /// Restricts Revit selection only to Structural Framing (Beams).
    /// </summary>
    public class BeamSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem.Category != null && 
                   elem.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFraming && 
                   elem is FamilyInstance;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false; // Not allowing sub-element geometry selection
        }
    }
}
