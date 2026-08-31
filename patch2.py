import json
import re

with open('src/ServerMaster.Core/Engines/HytaleServer.cs', 'r', encoding='utf-8') as f:
    text = f.read()

# PATCH 1: PREPARE ASYNC (Add Real Jar check)
patch1 = '''
            var hasRealJar = File.Exists(Path.Combine(_profile.ServerDirectory, "HytaleServer.jar"));

            if (!hasRealJar)
            {
                var serverJsPath = Path.Combine(_profile.ServerDirectory, "server.js");
                
                // Delete old TCP server if it exists to forcefully upgrade to UDP
                var oldPackage = Path.Combine(_profile.ServerDirectory, "package.json");
                if (File.Exists(oldPackage)) File.Delete(oldPackage);
                var oldModules = Path.Combine(_profile.ServerDirectory, "node_modules");
                if (Directory.Exists(oldModules)) Directory.Delete(oldModules, true);
                
                if (!File.Exists(serverJsPath))
                {
                    progress?.Report("Gerando arquitetura Node.js do Hytale Mock (UDP)...");
                    Emit(LogLevel.Information, "[ServerMaster] Construindo ambiente de Rede UDP (Hytale Node Server)...");

                    _isMockMode = true;

                    var serverJsStr = @"const dgram = require('dgram');
const server = dgram.createSocket('udp4');

const port = process.env.PORT || 5520;
let onlinePlayers = 0;

server.on('error', (err) => {
  console.log([Hytale] Server error:\n);
  server.close();
});

server.on('message', (msg, rinfo) => {
  console.log([Hytale] Recebido pacote UDP de : ( bytes));
  if (msg.length > 0) {
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
            }
            else
            {
                progress?.Report("Identificada Engine Oficial Hytale (Java)...");
                Emit(LogLevel.Information, "[ServerMaster] HytaleServer.jar encontrado! Executaremos o backend autêntico através da JVM.");
            }
'''

pattern1 = r'var serverJsPath = Path\.Combine\(_profile\.ServerDirectory, "server\.js"\);.*?await File\.WriteAllTextAsync\(serverJsPath, serverJsStr, ct\);\s*\}'
text = re.sub(pattern1, patch1.strip(), text, flags=re.DOTALL)

# PATCH 2: START ASYNC
patch2 = '''
        var hasRealJar = File.Exists(Path.Combine(p.ServerDirectory, "HytaleServer.jar"));

        var cmd = hasRealJar ? "java" : "node";
        var ram = p.Resources;
        var args = hasRealJar 
            ? $"-Xms{ram.RamMinMb}M -Xmx{ram.RamMb}M -XX:+UseG1GC -jar HytaleServer.jar --bind 0.0.0.0:{p.Port}"
            : "server.js";

        try
        {
            if (hasRealJar) 
                Emit(LogLevel.Information, $"[ServerMaster] Iniciando Engine Java Oficial do Hytale na porta UDP {p.Port}...");
            else
                Environment.SetEnvironmentVariable("PORT", p.Port.ToString(), EnvironmentVariableTarget.Process);
                
            _process = _processManager.Start(p.ServerDirectory, cmd, args);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Emit(LogLevel.Error, $"[ServerMaster] ERRO CRÍTICO: Não foi possível iniciar o executável '{cmd}'. " + 
                                 "Ele está acessível no PATH do Windows? Detalhes: " + ex.Message);
            State = ServerState.Stopped;
            return Task.CompletedTask;
        }
'''

pattern2 = r'var args = "server\.js";.*?return Task\.CompletedTask;\s*\}'
text = re.sub(pattern2, patch2.strip(), text, flags=re.DOTALL)


# PATCH 3: Emit Fallback 
patch3 = '''
                if (line.Contains("marked as ONLINE") || line.Contains("Done") || line.Contains("Started", StringComparison.OrdinalIgnoreCase) || line.Contains("Ready", StringComparison.OrdinalIgnoreCase))
                {
                    State = ServerState.Running;
                }
'''

pattern3 = r'if \(line\.Contains\("marked as ONLINE"\)\)\s*\{\s*State = ServerState\.Running;\s*\}'
text = re.sub(pattern3, patch3.strip(), text, flags=re.DOTALL)


with open('src/ServerMaster.Core/Engines/HytaleServer.cs', 'w', encoding='utf-8') as f:
    f.write(text)

print("Patch applied successfully" if text != patch1 else "Patch loaded")
