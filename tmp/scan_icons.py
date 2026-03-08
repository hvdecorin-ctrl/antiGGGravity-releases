import os
import re

ribbon_path = r'c:\Users\DELL\source\repos\antiGGGravity\Resources\ribbon.yaml'
icons_dir = r'c:\Users\DELL\source\repos\antiGGGravity\Resources\Icons'

with open(ribbon_path, 'r') as f:
    content = f.read()

# Extract icon names using regex
icon_names_in_yaml = set(re.findall(r'icon:\s*(\w+)', content))

existing_icons = os.listdir(icons_dir)

missing_icons = []
for name in icon_names_in_yaml:
    expected_filename = f"{name}(32x32).png"
    if expected_filename not in existing_icons:
        # Check if it exists without the (32x32) suffix just in case
        if f"{name}.png" in existing_icons:
            print(f"Warning: Icon '{name}' exists as '{name}.png' but expected '{expected_filename}'")
        else:
            missing_icons.append(name)

unused_icons = []
for filename in existing_icons:
    if not filename.endswith('.png'):
        continue
    
    basename = os.path.splitext(filename)[0]
    if basename.endswith('(32x32)'):
        pure_name = basename[:-7]
    else:
        pure_name = basename
    
    if pure_name not in icon_names_in_yaml:
        unused_icons.append(filename)

print("--- MISSING ICONS ---")
if not missing_icons:
    print("None")
else:
    for m in sorted(missing_icons):
        print(f"- {m} (Expected: {m}(32x32).png)")

print("\n--- UNUSED ICONS ---")
if not unused_icons:
    print("None")
else:
    for u in sorted(unused_icons):
        print(f"- {u}")
