import yaml

ribbon_path = r'c:\Users\DELL\source\repos\antiGGGravity\Resources\ribbon.yaml'

with open(ribbon_path, 'r') as f:
    data = yaml.safe_load(f)

icon_to_commands = {}

def track_icons(items):
    for item in items:
        if 'command' in item and 'icon' in item:
            cmd = item['command']
            icon = item['icon']
            if icon not in icon_to_commands:
                icon_to_commands[icon] = []
            icon_to_commands[icon].append(cmd)
        
        if 'buttons' in item:
            track_icons(item['buttons'])
        if 'items' in item:
            track_icons(item['items'])

track_icons(data['panels'])

for icon, cmds in icon_to_commands.items():
    if len(set(cmds)) > 1:
        print(f"Shared Icon '{icon}': Used by {', '.join(set(cmds))}")
