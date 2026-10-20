param([switch]$Test,[switch]$Package)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath($PSScriptRoot)
$taskBuild=[IO.Path]::GetFullPath((Join-Path $taskRoot 'build'))
$taskApp=[IO.Path]::GetFullPath((Join-Path $taskBuild 'app'))
if(-not $taskApp.StartsWith($taskBuild+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Destino de compilação inválido.'}
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not(Test-Path -LiteralPath $taskCompiler)){throw 'Este script requer Windows x64 com .NET Framework.'}
& (Join-Path $taskRoot 'tools\Generate-Assets.ps1')
if(Test-Path -LiteralPath $taskApp){Remove-Item -LiteralPath $taskApp -Recurse -Force}
New-Item -ItemType Directory -Path $taskApp -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'build\generated\assets') -Destination (Join-Path $taskApp 'assets') -Recurse
Copy-Item -LiteralPath (Join-Path $taskRoot 'assets\logo.svg') -Destination (Join-Path $taskApp 'assets\logo.svg')
Copy-Item -LiteralPath (Join-Path $taskRoot 'themes') -Destination (Join-Path $taskApp 'themes') -Recurse
foreach($taskTheme in Get-ChildItem (Join-Path $taskRoot 'build\generated\themes') -Directory){Copy-Item -Path (Join-Path $taskTheme.FullName '*') -Destination (Join-Path $taskApp ('themes\'+$taskTheme.Name)) -Force}
foreach($taskOptional in @('sensors','media')){
    $taskOptionalPath=Join-Path $taskRoot ('deps\'+$taskOptional)
    New-Item -ItemType Directory -Path (Join-Path $taskApp $taskOptional) -Force | Out-Null
    Get-ChildItem -LiteralPath $taskOptionalPath -File | Where-Object {$_.Name -ne 'README.md'} | Copy-Item -Destination (Join-Path $taskApp $taskOptional)
}
if(Test-Path -LiteralPath (Join-Path $taskApp 'media\ffmpeg.exe')){Copy-Item -LiteralPath (Join-Path $taskApp 'media\ffmpeg.exe') -Destination (Join-Path $taskApp 'media\decoder.exe') -Force}
$taskSources=Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src\AquaLite') -Recurse -Filter '*.cs' | Select-Object -ExpandProperty FullName
$taskIcon=Join-Path $taskApp 'assets\aqualite.ico'
$taskReferences=@('/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Management.dll','/r:System.Runtime.Serialization.dll','/r:System.ServiceProcess.dll','/r:Microsoft.CSharp.dll')
& $taskCompiler /nologo /target:library /platform:x64 "/out:$taskApp\assets\AquaLite.DeviceIcons.dll" "/win32icon:$taskIcon" (Join-Path $taskRoot 'src\AquaLite\Devices\IconResource.cs')
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o ícone do dispositivo.'}
& $taskCompiler /nologo /target:winexe /platform:x64 /optimize+ /debug- /utf8output "/out:$taskApp\AquaLite.exe" "/win32icon:$taskIcon" "/win32manifest:$taskRoot\src\AquaLite\app.manifest" $taskReferences $taskSources
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o aplicativo.'}
Copy-Item -LiteralPath (Join-Path $taskRoot 'src\AquaLite\App.config') -Destination (Join-Path $taskApp 'AquaLite.exe.config')
foreach($taskDocument in @('README.md','LICENSE','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath (Join-Path $taskRoot $taskDocument) -Destination $taskApp}
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs') -Destination (Join-Path $taskApp 'docs') -Recurse
if($Test){
    foreach($taskTest in Get-ChildItem -LiteralPath (Join-Path $taskRoot 'tests') -Filter '*.cs'){
        $taskTestExe=Join-Path $taskApp ($taskTest.BaseName+'.exe')
        & $taskCompiler /nologo /target:exe /platform:x64 "/out:$taskTestExe" "/r:$taskApp\AquaLite.exe" /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.ServiceProcess.dll $taskTest.FullName
        if($LASTEXITCODE -ne 0){throw "Falha ao compilar $($taskTest.Name)."}
        if($taskTest.BaseName -eq 'ValidateWindow'){& $taskTestExe (Join-Path $taskBuild 'screenshots')}else{& $taskTestExe}
        if($LASTEXITCODE -ne 0){throw "Falha em $($taskTest.Name)."}
        Remove-Item -LiteralPath $taskTestExe
    }
}
if($Package){
    & (Join-Path $taskRoot 'tools\Build-Installer.ps1')
}
Write-Output "AquaLite compilado: $taskApp\AquaLite.exe"
