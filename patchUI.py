# PATCH 1: MainWindow.axaml
with open('src/ServerMaster.App/MainWindow.axaml', 'r', encoding='utf-8') as f:
    text = f.read()

replacement = '''
    <!-- Main content — routes by ViewModel type -->
    <ContentControl Grid.Row="1" Content="{Binding CurrentPage}"/>
    
    <TextBlock Grid.Row="1" Text="ServerMaster v1.0.0" Foreground="#2A2A2A" FontSize="11" 
               VerticalAlignment="Bottom" HorizontalAlignment="Right" Margin="12" IsHitTestVisible="False"/>
'''

pattern = '<!-- Main content'
start_idx = text.find(pattern)
end_idx = text.find('/>', start_idx) + 2
text = text[:start_idx] + replacement.strip() + text[end_idx:]

with open('src/ServerMaster.App/MainWindow.axaml', 'w', encoding='utf-8') as f:
    f.write(text)


# PATCH 2: HytaleServer.cs
with open('src/ServerMaster.Core/Engines/HytaleServer.cs', 'r', encoding='utf-8') as f:
    coreText = f.read()

pStartFind = '''var hasRealJar = File.Exists(Path.Combine(_profile.ServerDirectory, "HytaleServer.jar"));

            if (!hasRealJar)
            {
                var serverJsPath = Path.Combine(_profile.ServerDirectory, "server.js");'''

pStartRepl = '''var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var hytalePath = Path.Combine(appDataPath, @"Hytale\install\release\package\game\latest");
            var hytaleServerPath = Path.Combine(hytalePath, "Server");
            
            var hasRealJar = File.Exists(Path.Combine(hytaleServerPath, "HytaleServer.jar"));

            if (!hasRealJar)
            {
                var serverJsPath = Path.Combine(_profile.ServerDirectory, "server.js");'''

coreText = coreText.replace(pStartFind, pStartRepl)


pStartFind2 = '''var hasRealJar = File.Exists(Path.Combine(p.ServerDirectory, "HytaleServer.jar"));

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
                
            _process = _processManager.Start(p.ServerDirectory, cmd, args);'''

pStartRepl2 = '''var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var hytalePath = Path.Combine(appDataPath, @"Hytale\install\release\package\game\latest");
        var hytaleServerPath = Path.Combine(hytalePath, "Server");
        
        var hasRealJar = File.Exists(Path.Combine(hytaleServerPath, "HytaleServer.jar"));

        var cmd = hasRealJar ? "java" : "node";
        var ram = p.Resources;
        var args = hasRealJar 
            ? $"-Xms{ram.RamMinMb}M -Xmx{ram.RamMb}M -XX:+UseG1GC -jar HytaleServer.jar --assets ../Assets.zip --bind 0.0.0.0:{p.Port} --backup --backup-dir backups --backup-frequency 30"
            : "server.js";

        var execContext = hasRealJar ? hytaleServerPath : p.ServerDirectory;

        try
        {
            if (hasRealJar) 
                Emit(LogLevel.Information, $"[ServerMaster] Iniciando Engine Hytale (AppData) na porta {p.Port}...");
            else
                Environment.SetEnvironmentVariable("PORT", p.Port.ToString(), EnvironmentVariableTarget.Process);
                
            _process = _processManager.Start(execContext, cmd, args);'''

coreText = coreText.replace(pStartFind2, pStartRepl2)

with open('src/ServerMaster.Core/Engines/HytaleServer.cs', 'w', encoding='utf-8') as f:
    f.write(coreText)
