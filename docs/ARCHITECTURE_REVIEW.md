# Revisão de arquitetura — AquaLite 0.3.2

Nota de evolução: esta revisão registra as sete perguntas sobre o mapa da versão 0.3.1 e a separação inicial da versão 0.3.2. Na versão 0.4.0, a solicitação de funcionamento antes do login acrescentou um serviço real do Windows, armazenamento em ProgramData e painel remoto. `WindowsStartup` passou a configurar esse serviço, substituindo o registro Run. Consulte `SERVICE.md` para o comportamento atual.

Análise de 20/10/2026, iniciada pelo grafo e confirmada no código. A contagem de conexões mede relações estáticas, incluindo tipos e bibliotecas; não mede lentidão, defeitos ou dependências de execução. Comunidades do grafo também não equivalem a módulos do aplicativo.

## 1. Por que MainForm conecta tantas áreas?

A janela coordena navegação, preferências, prévia, galeria e editor. Ela cria `DisplayEngine`, mas não escreve nos canais WinUSB. O envio pertence a `DisplayEngine` e `UsbDisplay`. No código anterior, a janela também consultava diretamente `CpuBridge`, iniciava auxiliares elevados, validava mídia e copiava arquivos.

Foram extraídos os controles visuais, a construção das páginas e os comportamentos da janela para arquivos próprios. Importação de mídia e processos auxiliares agora pertencem a `ApplicationActions`. A inicialização com Windows foi isolada em `WindowsStartup`, com a API de `Settings` preservada. A janela mantém as conexões necessárias para coordenar sua apresentação.

## 2. As duas relações inferidas de MainForm estão corretas?

Sim. Ambas são chamadas explícitas ao construtor:

| Origem no mapa anterior | Evidência | Resultado |
|---|---|---|
| `Program.Main`, `App/Program.cs`, L24 | `new MainForm(new Settings(),true,false)` | Confirmada; abertura em modo de prévia |
| `ValidateWindow.Main`, `tests/ValidateWindow.cs`, L17 | `new MainForm(config,true,false)` | Confirmada; teste visual sem USB |

Há também a criação da janela normal em `Program.Main`. O grafo anterior agregava chamadas entre o mesmo par de elementos, por isso as duas relações listadas não representam todas as ocorrências. A classificação auditada é `constructs`, com confiança EXTRACTED 1.0, em vez de chamada de função inferida.

## 3. Como Folder, PathName e StartupEnabled se conectam?

`Settings.Folder` resolve `%LOCALAPPDATA%\AquaLite`. É usado por preferências, mídia importada, vídeo convertido, diagnóstico e backups das alterações opcionais do Windows. `Settings.PathName` acrescenta `configuracao.json`; `Load()` lê esse arquivo e `Save()` usa um temporário com substituição e backup.

`Settings.StartupEnabled` resume `StartupState`. A página de configurações consulta esse estado, e `SetStartup()` altera a entrada de inicialização. Na revisão, esses acessos ao registro foram delegados a `WindowsStartup`. O código de registro conserva seu comportamento anterior; esta análise não habilitou nem desabilitou a inicialização neste computador.

As três propriedades pareciam isoladas porque o extrator registrou suas declarações, mas perdeu os acessos aos getters. Não eram funcionalidades desconectadas. `StartupState` também preserva o resultado desconhecido quando a consulta é bloqueada, para a interface não exibir um estado confirmado indevidamente.

## 4. A interface deveria ser dividida?

Sim, por responsabilidade. A separação inicial foi aplicada sem reescrever coordenadas, textos ou eventos dos controles: `Controls.cs`, `MainForm.Pages.cs`, `MainForm.Window.cs` e os serviços citados acima. Galeria, sensores e editor já possuem classes próprias.

Os arquivos parciais de MainForm são organização do código, não uma eliminação artificial do acoplamento. Uma futura divisão de cada página em um controle autônomo pode fazer sentido ao ampliar a interface. A baixa coesão calculada pelo grafo, sozinha, não justifica essa reescrita nem demonstra um problema de desempenho.

## 5. Por que UsbDisplay aparece conectado a sensores e bibliotecas?

`UsbDisplay` usa estruturas e funções nativas do Windows, como `IntPtr`, `DllImport` e handles. A telemetria também usa tipos nativos, o que aproxima essas áreas no agrupamento do grafo. Isso não significa que o transporte leia temperatura ou dependa dos sensores.

`DisplayEngine` é o ponto de composição: recebe as leituras, solicita a imagem e envia seus bytes. A dependência direta real que merecia correção era `UsbDisplay.BeginH264()` criar uma imagem usando `ScreenRenderer.EmptyOverlay()`. Agora recebe o PNG pronto do streaming de vídeo; a camada Devices não conhece o renderizador.

## 6. As cinco relações inferidas de Reading estão corretas?

Sim. Todas são criações explícitas do modelo de dados:

| Origem no mapa anterior | Evidência e finalidade |
|---|---|
| `DisplayEngine.Run`, L54 | `new Reading()` inicia a leitura antes da primeira coleta |
| `BackgroundTelemetry.Run`, L54 | `new Reading { SensorStatus=... }` representa falha do leitor |
| `MainForm.EditCustom`, L260 | `new Reading()` permite abrir o editor sem motor disponível |
| `ValidateCompatibility.Main`, L7 | Instância com valores de teste dos canais dos temas |
| `ValidateMetrics.Main`, L14 | Instância com valores de teste das unidades e indicadores |

As linhas referem-se ao mapa anterior à refatoração. O resultado auditado é EXTRACTED 1.0, relação `constructs`. `Reading` foi separado de `Telemetry.cs` e recebeu `Copy()` com cópia profunda das coleções e dos sensores. Os valores de teste permanecem nos testes, e não são apresentados como medições reais.

## 7. O transporte USB deveria ser dividido?

Sim, mantendo uma sessão sincronizada. A divisão aplicada separa codificação do protocolo, declarações nativas, descoberta do dispositivo e comandos da sessão. O fluxo H.264 foi movido para Rendering, onde pode preparar sua sobreposição antes de entregá-la ao transporte.

A ordenação dos comandos, os timeouts, o controle de fila e a trava que serializa transferências foram conservados. Não foram criadas sessões concorrentes nem alterados os bytes de protocolo para obter um grafo com menos conexões. A assinatura de `BeginH264` agora recebe a camada PNG, e seu chamador foi atualizado.

## Validação e limites

- Seis cabeçalhos completos comparados com hashes do executável anterior, capturados antes das mudanças.
- Cópias de Reading verificadas contra alterações cruzadas em valores, listas e objetos de sensores.
- Camada transparente decodificada e verificada em 480 × 480.
- Camada ausente rejeitada antes de qualquer acesso USB.
- Testes existentes de unidades, compatibilidade, troca automática e janela fixa executados junto aos novos testes.

Os testes automáticos não acessam USB, não alteram o registro e não substituem uma nova verificação visual no watercooler. Os diagnósticos de pontas não declaradas, agregação de relações e autorrelações do extrator continuam publicados no relatório do grafo; as sete verificações acima não equivalem a uma auditoria de todas as relações inferidas.
