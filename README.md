<h1 align="center">Server Master</h1>
<p align="center">
  <strong>Gerenciador definitivo de Servidores de Jogos para Desktop.</strong><br/>
  Crie, configure e hospede servidores de Minecraft e Hytale com poucos cliques, tudo integrado com Playit.gg para jogar com amigos sem precisar abrir portas no roteador (Sem CGNAT/Port-Forwarding).
</p>

---

## 🚀 Funcionalidades Principais

- **Criação Guiada (Wizard):** Passos simples e intuitivos para configurar Núcleo, Memória RAM, Variantes (Vanilla, Paper, Purpur) e Modos de Rede.
- **Túnel Automático Global (Playit.gg):** Integração nativa de rede que aciona instâncias dinâmicas e isoladas do Playit. Hospede múltiplos servidores diferentes ao mesmo tempo sem conflito de agentes!
- **Dashboards Isoladas (Multi-Instância):** Acompanhe o consumo de CPU e RAM em tempo real com gráficos baseados em `System.Diagnostics`. 
- **Console Integrado:** Acompanhe os logs via *StandardOutput/Error* de forma reativa e envie comandos RCON ou *stdin* diretamente do app.
- **Painel Moderno (UX/UI):** Interface *Dark Mode* elegante feita em cima do Avalonia UI, contendo animações fluídas, painéis colapsáveis flutuantes, overlays translúcidas (Glassmorphism) e SVGs dinâmicos.

## 🛠️ Tecnologias Utilizadas

- **[C# & .NET 8](https://dotnet.microsoft.com/)** - Backend robusto rodando sobre a mais recente plataforma abstrata de código da Microsoft.
- **[Avalonia UI](https://avaloniaui.net/)** - Framework XAML Multi-plataforma poderoso que entrega as interfaces ricas de Desktop.
- **[CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)** - Sistema reativo de *Source Generators* para ligação inteligente de dados (Bindings) entre View e ViewModel.
- **System.Reactive (Rx.NET)** - Fluxo contínuo assíncrono para os *Streams* de Console e Telemetria (RAM/CPU).

## ⚙️ Arquitetura e Engenharia

- **Injection & DI:** Registro de dependências estrito em `App.axaml.cs`. Motores (`IServerEngine`) e Túneis (`INetworkTunnel`) são instanciados transitoriamente (*AddTransient*) para isolar os ambientes lógicos de cada servidor operando simultanemamente.
- **Strategy Pattern:** Os motores de jogo (como `MinecraftServer.cs` e `HytaleServer.cs`) abstraem chamadas de instalação (`PrepareAsync`) e lançamento do processo (`StartAsync`), garantindo escalabilidade máxima caso um suporte a um novo jogo precise ser codado.
- **Sessões Em Memória:** Evita sobreposições e instâncias "fantasmas" através do `SessionManager`. 

## 📦 Como rodar localmente (Desenvolvimento)

1. **Clone o repositório:**
```bash
git clone https://github.com/SeuUsuario/ServerMaster.git
cd ServerMaster
```

2. **Certifique-se de possuir o .NET 8 SDK instalado.**

3. **Inicie o projeto Avalonia:**
```bash
dotnet run --project src/ServerMaster.App -c Release
```

*(O Java Runtime Environment - JRE 17/21 deve estar globalmente acessível via linha de comando para a correta inicialização dos processos do Minecraft).*

## 🤝 Contribuições

Contribuições são bem-vindas! Se você tiver ideias para adicionar suporte a novos motores de jogos, Sinta-se à vontade para abrir uma *Issue* ou submeter um *Pull Request*.

## 📝 Licença
Distribuído sob a licença MIT. Veja `LICENSE` para mais detalhes.
