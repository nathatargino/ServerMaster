import json
import re

with open('src/ServerMaster.Core/Engines/HytaleServer.cs', 'r', encoding='utf-8') as f:
    text = f.read()

replacement = '''
            var packageJsonPath = Path.Combine(_profile.ServerDirectory, "package.json");
            var serverJsPath = Path.Combine(_profile.ServerDirectory, "server.js");
            
            // Força a atualização deletando o server.js antigo (se existir)
            if (File.Exists(serverJsPath)) File.Delete(serverJsPath);

            if (!File.Exists(serverJsPath))
            {
                progress?.Report("Gerando arquitetura Node.js do Hytale Mock (UDP)...");
                Emit(LogLevel.Information, "[ServerMaster] Construindo ambiente de Rede UDP (Hytale Node Server)...");

                _isMockMode = true; // Still marked as mock since Hytale isn't live

                var serverJsStr = @"const dgram = require('dgram');
const server = dgram.createSocket('udp4');

const port = process.env.PORT || 5520;
let onlinePlayers = 0;

server.on('error', (err) => {
  console.log([Hytale] Server error:\n);
  server.close();
});

server.on('message', (msg, rinfo) => {
  console.log([Hytale] Recebido pacote UDP de : (Size:  bytes));
  
  if (msg.length > 0) {
      // Fake handshake acceptance pinging back
      const response = Buffer.from('HYTALE_MOCK_ACCEPTED');
      server.send(response, 0, response.length, rinfo.port, rinfo.address);
  }
});

server.on('listening', () => {
    console.log([Hytale] Loading mock world data (Seed: -2294191));
    setTimeout(() => {
        console.log([Hytale] Starting network listener on 0.0.0.0:...);
        setTimeout(() => {
            console.log([Hytale] Server marked as ONLINE. Conecte no IP local.);
        }, 500);
    }, 1000);
});

setInterval(() => {
    console.log('[Hytale] Keep-alive packet broadcasted.');
}, 6000);

server.bind(port);";
                
                await File.WriteAllTextAsync(serverJsPath, serverJsStr, ct);
            }
'''

pattern = r'var packageJsonPath = Path\.Combine\(_profile\.ServerDirectory, "package\.json"\);\s*var serverJsPath = Path\.Combine\(_profile\.ServerDirectory, "server\.js"\).*?catch \(System\.ComponentModel\.Win32Exception\)\s*\{\s*Emit\(LogLevel\.Warning, "\[ServerMaster\] Node\.js ou NPM não encontrados no PATH\. O servidor pode falhar ao iniciar\."\);\s*\}\s*\}'

new_text = re.sub(pattern, replacement.strip(), text, flags=re.DOTALL)

with open('src/ServerMaster.Core/Engines/HytaleServer.cs', 'w', encoding='utf-8') as f:
    f.write(new_text)

print("Patch applied successfully" if text != new_text else "Patch failed")
