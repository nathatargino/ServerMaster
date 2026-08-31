using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ServerMaster.Core.Models;

namespace ServerMaster.Core.Services;

public class ServerRepository
{
    private static readonly string ServersRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ServerMaster", "Servers");

    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public ServerRepository()
    {
        Directory.CreateDirectory(ServersRoot);
    }

    public IReadOnlyList<ServerProfile> GetAllProfiles()
    {
        var profiles = new List<ServerProfile>();
        foreach (var dir in Directory.GetDirectories(ServersRoot))
        {
            var pPath = Path.Combine(dir, "profile.json");
            if (File.Exists(pPath))
            {
                try
                {
                    var json = File.ReadAllText(pPath);
                    var p = JsonSerializer.Deserialize<ServerProfile>(json);
                    if (p != null) profiles.Add(p);
                }
                catch { }
            }
        }
        return profiles.OrderByDescending(p => p.CreatedAt).ToList();
    }

    public void SaveProfile(ServerProfile profile)
    {
        Directory.CreateDirectory(profile.ServerDirectory);
        var pPath = Path.Combine(profile.ServerDirectory, "profile.json");
        var json = JsonSerializer.Serialize(profile, _options);
        File.WriteAllText(pPath, json);
    }

    public void DeleteProfile(ServerProfile profile)
    {
        try
        {
            if (Directory.Exists(profile.ServerDirectory))
            {
                Directory.Delete(profile.ServerDirectory, true);
            }
        }
        catch { }
    }
}
