# Temas

Os temas Aqua, Brasa e Oceano são definidos em JSON e usam arte gerada pelo projeto. O gerador cria `background.png` e `preview.png` durante a compilação.

Para criar outro tema, copie uma pasta dentro de `themes`, escolha um `Id` único e ajuste `Name` e `Layers`. Os tipos básicos são `Image`, `Text`, `Data`, `StatuBar` e `Clock`. Use coordenadas de 0 a 480 e cores em hexadecimal. A fonte padrão é Segoe UI, instalada no Windows.

Exemplos de dados: `CPUTEMP`, `GPUTEMP`, `CPULOAD`, `RAMLOAD`, `TIME`, `DATE`, `Volume`, `UPSPEED` e `DOWNDSPEED`. Leituras não disponíveis aparecem como traço.

Para um fundo de vídeo pessoal:

1. Prepare o H.264 com `tools/Convert-Video.ps1`.
2. Guarde o MP4 e o H.264 na pasta do tema na sua distribuição local.
3. Adicione uma camada `Animation` com `Image` apontando para o MP4.
4. Defina `NativeVideo` para o caminho relativo do H.264 e `VideoFrameRate` para a taxa da conversão.
5. Mantenha as demais camadas transparentes onde o vídeo deve aparecer.

O campo opcional `VideoReplacesImage` identifica uma imagem de fundo estática substituída pelo vídeo. Não o use para esconder elementos adicionais do tema. Mídias pessoais ficam ignoradas pelo Git por padrão.
