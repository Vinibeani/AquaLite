# Arquitetura

| Área | Responsabilidade |
|---|---|
| App | Inicialização, instância única e entradas auxiliares |
| UI | Janela principal, galeria, editor e bandeja |
| Configuration | Preferências locais e cópias de configuração |
| Rendering | Composição visual, prévia, JPEG e reprodução local |
| Themes | Catálogo, camadas, alternância e planejamento de vídeo |
| Devices | WinUSB, enquadramento do protocolo e streaming H.264 |
| Telemetry | Sensores, snapshots e comunicação com leitor temporário |
| Services | Consulta opcional de clima |

Na instalação, `DisplayService` inicia um `DisplayEngine` na sessão de serviços do Windows, antes do login. O painel usa `RemoteDisplayEngine` para trocar comandos e receber a prévia pelo serviço, sem abrir outra conexão USB. O motor local é usado pelos testes e pela execução de desenvolvimento. `BackgroundTelemetry` coleta sensores em outra thread para não bloquear a animação. `NativeVideoStream` alimenta o decodificador do LCD e mantém a camada de dados separada.

`MainForm` coordena os eventos e a apresentação do estado. Os controles reutilizáveis ficam em `Controls.cs`, a construção das páginas em `MainForm.Pages.cs` e os comportamentos da janela e bandeja em `MainForm.Window.cs`. Os arquivos parciais organizam a mesma janela; não são módulos independentes. `ApplicationActions` concentra a importação de mídia e a abertura dos processos auxiliares. `WindowsStartup` consulta e configura a inicialização automática do serviço, preservando os métodos públicos de `Settings` como fachada de compatibilidade. A entrada antiga em Run é removida na instalação; não é o mecanismo de início do LCD.

`Reading` é o modelo compartilhado de uma amostra, separado dos provedores de `Telemetry`. Seu método `Copy()` duplica também o dicionário, a lista e os objetos de sensores para não compartilhar alterações entre consumidores.

O transporte se divide em `DisplayProtocol` (codificação dos cabeçalhos), `WinUsbNative` (declarações da API nativa), `UsbDisplayDiscovery` (enumeração) e `UsbDisplay` (sessão conectada, sincronização e comandos). `NativeVideoStream` pertence a Rendering e fornece a camada transparente ao iniciar vídeo. Devices recebe os bytes prontos e não referencia o renderizador, a interface ou os sensores. As duas threads que enviam vídeo e sobreposição continuam compartilhando a mesma trava da sessão.

A janela não pode ser redimensionada. Habilitar a troca automática ativa a galeria, e alterações do intervalo são aplicadas sem reiniciar a sequência a cada cópia das preferências.

No serviço, `CpuSensor` é usado diretamente pela thread de telemetria sob LocalSystem, sem depender de um processo da conta do usuário. O acesso a sensores é preparado na instalação, sem instaladores ou caixas de diálogo durante o boot. Na execução de desenvolvimento, o leitor elevado é temporário: o consumidor sinaliza atividade em memória compartilhada. As amostras possuem verificação de sequência e validade de seis segundos. A comparação de horários respeita a precisão de milissegundos do JSON.

A codificação DES no transporte é exigida pelo formato de mensagens do LCD compatível. Os bytes fixos do protocolo não são credenciais de usuário e não protegem dados de rede.

Os testes de arquitetura comparam seis cabeçalhos completos com hashes capturados do executável 0.3.1 antes da separação dos módulos. Os testes não abrem USB nem alteram a inicialização do Windows. Consulte `ARCHITECTURE_REVIEW.md` para a investigação das sete questões levantadas pelo mapa.
