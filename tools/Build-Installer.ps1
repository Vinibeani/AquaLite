param([string]$Compiler)
$ErrorActionPreference='Stop'
$installerRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $Compiler){
    $installerCandidates=@((Join-Path $installerRoot 'build\tooling\InnoSetup\ISCC.exe'),(Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),(Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),(Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'))
    $Compiler=$installerCandidates | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
}
if(-not $Compiler){throw 'Instale o Inno Setup 6 ou 7, ou informe -Compiler com o caminho de ISCC.exe.'}
if(-not(Test-Path -LiteralPath (Join-Path $installerRoot 'build\app\AquaLite.exe'))){throw 'Execute build.ps1 antes de gerar o instalador.'}
& $Compiler (Join-Path $installerRoot 'installer\AquaLite.iss')
if($LASTEXITCODE -ne 0){throw 'Falha ao gerar o instalador.'}
Write-Output ('Instalador gerado: '+(Join-Path $installerRoot 'dist\AquaLite-Instalador.exe'))
