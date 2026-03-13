using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.DTO;
using antiGGGravity.StructuralRebar.Constants;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Layout
{
    public static class FootingPadLayoutGenerator
    {
        public static List<RebarDefinition> CreateMat(
            HostGeometry host,
            RebarLayerConfig layer,
            bool isTop)
        {
            var definitions = new List<RebarDefinition>();
            
            XYZ basisL = host.LAxis;
            XYZ basisW = host.WAxis;
            XYZ basisH = host.HAxis;
            
            double length = host.Length;
            double width = host.Width;
            double height = host.Height;
            double cTop = host.CoverTop;
            double cBot = host.CoverBottom;
            double cSide = host.CoverExterior;

            double dia = layer.BarDiameter_Backing;
            double spacing = layer.VerticalSpacing; // Using VerticalSpacing for grid spacing
            if (dia <= 0 || spacing <= 0) return definitions;

            // Offset to prevent edge clashes (standard practice)
            double edgeOff = cSide + (2.5 * dia);

            // Z levels for the mats (B1/B2 or T1/T2)
            double z1, z2;
            if (isTop)
            {
                z1 = (height / 2.0) - cTop - dia / 2.0;
                z2 = z1 - dia;
            }
            else
            {
                z1 = -(height / 2.0) + cBot + dia / 2.0;
                z2 = z1 + dia;
            }

            // Determine orientation. Short edge gets the outer bar B1/T1.
            bool shortIsL = length <= width;

            // Layer 1 (Outer) - Along the short dimension
            XYZ p1_1, p2_1, distDir1;
            double distLen1;

            if (shortIsL)
            {
                // Along L, distributed along W
                p1_1 = host.StartPoint + basisH * z1 + basisW * (-width / 2.0 + cSide);
                p2_1 = host.EndPoint + basisH * z1 + basisW * (-width / 2.0 + cSide);
                distDir1 = basisW;
                distLen1 = width - 2 * edgeOff; // Original used a larger offset for distribution start
            }
            else
            {
                // Along W, distributed along L
                p1_1 = host.Origin - basisW * (width / 2.0 - cSide) + basisH * z1 - basisL * (length / 2.0 - edgeOff);
                p2_1 = host.Origin + basisW * (width / 2.0 - cSide) + basisH * z1 - basisL * (length / 2.0 - edgeOff);
                distDir1 = basisL;
                distLen1 = length - 2 * edgeOff;
            }

            // Re-align points for layer 1 distribution
            // The original logic was: p1/p2 define the curves, distDir/distLen define the array.
            // Let's refine based on the original command's snippet for Layer 1.
            
            if (shortIsL)
            {
                // B1: Along X (short), distributed along Y
                // p1 = minX + side, minY + off; p2 = maxX - side, minY + off; dist = BasisY, len = sizeY - 2*off
                p1_1 = host.Origin - basisL * (length / 2.0 - cSide) - basisW * (width / 2.0 - edgeOff) + basisH * z1;
                p2_1 = host.Origin + basisL * (length / 2.0 - cSide) - basisW * (width / 2.0 - edgeOff) + basisH * z1;
                distDir1 = basisW;
                distLen1 = width - 2 * edgeOff;
            }
            else
            {
                // B1: Along Y (short), distributed along X
                // p1 = maxX - off, minY + side; p2 = maxX - off, maxY - side; dist = -BasisX, len = sizeX - 2*off
                p1_1 = host.Origin + basisL * (length / 2.0 - edgeOff) - basisW * (width / 2.0 - cSide) + basisH * z1;
                p2_1 = host.Origin + basisL * (length / 2.0 - edgeOff) + basisW * (width / 2.0 - cSide) + basisH * z1;
                distDir1 = -basisL;
                distLen1 = length - 2 * edgeOff;
            }

            XYZ legDir = isTop ? -basisH : basisH;
            double legLen = layer.OverrideHookLength ? layer.HookLengthOverride : (height - cTop - cBot - dia);
            if (legLen < 0.2) legLen = 0.5;

            if (!isTop)
            {
                XYZ temp1 = p1_1; p1_1 = p2_1; p2_1 = temp1;
            }

            XYZ pStartLeg1 = p1_1 + legDir * legLen;
            XYZ pEndLeg1 = p2_1 + legDir * legLen;
            List<Curve> curves1 = new List<Curve>
            {
                Line.CreateBound(pStartLeg1, p1_1),
                Line.CreateBound(p1_1, p2_1),
                Line.CreateBound(p2_1, pEndLeg1)
            };

            definitions.Add(new RebarDefinition
            {
                Curves = curves1,
                Style = RebarStyle.Standard,
                BarTypeName = layer.VerticalBarTypeName,
                BarDiameter = dia,
                Spacing = spacing,
                ArrayLength = distLen1,
                Normal = distDir1,
                HookStartName = layer.HookStartName,
                HookEndName = layer.HookEndName,
                HookStartOrientation = RebarHookOrientation.Right, // Re-verify: Inner hook bend
                HookEndOrientation = RebarHookOrientation.Right,   // Re-verify: Inner hook bend
                OverrideHookLength = layer.OverrideHookLength,
                HookLengthOverride = layer.HookLengthOverride,
                Label = "Footing Pad Mat 1",
                Comment = isTop ? "Top Bar" : "Btm Bar"
            });

            // Layer 2 (Inner) - Along the long dimension
            XYZ p1_2, p2_2, distDir2;
            double distLen2;

            if (shortIsL)
            {
                // B2: Along Y (long), distributed along X
                p1_2 = host.Origin + basisL * (length / 2.0 - edgeOff) - basisW * (width / 2.0 - cSide) + basisH * z2;
                p2_2 = host.Origin + basisL * (length / 2.0 - edgeOff) + basisW * (width / 2.0 - cSide) + basisH * z2;
                distDir2 = -basisL;
                distLen2 = length - 2 * edgeOff;
            }
            else
            {
                // B2: Along X (long), distributed along Y
                p1_2 = host.Origin - basisL * (length / 2.0 - cSide) - basisW * (width / 2.0 - edgeOff) + basisH * z2;
                p2_2 = host.Origin + basisL * (length / 2.0 - cSide) - basisW * (width / 2.0 - edgeOff) + basisH * z2;
                distDir2 = basisW;
                distLen2 = width - 2 * edgeOff;
            }

            if (!isTop)
            {
                XYZ temp2 = p1_2; p1_2 = p2_2; p2_2 = temp2;
            }

            XYZ pStartLeg2 = p1_2 + legDir * legLen;
            XYZ pEndLeg2 = p2_2 + legDir * legLen;
            List<Curve> curves2 = new List<Curve>
            {
                Line.CreateBound(pStartLeg2, p1_2),
                Line.CreateBound(p1_2, p2_2),
                Line.CreateBound(p2_2, pEndLeg2)
            };

            definitions.Add(new RebarDefinition
            {
                Curves = curves2,
                Style = RebarStyle.Standard,
                BarTypeName = layer.VerticalBarTypeName,
                BarDiameter = dia,
                Spacing = spacing,
                ArrayLength = distLen2,
                Normal = distDir2,
                HookStartName = layer.HookStartName,
                HookEndName = layer.HookEndName,
                HookStartOrientation = RebarHookOrientation.Right,
                HookEndOrientation = RebarHookOrientation.Right,
                OverrideHookLength = layer.OverrideHookLength,
                HookLengthOverride = layer.HookLengthOverride,
                Label = "Footing Pad Mat 2",
                Comment = isTop ? "Top Bar" : "Btm Bar"
            });

            return definitions;
        }

        public static List<RebarDefinition> CreateSideRebars(
            HostGeometry host, string barTypeName, double barDia, double spacing,
            bool overrideLeg, double legLenOverride)
        {
            var defs = new List<RebarDefinition>();
            if (barDia <= 0 || spacing <= 0 || string.IsNullOrEmpty(barTypeName)) return defs;

            double cTop = host.CoverTop;
            double cBot = host.CoverBottom;
            double cOther = host.CoverExterior; 
            double height = host.Height;

            double assumedMainDia = UnitConversion.MmToFeet(25);
            // Height available for side bars
            double sideZTop = (height / 2.0) - cTop - assumedMainDia * 2;
            double sideZBot = -(height / 2.0) + cBot + assumedMainDia * 2;
            double availableHeight = sideZTop - sideZBot;

            if (availableHeight <= 0) return defs;

            // Maximum Spacing Rule: Calculate number of spaces needed so each space <= spacing
            int numSpaces = (int)Math.Ceiling(availableHeight / spacing);
            int rowCount = numSpaces - 1;
            
            if (rowCount < 0) rowCount = 0;

            XYZ L = host.LAxis;
            XYZ W = host.WAxis;
            XYZ H = host.HAxis;
            XYZ orig = host.Origin;

            // Strategy: 2 faces (L- and L+)
            // Each bar is Shape LL (U-Shape) with legs wrapping into Sides 3 and 4 (along Length).
            
            // Default leg length: Half of host length minus cover
            double defaultLegLen = (host.Length / 2.0) - cOther;
            double legLen = overrideLeg ? legLenOverride : defaultLegLen;
            if (legLen < 0.2) legLen = 0.5;

            // The main segment is across Width
            double mainLen = host.Width - 2 * (cOther + barDia / 2.0);

            for (int i = 0; i < 2; i++)
            {
                // Face 1: L- , Face 2: L+
                XYZ facePos = orig + L * (i == 0 ? -(host.Length/2.0 - cOther) : (host.Length/2.0 - cOther));
                double sign = (i == 0) ? 1.0 : -1.0; // Inward direction for legs relative to face normal

                for (int row = 1; row <= rowCount; row++)
                {
                    double z = sideZBot + (availableHeight / numSpaces) * row;
                    XYZ zOff = H * z;

                    XYZ pMainStart = facePos - W * (mainLen / 2.0) + zOff;
                    XYZ pMainEnd = facePos + W * (mainLen / 2.0) + zOff;
                    XYZ pLeg1Start = pMainStart + L * (sign * legLen);
                    XYZ pLeg2End = pMainEnd + L * (sign * legLen);

                    var curves = new List<Curve>
                    {
                        Line.CreateBound(pLeg1Start, pMainStart),
                        Line.CreateBound(pMainStart, pMainEnd),
                        Line.CreateBound(pMainEnd, pLeg2End)
                    };

                    defs.Add(new RebarDefinition
                    {
                        Curves = curves,
                        Style = RebarStyle.Standard,
                        BarTypeName = barTypeName,
                        BarDiameter = barDia,
                        FixedCount = 1,
                        DistributionWidth = 0,
                        ArrayDirection = H,
                        Normal = H, // Planes are horizontal
                        ShapeNameHint = "Shape LL",
                        Label = "Footing Pad Side Bar",
                        Comment = "Side Bar"
                    });
                }
            }

            return defs;
        }
    }
}
