---
description: [Standard UI Design for antiGGGravity - enforce Title Case and content safety]
---
// turbo-all
# UI Design Standard Workflow

All UI work for antiGGGravity must strictly follow these branding and usability rules.

## 1. Typography & Casing
- **Rule: Title Case is Mandatory.** 
  - All headers, section labels, checkboxes, and radio buttons must use **Title Case**.
  - **Forbidden**: ALL CAPS (e.g., "SELECT VIEWS") or all lowercase (e.g., "apply results").
  - **Correct**: "Select Views", "Apply Results", "Application Scope".

## 2. Window Chrome & Safety
- **Resizability**: Use the standardized window chrome:
  ```xaml
  <WindowChrome.WindowChrome>
      <StaticResource ResourceKey="PremiumWindowChrome"/>
  </WindowChrome.WindowChrome>
  ```
- **Minimum Sizing**: Every modeless window MUST define `MinWidth` and `MinHeight` in the `<Window>` tag to prevent content from being cut off.
- **Corner Safety**: Maintain at least **15px margin** for interactive elements near the bottom-right corner to allow the OS to capture the resize point.

## 3. Interaction Mechanics
- **Master Toggles**: When providing a "Toggle All" feature for a list, place the checkbox in its own row directly **above** the list. The checkbox and its label must be perfectly **aligned** with the items in the list below. Use "**Select**" as the text label.
- **Action Buttons**: Place primary action buttons (e.g., "Apply") in the bottom-right of the footer using `PremiumActionButtonStyle`.

## 4. Verification Step
- Run a build to ensure no `StaticResource` errors occur with the brand style dictionary.
- Verify the final layout in Revit to ensure "Title Case" is applied consistently.
