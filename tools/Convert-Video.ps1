param([Parameter(Mandatory)][string]$InputPath,[Parameter(Mandatory)][string]$OutputPath,[string]$FfmpegPath)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not(Test-Path -LiteralPath $InputPath -PathType Leaf)){throw 'Vídeo de entrada não encontrado.'}
if(Test-Path -LiteralPath $OutputPath){throw 'O destino já existe; escolha outro nome para preservar o arquivo.'}
if(-not $FfmpegPath){
    $taskLocal=Join-Path $taskRoot 'deps\media\ffmpeg.exe'
    if(Test-Path -LiteralPath $taskLocal){$FfmpegPath=$taskLocal}else{$FfmpegPath=(Get-Command ffmpeg -ErrorAction Stop).Source}
}
& $FfmpegPath -nostdin -hide_banner -i $InputPath -map 0:v:0 -vf 'scale=480:480:force_original_aspect_ratio=decrease,pad=480:480:(ow-iw)/2:(oh-ih)/2,setsar=1' -c:v libx264 -preset ultrafast -tune zerolatency -x264opts bframes=0:repeat-headers=1 -crf 20 -pix_fmt yuv420p -an -f h264 $OutputPath
if($LASTEXITCODE -ne 0){throw 'Falha na conversão do vídeo.'}
Write-Output "Vídeo preparado: $OutputPath. Configure VideoFrameRate no tema com a taxa indicada pelo conversor."
