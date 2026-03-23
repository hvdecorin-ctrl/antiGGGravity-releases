using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using antiGGGravity.Utilities;

namespace antiGGGravity.Commands.Rebar
{
    /// <summary>
    /// Data model for a single cell in the quantity matrix.
    /// </summary>
    public class RebarCellData
    {
        public double TotalLengthM { get; set; }
        public double TotalWeightKg { get; set; }
    }

    /// <summary>
    /// One row in the quantity matrix (e.g. Foundation, Wall, etc.).
    /// </summary>
    public class RebarHostRow
    {
        public string HostCategory { get; set; }
        public Dictionary<int, RebarCellData> DiameterData { get; set; } = new Dictionary<int, RebarCellData>();
        
        public double RowTotalWeightKg => DiameterData.Values.Sum(c => c.TotalWeightKg);
    }

    /// <summary>
    /// Complete result of the rebar quantity scan.
    /// </summary>
    public class RebarQuantityResult
    {
        public List<int> Diameters { get; set; } = new List<int>();
        public List<RebarHostRow> Rows { get; set; } = new List<RebarHostRow>();

        /// <summary>Grand total length per diameter (m).</summary>
        public Dictionary<int, double> TotalLengthPerDia { get; set; } = new Dictionary<int, double>();

        /// <summary>Grand total weight per diameter (kg).</summary>
        public Dictionary<int, double> TotalWeightPerDia { get; set; } = new Dictionary<int, double>();

        /// <summary>Grand total weight across everything (kg).</summary>
        public double GrandTotalWeightKg { get; set; }
    }

    /// <summary>
    /// Scans all rebar in the document and produces a quantity summary
    /// grouped by host category and bar diameter.
    /// </summary>
    public static class RebarQuantityService
    {
        // Unit weight of steel rebar per diameter (kg/m)
        // Calculated as: (π/4) × d² × 7850 / 1e6  where d is in mm
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

        // Host category display order priority
        private static readonly string[] HostOrder = 
            { "Foundation", "Wall", "Column", "Beam", "Floor", "Roof" };

        /// <summary>
        /// Perform the full rebar quantity scan on the given document.
        /// </summary>
        public static RebarQuantityResult Scan(UIApplication app)
        {
            try
            {
                var uiDoc = app.ActiveUIDocument;
                var doc = uiDoc?.Document;
                if (doc == null || !doc.IsValidObject) return null;

                var selectedIds = uiDoc.Selection.GetElementIds();
                IEnumerable<Element> allRebarElements;

                // 1. Collect rebar elements (either selected or all in document)
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

                // 2. Build raw data: (hostCategory, diameterMm, totalLengthM)
                var rawEntries = new List<(string host, int dia, double lengthM)>();

                foreach (var elem in allRebarElements)
                {
                    try
                    {
                        if (elem == null || !elem.IsValidObject) continue;

                        string hostCat = GetHostCategoryName(doc, elem);
                        int diaMm = GetBarDiameterMm(elem);
                        double lengthM = GetTotalLengthMeters(elem);

                        if (diaMm > 0)
                        {
                            rawEntries.Add((hostCat, diaMm, lengthM));
                        }
                    }
                    catch { /* Skip problematic individual bars */ }
                }

                if (rawEntries.Count == 0) return new RebarQuantityResult();

                // 3. Determine unique diameters, sorted ascending
                var diameters = rawEntries.Select(e => e.dia).Distinct().OrderBy(d => d).ToList();

                // 4. Group by host category → build rows
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

                // 5. Order rows: Priorities first, then others alphabetically
                var rows = new List<RebarHostRow>();
                var processed = new HashSet<string>();

                foreach (var cat in HostOrder)
                {
                    if (rowDict.ContainsKey(cat))
                    {
                        rows.Add(rowDict[cat]);
                        processed.Add(cat);
                    }
                }

                // Add any other categories alphabetically
                var others = rowDict.Keys.Where(k => !processed.Contains(k)).OrderBy(k => k).ToList();
                foreach (var otherCat in others)
                {
                    rows.Add(rowDict[otherCat]);
                }

                // 6. Compute totals per diameter
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
                // Safety catch to avoid Revit unrecoverable errors
                System.Diagnostics.Debug.WriteLine("RebarQuantityService.Scan Exception: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Map the host element's built-in category to a display name.
        /// </summary>
        private static string GetHostCategoryName(Document doc, Element rebarElem)
        {
            try
            {
                ElementId hostId = RebarHelper.GetHostIdSafe(rebarElem);
                if (hostId == null || hostId == ElementId.InvalidElementId) return "Unknown Host";

                Element host = doc.GetElement(hostId);
                if (host == null || !host.IsValidObject) return "Deleted/Missing Host";
                if (host.Category == null) return "Unknown Category";

                long catId = host.Category.Id.GetIdValue();

                if (catId == (long)BuiltInCategory.OST_StructuralFoundation) return "Foundation";
                if (catId == (long)BuiltInCategory.OST_Walls) return "Wall";
                if (catId == (long)BuiltInCategory.OST_StructuralColumns || 
                    catId == (long)BuiltInCategory.OST_Columns) return "Column";
                if (catId == (long)BuiltInCategory.OST_StructuralFraming) return "Beam";
                if (catId == (long)BuiltInCategory.OST_Floors) return "Floor";
                if (catId == (long)BuiltInCategory.OST_Roofs) return "Roof";

                // If not a standard structural host, return the actual category name
                try { return host.Category.Name; }
                catch { return "Unknown (" + catId + ")"; }
            }
            catch
            {
                return "Unknown Host";
            }
        }

        /// <summary>
        /// Get the nominal bar diameter in mm (rounded to nearest standard size).
        /// </summary>
        private static int GetBarDiameterMm(Element rebarElem)
        {
            try
            {
                if (rebarElem is Autodesk.Revit.DB.Structure.Rebar rebar)
                {
                    // BarType.BarNominalDiameter is in feet (internal units)
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

            // Fallback: try parameter
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

        /// <summary>
        /// Round a raw diameter in mm to the nearest standard bar size.
        /// </summary>
        private static int RoundToStandardDia(double rawMm)
        {
            int[] standards = { 6, 8, 10, 12, 16, 20, 25, 28, 32, 36, 40 };
            int best = (int)Math.Round(rawMm);
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

        /// <summary>
        /// Get the total bar length in meters.
        /// </summary>
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

        /// <summary>
        /// Get the unit weight (kg/m) for a given diameter. Falls back to calculated value.
        /// </summary>
        private static double GetUnitWeight(int diaMm)
        {
            if (UnitWeightKgPerM.TryGetValue(diaMm, out double wt))
                return wt;

            // Calculate: (π/4) × d² × 7850 / 1e6
            return Math.PI / 4.0 * diaMm * diaMm * 7850.0 / 1e6;
        }
    }
}
