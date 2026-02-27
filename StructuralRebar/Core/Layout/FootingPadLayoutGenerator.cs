using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.DTO;
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

            definitions.Add(new RebarDefinition
            {
                Curves = new List<Curve> { Line.CreateBound(p1_1, p2_1) },
                Style = RebarStyle.Standard,
                BarTypeName = layer.VerticalBarTypeName,
                BarDiameter = dia,
                Spacing = spacing,
                ArrayLength = distLen1,
                Normal = distDir1,
                HookStartName = layer.HookStartName,
                HookEndName = layer.HookEndName,
                HookStartOrientation = isTop ? RebarHookOrientation.Left : RebarHookOrientation.Right,
                HookEndOrientation = isTop ? RebarHookOrientation.Left : RebarHookOrientation.Right,
                OverrideHookLength = layer.OverrideHookLength,
                HookLengthOverride = layer.HookLengthOverride,
                Label = "Footing Pad Mat 1"
            });

            // Layer 2 (Inner) - Along the long dimension
            XYZ p1_2, p2_2, distDir2;
            double distLen2;

            if (shortIsL)
            {
                // B2: Along Y (long), distributed along X
                p1_1 = host.Origin + basisL * (length / 2.0 - edgeOff) - basisW * (width / 2.0 - cSide) + basisH * z2;
                p2_1 = host.Origin + basisL * (length / 2.0 - edgeOff) + basisW * (width / 2.0 - cSide) + basisH * z2;
                distDir1 = -basisL;
                distLen1 = length - 2 * edgeOff;
            }
            else
            {
                // B2: Along X (long), distributed along Y
                p1_1 = host.Origin - basisL * (length / 2.0 - cSide) - basisW * (width / 2.0 - edgeOff) + basisH * z2;
                p2_1 = host.Origin + basisL * (length / 2.0 - cSide) - basisW * (width / 2.0 - edgeOff) + basisH * z2;
                distDir1 = basisW;
                distLen1 = width - 2 * edgeOff;
            }

            definitions.Add(new RebarDefinition
            {
                Curves = new List<Curve> { Line.CreateBound(p1_1, p2_1) },
                Style = RebarStyle.Standard,
                BarTypeName = layer.VerticalBarTypeName,
                BarDiameter = dia,
                Spacing = spacing,
                ArrayLength = distLen1,
                Normal = distDir1,
                HookStartName = layer.HookStartName,
                HookEndName = layer.HookEndName,
                HookStartOrientation = isTop ? RebarHookOrientation.Left : RebarHookOrientation.Right,
                HookEndOrientation = isTop ? RebarHookOrientation.Left : RebarHookOrientation.Right,
                OverrideHookLength = layer.OverrideHookLength,
                HookLengthOverride = layer.HookLengthOverride,
                Label = "Footing Pad Mat 2"
            });

            return definitions;
        }
    }
}
