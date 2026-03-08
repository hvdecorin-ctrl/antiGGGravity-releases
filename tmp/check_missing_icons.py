import yaml

ribbon_path = r'c:\Users\DELL\source\repos\antiGGGravity\Resources\ribbon.yaml'

with open(ribbon_path, 'r') as f:
    data = yaml.safe_load(f)

missing_explicit_icons = []

def check_items(items):
    for item in items:
        if 'command' in item and 'icon' not in item:
            missing_explicit_icons.append(item.get('name', item.get('command')))
        
        if 'buttons' in item:
            check_items(item['buttons'])
        
        if 'items' in item:
            check_items(item['items'])

check_items(data['panels'])

if not missing_explicit_icons:
    print("All commands have explicit icons.")
else:
    print("Commands missing explicit 'icon:' key:")
    for name in missing_explicit_icons:
        print(f"- {name}")
