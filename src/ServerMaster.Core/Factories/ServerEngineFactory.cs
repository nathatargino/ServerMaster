using Microsoft.Extensions.DependencyInjection;
using ServerMaster.Core.Abstractions;
using ServerMaster.Core.Engines;
using ServerMaster.Core.Models;

namespace ServerMaster.Core.Factories;

/// <summary>
/// Concrete Abstract Factory that maps a <see cref="ServerProfile"/>
/// to the correct <see cref="IServerEngine"/> Strategy implementation.
/// </summary>
public sealed class ServerEngineFactory : IServerFactory
{
    private readonly IServiceProvider _services;

    public ServerEngineFactory(IServiceProvider services) => _services = services;

    /// <inheritdoc/>
    public IServerEngine Create(ServerProfile profile) => profile.Game switch
    {
        GameType.Minecraft => ActivatorUtilities.CreateInstance<MinecraftServer>(_services),
        GameType.Hytale    => ActivatorUtilities.CreateInstance<HytaleServer>(_services),
        _ => throw new NotSupportedException($"Jogo não suportado: {profile.Game}")
    };
}
