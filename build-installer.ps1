$ErrorActionPreference = "Stop"

Write-Host "Publicando projeto Server Master (Tentativa 1)..."
$publishArgs = "publish src\ServerMaster.App\ServerMaster.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish"
$process = Start-Process dotnet -ArgumentList $publishArgs -NoNewWindow -Wait -PassThru
if ($process.ExitCode -ne 0) {
    Write-Host "Falha na publicacao. Tentando novamente..."
    $process = Start-Process dotnet -ArgumentList $publishArgs -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Write-Error "Falha ao publicar o projeto."
        exit 1
    }
}

Write-Host "Verificando Inno Setup..."
if (-not (Test-Path "C:\Program Files (x86)\Inno Setup 6\ISCC.exe")) {
    Write-Host "Instalando Inno Setup via winget..."
    winget install -e --id JRSoftware.InnoSetup --accept-package-agreements --accept-source-agreements
}

Write-Host "Compilando instalador..."
$isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (Test-Path $isccPath) {
    & $isccPath "installer.iss"
    Write-Host "Instalador gerado com sucesso na pasta Output!"
} else {
    Write-Error "Inno Setup não encontrado em $isccPath"
}
