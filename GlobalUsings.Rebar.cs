// ──────────────────────────────────────────────────────────────
//  Revit 2027 API Compatibility: RebarHookOrientation → RebarTerminationOrientation
//
//  Revit 2027 removed the RebarHookOrientation enum entirely
//  and replaced it with RebarTerminationOrientation (same values: Left=1, Right=-1).
//  This global alias lets all existing code continue to compile for R27
//  without touching every single file that references the old type.
// ──────────────────────────────────────────────────────────────
#if REVIT2027_OR_GREATER
global using RebarHookOrientation = Autodesk.Revit.DB.Structure.RebarTerminationOrientation;
#endif
