$files = Get-ChildItem -Path '.' -Filter '*.axaml' -Recurse | Where-Object { $_.Name -ne 'Colors.axaml' }
foreach ($f in $files) { 
    (Get-Content $f.FullName) `
    -replace '#0D0F14', '{StaticResource BrushBgBase}' `
    -replace '#161B27', '{StaticResource BrushBgSurface}' `
    -replace '#1E2534', '{StaticResource BrushBgElevated}' `
    -replace '#252D3D', '{StaticResource BrushBgInput}' `
    -replace '#2A3147', '{StaticResource BrushBorder}' `
    -replace '#1C2235', '{StaticResource BrushBorderSubtle}' `
    -replace '#6C63FF', '{StaticResource BrushAccentPrimary}' `
    -replace '#7D75FF', '{StaticResource BrushAccentPrimaryHover}' `
    -replace '#E8EAF0', '{StaticResource BrushTextPrimary}' `
    -replace '#9CA3AF', '{StaticResource BrushTextSecondary}' `
    -replace '#6B7280', '{StaticResource BrushTextMuted}' `
    -replace '#EF4444', '{StaticResource BrushDanger}' `
    -replace '#22C55E', '{StaticResource BrushSuccess}' `
    -replace '#38BDF8', '{StaticResource BrushInfo}' `
    -replace '#F59E0B', '{StaticResource BrushWarning}' | Set-Content $f.FullName 
}
