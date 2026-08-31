import re

# PATCH 1: AutoUpdaterService.cs
with open('src/ServerMaster.Core/Services/AutoUpdaterService.cs', 'r', encoding='utf-8') as f:
    text = f.read()

text = re.sub(r'private const string CurrentVersion = "v1\.0\.0";\s*', '', text)

get_version_meth = '''    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
        return fvi.FileVersion ?? "1.0.0";
    }

    /// <summary>'''
                    
text = text.replace('''    /// <summary>''', get_version_meth, 1)

comparison_meth = '''            var latestTag = root.GetProperty("tag_name").GetString();
            var currentVersion = GetCurrentVersion();
            
            if (string.IsNullOrEmpty(latestTag)) return;
            
            if (latestTag == currentVersion || latestTag == "v" + currentVersion || latestTag.StartsWith("v" + currentVersion + "-")) return;'''
            
text = re.sub(r'var latestTag = root\.GetProperty\("tag_name"\)\.GetString\(\);\s*if \(string\.IsNullOrEmpty\(latestTag\) \|\| latestTag == CurrentVersion\) return;', comparison_meth.strip(), text)

with open('src/ServerMaster.Core/Services/AutoUpdaterService.cs', 'w', encoding='utf-8') as f:
    f.write(text)


# PATCH 2: release.yml
with open('.github/workflows/release.yml', 'r', encoding='utf-8') as f:
    ymlText = f.read()

ymlOverride = '''      - name: Get Version
      id: get_version
      run: |
        if ("${{ github.ref }}" -like "refs/tags/*") {
          $version = "${{ github.ref_name }}"
        } else {
          $csprojPath = "src/ServerMaster.App/ServerMaster.App.csproj"
          $xml = [xml](Get-Content $csprojPath)
          $codeVersion = $xml.Project.PropertyGroup.Version
          if (-not $codeVersion) { $codeVersion = "1.0.0" }
          $timestamp = Get-Date -Format "yyyyMMddHHmmss"
          $version = "v$codeVersion-$timestamp"
        }
        echo "VERSION=$version" >> $env:GITHUB_ENV'''
        
ymlText = re.sub(r'- name: Get Version.*?echo "VERSION=\$version" >> \$env:GITHUB_ENV', ymlOverride.strip(), ymlText, flags=re.DOTALL)

with open('.github/workflows/release.yml', 'w', encoding='utf-8') as f:
    f.write(ymlText)
