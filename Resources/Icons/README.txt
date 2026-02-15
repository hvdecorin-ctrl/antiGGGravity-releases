RIBBON ICON STANDARD

All icons use a single 32x32 PNG file. The code handles scaling automatically:

  - LARGE ICONS (Ribbon Buttons): 32x32 displayed as-is via LargeImage property.
  - SMALL ICONS (Quick Access Toolbar): 32x32 scaled down to 16x16 via Image property.

FILE NAMING:
  - Format: "IconName(32x32).png"
  - Example: "Selection(32x32).png", "Visibility(32x32).png"

HOW TO ADD/UPDATE ICONS:
  1. Create a 32x32 pixel PNG icon.
  2. Name it "YourToolName(32x32).png".
  3. Place it in this folder (Resources\Icons) or a subfolder matching the panel name.
  4. Rebuild the project (dotnet build --configuration Release).
  5. The icon is automatically embedded into the DLL and scaled for QAT.
