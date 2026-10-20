# Dependências opcionais

## Sensores

Obtenha uma distribuição compatível do LibreHardwareMonitor 0.9.6 pela fonte indicada em THIRD_PARTY_NOTICES.md. Guarde as bibliotecas e as licenças dessa distribuição em `deps/sensors/`. Instale o PawnIO pela distribuição oficial quando necessário. Não é preciso instalar outro driver para compilar o AquaLite.

Depois, compile novamente. O script copia os arquivos presentes em `deps/sensors` para `build/app/sensors`. Para habilitar as leituras, use **Sensores do PC → Habilitar sensores** e confirme o acesso solicitado pelo Windows.

Sem o componente, o aplicativo mostra dados básicos do Windows e indica quais temperaturas estão indisponíveis.

## Vídeo

Coloque uma compilação do FFmpeg e seus avisos de licença em `deps/media/`. O executável pode se chamar `ffmpeg.exe` ou `decoder.exe`; a compilação copia-o para o nome de execução `media/decoder.exe`.

Escolha uma compilação com decodificador H.264 e codificador `libx264` se quiser converter vídeos para o LCD. Os requisitos de licença variam conforme a compilação utilizada.

Para preparar uma mídia pessoal:

```powershell
.\tools\Convert-Video.ps1 -InputPath C:\Midia\video.mp4 -OutputPath C:\Midia\video-lcd.h264
```

A conversão mantém o vídeo completo e desabilita quadros B para a reprodução no LCD. Para usar como fundo de um tema, consulte THEMES.md.
