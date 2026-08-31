import re
with open('src/ServerMaster.Core/Engines/HytaleServer.cs', 'r', encoding='utf-8') as f:
    text = f.read()

text = re.sub(r'install\s*elease', r'install\\release', text)

with open('src/ServerMaster.Core/Engines/HytaleServer.cs', 'w', encoding='utf-8') as f:
    f.write(text)
