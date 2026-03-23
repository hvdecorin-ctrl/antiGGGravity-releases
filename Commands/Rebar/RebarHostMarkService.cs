using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.Utilities;

namespace antiGGGravity.Commands.Rebar
{
    public static class RebarHostMarkService
    {
        // Unit weight of steel rebar per diameter (kg/m)
        private static readonly Dictionary<int, double> UnitWeightKgPerM = new Dictionary<int, double>
        {
            { 6,   0.222 },
            { 8,   0.395 },
            { 10,  0.617 },
            { 12,  0.888 },
            { 16,  1.579 },
            { 20,  2.466 },
            { 25,  3.853 },
            { 28,  4.834 },
            { 32,  6.313 },
            { 36,  7.990 },
            { 40,  9.864 },
        };

        public static RebarQuantityResult Scan(UIApplication app)
        {
            try
            {
                var uiDoc = app.ActiveUIDocument;
                var doc = uiDoc?.Document;
                if (doc == null || !doc.IsValidObject) return null;

                var selectedIds = uiDoc.Selection.GetElementIds();
                IEnumerable<Element> allRebarElements;

                if (selectedIds.Count > 0)
                {
                    allRebarElements = selectedIds.Select(id => doc.GetElement(id))
                        .Where(e => e != null && e.IsValidObject && !(e is ElementType))
                        .Where(e => e.Category != null && e.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_Rebar)
                        .ToList();
                }
                else
                {
                    allRebarElements = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rebar)
                        .WhereElementIsNotElementType()
                        .Where(e => e.IsValidObject)
                        .ToList();
                }

                var rawEntries = new List<(string host, int dia, double lengthM)>();

                foreach (var elem in allRebarElements)
                {
                    try
                    {
                        if (elem == null || !elem.IsValidObject) continue;

                        string hostMark = GetHostMark(doc, elem);
                        int diaMm = GetBarDiameterMm(elem);
                        double lengthM = GetTotalLengthMeters(elem);

                        if (diaMm > 0)
                        {
                            rawEntries.Add((hostMark, diaMm, lengthM));
                        }
                    }
                    catch { /* Skip problematic individual bars */ }
                }

                if (rawEntries.Count == 0) return new RebarQuantityResult();

                var diameters = rawEntries.Select(e => e.dia).Distinct().OrderBy(d => d).ToList();
                var grouped = rawEntries.GroupBy(e => e.host);
                var rowDict = new Dictionary<string, RebarHostRow>();

                foreach (var grp in grouped)
                {
                    try
                    {
                        var row = new RebarHostRow { HostCategory = grp.Key };
                        foreach (var diaGrp in grp.GroupBy(e => e.dia))
                        {
                            double totalLen = diaGrp.Sum(e => e.lengthM);
                            double unitWt = GetUnitWeight(diaGrp.Key);
                            double totalWt = totalLen * unitWt;

                            row.DiameterData[diaGrp.Key] = new RebarCellData
                            {
                                TotalLengthM = Math.Round(totalLen, 1),
                                TotalWeightKg = Math.Round(totalWt, 1)
                            };
                        }
                        rowDict[grp.Key] = row;
                    }
                    catch { }
                }

                var rows = rowDict.Values.OrderBy(r => r.HostCategory).ToList();

                var totalLenPerDia = new Dictionary<int, double>();
                var totalWtPerDia = new Dictionary<int, double>();

                foreach (var dia in diameters)
                {
                    double sumLen = rows.Sum(r => r.DiameterData.ContainsKey(dia) ? r.DiameterData[dia].TotalLengthM : 0);
                    double sumWt = rows.Sum(r => r.DiameterData.ContainsKey(dia) ? r.DiameterData[dia].TotalWeightKg : 0);
                    totalLenPerDia[dia] = Math.Round(sumLen, 1);
                    totalWtPerDia[dia] = Math.Round(sumWt, 1);
                }

                return new RebarQuantityResult
                {
                    Diameters = diameters,
                    Rows = rows,
                    TotalLengthPerDia = totalLenPerDia,
                    TotalWeightPerDia = totalWtPerDia,
                    GrandTotalWeightKg = Math.Round(rows.Sum(r => r.RowTotalWeightKg), 1)
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RebarHostMarkService.Scan Exception: " + ex.Message);
                return null;
            }
        }

        private static string GetHostMark(Document doc, Element rebarElem)
        {
            try
            {
                ElementId hostId = RebarHelper.GetHostIdSafe(rebarElem);
                if (hostId == null || hostId == ElementId.InvalidElementId) return "Unknown Host";

                Element host = doc.GetElement(hostId);
                if (host == null || !host.IsValidObject) return "Deleted/Missing Host";

                Parameter markParam = host.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                if (markParam != null && markParam.HasValue)
                {
                    string mark = markParam.AsString();
                    if (!string.IsNullOrWhiteSpace(mark))
                    {
                        return mark;
                    }
                }
                
                return "<Unmarked>";
            }
            catch
            {
                return "Unknown Host";
            }
        }

        private static int GetBarDiameterMm(Element rebarElem)
        {
            try
            {
                if (rebarElem is Autodesk.Revit.DB.Structure.Rebar rebar)
                {
                    var barType = rebar.Document.GetElement(rebar.GetTypeId()) as RebarBarType;
                    if (barType != null)
                    {
                        double diaFeet = barType.BarNominalDiameter;
                        double diaMm = diaFeet * 304.8;
                        return RoundToStandardDia(diaMm);
                    }
                }
                else if (rebarElem is RebarInSystem ris)
                {
                    var barType = ris.Document.GetElement(ris.GetTypeId()) as RebarBarType;
                    if (barType != null)
                    {
                        double diaFeet = barType.BarNominalDiameter;
                        double diaMm = diaFeet * 304.8;
                        return RoundToStandardDia(diaMm);
                    }
                }
            }
            catch { }

            try
            {
                var param = rebarElem.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER);
                if (param != null)
                {
                    double diaFeet = param.AsDouble();
                    double diaMm = diaFeet * 304.8;
                    return RoundToStandardDia(diaMm);
                }
            }
            catch { }

            return 0;
        }

        private static int RoundToStandardDia(double rawMm)
        {
            int[] standards = { 6, 8, 10, 12, 16, 20, 25, 28, 32, 36, 40 };
            int closest = standards[0];
            double minDiff = Math.Abs(rawMm - closest);

            foreach (int s in standards)
            {
                double diff = Math.Abs(rawMm - s);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = s;
                }
            }
            return closest;
        }

        private static double GetTotalLengthMeters(Element rebarElem)
        {
            try
            {
                var param = rebarElem.get_Parameter(BuiltInParameter.REBAR_ELEM_TOTAL_LENGTH);
                if (param != null)
                {
                    double lengthFeet = param.AsDouble();
                    return lengthFeet * 0.3048;
                }
            }
            catch { }

            return 0;
        }

        private static double GetUnitWeight(int diaMm)
        {
            if (UnitWeightKgPerM.TryGetValue(diaMm, out double wt))
                return wt;
            return Math.PI / 4.0 * diaMm * diaMm * 7850.0 / 1e6;
        }
    }
}
