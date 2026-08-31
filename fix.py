import re

with open('src/ServerMaster.Core/Engines/HytaleServer.cs', 'r', encoding='utf-8') as f:
    text = f.read()

text = re.sub(r'HytalePath.*', r'', text)

text = text.replace(r'@"Hytale\install' + '\n' + '  elease\package\game\latest"', r'@"Hytale\install\release\package\game\latest"')
text = text.replace(r'@"Hytale\install' + '\r\n' + '  elease\package\game\latest"', r'@"Hytale\install\release\package\game\latest"')

with open('src/ServerMaster.Core/Engines/HytaleServer.cs', 'w', encoding='utf-8') as f:
    f.write(text)
