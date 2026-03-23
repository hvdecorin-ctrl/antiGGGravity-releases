using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using antiGGGravity.Utilities;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Global Compatibility Layer to handle Revit API changes across 2022-2026.
    /// This allows most command code to remain unchanged.
    /// </summary>
    public static class RevitCompatibility
    {
        /// <summary>
        /// Safely gets the numerical value of an ElementId regardless of Revit version.
        /// </summary>
        public static long GetIdValue(this ElementId id)
        {
            if (id == null) return -1;
#if REVIT2024_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        /// <summary>
        /// Safely creates a new ElementId from a long value.
        /// </summary>
        public static ElementId NewElementId(long idValue)
        {
#if REVIT2024_OR_GREATER
            return new ElementId(idValue);
#else
            return new ElementId((int)idValue);
#endif
        }

        public static ElementId NewElementId(BuiltInCategory bic)
        {
#if REVIT2024_OR_GREATER
            return new ElementId(bic);
#else
            return new ElementId((int)bic);
#endif
        }

        /// <summary>
        /// Converts Millimeters to internal Feet.
        /// Uses hardcoded constant for maximum compatibility across all Revit versions.
        /// </summary>
        public static double MmToInternal(double mm)
        {
            return mm / 304.8;
        }

        /// <summary>
        /// Converts internal Feet to Millimeters.
        /// </summary>
        public static double InternalToMm(double feet)
        {
            return feet * 304.8;
        }

        /// <summary>
        /// Centralized Units Handling (ForgeTypeId vs DisplayUnitType)
        /// </summary>
        public static double ConvertFromInternal(double value, Parameter p)
        {
#if REVIT2022_OR_GREATER
            return UnitUtils.ConvertFromInternalUnits(value, p.GetUnitTypeId());
#else
            return UnitUtils.ConvertFromInternalUnits(value, p.DisplayUnitType);
#endif
        }

        /// <summary>
        /// Safely creates Rebar from curves, handling the 2026 BarTerminationsData change.
        /// </summary>
        public static Rebar CreateRebar(Document doc, RebarStyle style, RebarBarType barType,
                                        RebarHookType startHook, RebarHookType endHook,
                                        Element host, XYZ normal, IList<Curve> curves,
                                        RebarHookOrientation startOrient, RebarHookOrientation endOrient,
                                        bool useExisting = true, bool createNew = true)
        {
#if REVIT2026_OR_GREATER
            BarTerminationsData termData = new BarTerminationsData(doc);
            if (startHook != null) termData.HookTypeIdAtStart = startHook.Id;
            if (endHook != null) termData.HookTypeIdAtEnd = endHook.Id;
            
#pragma warning disable 0618
            termData.TerminationOrientationAtStart = (RebarTerminationOrientation)(int)startOrient;
            termData.TerminationOrientationAtEnd = (RebarTerminationOrientation)(int)endOrient;

            return Rebar.CreateFromCurves(doc, style, barType, host, normal, curves, termData, useExisting, createNew);
#pragma warning restore 0618
#else
            return Rebar.CreateFromCurves(doc, style, barType, startHook, endHook, host, normal, curves, startOrient, endOrient, useExisting, createNew);
#endif
        }

        /// <summary>
        /// Safely creates Rebar from curves and shape, handling the 2026 BarTerminationsData change.
        /// </summary>
        public static Rebar CreateRebarWithShape(Document doc, RebarShape shape, RebarBarType barType,
                                                 RebarHookType startHook, RebarHookType endHook,
                                                 Element host, XYZ normal, IList<Curve> curves,
                                                 RebarHookOrientation startOrient, RebarHookOrientation endOrient)
        {
#if REVIT2026_OR_GREATER
            BarTerminationsData termData = new BarTerminationsData(doc);
            if (startHook != null) termData.HookTypeIdAtStart = startHook.Id;
            if (endHook != null) termData.HookTypeIdAtEnd = endHook.Id;
            
#pragma warning disable 0618
            termData.TerminationOrientationAtStart = (RebarTerminationOrientation)(int)startOrient;
            termData.TerminationOrientationAtEnd = (RebarTerminationOrientation)(int)endOrient;

            return Rebar.CreateFromCurvesAndShape(doc, shape, barType, host, normal, curves, termData);
#pragma warning restore 0618
#else
            return Rebar.CreateFromCurvesAndShape(doc, shape, barType, startHook, endHook, host, normal, curves, startOrient, endOrient);
#endif
        }

        /// <summary>
        /// Safely sets rebar hook orientation, handling the 2026 deprecation.
        /// </summary>
        public static void SetHookOrientationCompatible(Rebar rebar, int end, RebarHookOrientation orient)
        {
#pragma warning disable 0618
            rebar.SetHookOrientation(end, orient);
#pragma warning restore 0618
        }

        /// <summary>
        /// Safely gets the numerical value of a Parameter's ID.
        /// </summary>
        public static long GetParamIdValue(Parameter p)
        {
#if REVIT2024_OR_GREATER
            return p.Id.Value;
#else
            return p.Id.IntegerValue;
#endif
        }
    }
}
