param([string]$OutputPath)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $OutputPath){$OutputPath=Join-Path $taskRoot 'build\generated'}
$taskTools=Join-Path $taskRoot 'build\tools'
New-Item -ItemType Directory -Path $taskTools -Force | Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not(Test-Path -LiteralPath $taskCompiler)){throw 'Compilador .NET Framework x64 não encontrado.'}
& $taskCompiler /nologo /target:exe /platform:x64 /r:System.Drawing.dll "/out:$taskTools\GenerateAssets.exe" (Join-Path $PSScriptRoot 'Generate-Assets.cs')
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o gerador de recursos.'}
& (Join-Path $taskTools 'GenerateAssets.exe') $OutputPath
if($LASTEXITCODE -ne 0){throw 'Falha ao gerar os recursos visuais.'}
