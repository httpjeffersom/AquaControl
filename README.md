# Aqua Control

Aplicativo Windows para monitorar a temperatura da CPU e enviar esse valor para o
display de water coolers compatíveis, sem driver de kernel próprio e sem serviço
instalado.

Hoje o projeto cobre duas famílias: a linha **Pichau Aqua** (controlador CH340,
via porta serial) e a família **Rise Mode Aura Ice** (HID nativo do Windows). O
programa detecta qual delas o PC tem e controla **um water cooler por vez**;
cada família tem sessão, transporte e protocolo independentes.

<p align="center">
  <img width="1113" height="591" alt="Aqua Control" src="https://github.com/user-attachments/assets/73fcdad7-a6cc-4f40-b2a2-07d0ba13af9b" />

</p>

## Visão geral

A leitura da temperatura usa monitoramento de hardware em modo usuário. A
comunicação com cada cooler é feita pelo transporte que o próprio Windows já
expõe:

- **Pichau Aqua**: porta serial virtual do CH340 (`VID_1A86&PID_484A`).
- **Rise Mode Aura Ice**: dispositivo HID (`VID 0xAA88` / `PID 0x8666`), sem
  Zadig, WinUSB ou libusb.

O programa descobre os dispositivos, mantém uma sessão por dispositivo,
reconecta sozinho quando algo é plugado ou removido e encaminha os eventos para
a interface. Nenhum dado sai do computador.

## Principais funcionalidades

- detecção automática dos water coolers suportados por VID/PID
- detecção única: identifica se o PC tem Pichau **ou** Rise Mode e controla
  apenas o dispositivo encontrado
- leitura da temperatura da CPU com uma única instância do leitor de sensores
- envio da temperatura para o display do cooler
- reconexão automática a cada 5 segundos quando o dispositivo desaparece
- interface WPF com anel de temperatura, uso de CPU e estatísticas da sessão
- estado visual de "sem water cooler": anel vermelho e mensagem no lugar da
  temperatura
- alerta de temperatura alta (90 °C)
- minimização para a bandeja do sistema
- inicialização com o Windows
- log local em `%LOCALAPPDATA%\AquaControl\aquacontrol.log`

## Requisitos

- Windows 10/11
- .NET SDK 10.0 ou superior (para compilar)
- conector USB do cooler
- **Pichau Aqua**: driver CH340 instalado pelo próprio Windows
- **Rise Mode Aura Ice**: nada extra — o Windows já expõe o dispositivo como HID
- execução como administrador (o manifesto do app solicita elevação)

## Hardware suportado

| Família / modelo | Conexão | Situação |
|---|---|---|
| Pichau Aqua 240X | serial CH340 | validado fisicamente nesta máquina |
| Pichau Aqua 120X / 360X | serial CH340 | mesmo protocolo da família Aqua |
| Rise Mode Aura Ice ARGB 240 mm branco (`RM-WAI-02-ARGB`) | HID `0xAA88/0x8666` | alvo do suporte atual |
| Rise Mode Aura Ice Black 240/360 mm ARGB | HID `0xAA88/0x8666` | mesmo controlador e payload, confirmado pelas referências |

Sobre a **família Aura Ice**: os modelos Black e White, 240 e 360 mm, usam o
mesmo controlador HID e o mesmo formato de relatório. A detecção **não** usa o
nome comercial (o nome HID é genérico e o VID/PID é compartilhado por rebrands,
como a Mancer): o app exige o VID/PID correto **e** um relatório de saída de 9
bytes. Ainda assim, outro water cooler com o mesmo controlador pode aceitar o
mesmo protocolo.

O protocolo detalhado do Rise Mode, com as evidências e o que **não** foi
implementado, está em
[`Core/RiseModeAuraIce/PROTOCOL.md`](Core/RiseModeAuraIce/PROTOCOL.md).

> O Aqua Control foi desenhado para **um water cooler por PC**. Se dois forem
> conectados ao mesmo tempo (cenário não suportado), apenas o primeiro
> encontrado é controlado; o segundo fica intocado.

## Como funciona

1. O gerenciador procura os dois tipos de dispositivo: portas seriais CH340 do
   Pichau e dispositivos HID do Rise Mode.
2. O primeiro dispositivo compatível encontrado ganha uma sessão. Enquanto ela
   estiver ativa, nenhum outro candidato é aberto: o app assume um water cooler
   por PC e trabalha com **Pichau ou Rise Mode**, nunca os dois.
3. Um único `CpuTemperatureReader` compartilhado lê a temperatura da CPU.
4. O driver da família detectada monta o pacote no seu protocolo:
   - **Pichau Aqua**: payload de 23 bytes pela serial, a cada 1 segundo.
   - **Rise Mode**: relatório HID de 9 bytes (Report ID 0 + 8 bytes), com burst
     de 5 escritas a cada 50 ms para o display acompanhar rápido.
5. A interface reflete o estado do water cooler detectado: conectado, sem
   dispositivo ou monitoramento pausado.

## Interface

- **Com water cooler**: anel animado com a temperatura, valor em °C, uso de CPU,
  mínima/média/máxima da sessão e a opção de iniciar com o Windows.
- **Sem water cooler**: as mesmas 72 bolinhas do anel ficam cheias e vermelhas,
  na mesma posição, e o lugar da temperatura passa a mostrar
  `NENHUM WATERCOOLER ENCONTRADO`. O uso de CPU continua visível e atualizando.
- **Bandeja**: restaurar a janela, iniciar/parar o monitoramento e sair.
- **Fechar**: pede confirmação antes de encerrar o monitoramento.

## Segurança e Defender

O Aqua Control não instala nem carrega driver próprio. Conversa com o cooler
pela porta serial e pelo HID que o Windows já oferece, e não altera o registro
além do necessário para a inicialização opcional.

Para ler a temperatura da CPU, o projeto usa a biblioteca
`LibreHardwareMonitorLib`, a mesma engine open source do projeto
[LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor).
Para acessar sensores que o Windows não expõe (como a temperatura "Package" de
CPUs Intel, lida via MSR), essa biblioteca carrega em modo kernel o driver
**WinRing0**, que vem embutido nela — não é empacotado nem instalado à parte
pelo Aqua Control. Essa é a abordagem padrão de ferramentas conhecidas de
monitoramento (LibreHardwareMonitor, FanControl e similares).

O WinRing0 é um driver genérico de acesso ao hardware com vulnerabilidades já
documentadas publicamente e é muito usado por malware real — por isso os
antivírus aplicam heurística agressiva a qualquer aplicativo que o carregue. É
exatamente isso que dispara o alerta do Windows Defender na primeira execução,
e não qualquer comportamento malicioso do Aqua Control.

Na prática:

- nenhum dado sai do computador: o programa não faz acesso à rede
- a biblioteca de sensores é usada apenas para leitura de sensores de hardware
- o executável não é empacotado, ofuscado nem faz download de nada
- o alerta do Defender é um falso-positivo de heurística conhecido nesta
  categoria de software

Código-fonte 100% aberto e auditável neste repositório: cada linha do que o
programa faz está aqui.

Se o Defender bloquear a primeira execução, autorize dentro do histórico de
proteção do Windows Defender. Se preferir conferir por conta própria, envie o
executável ao [VirusTotal](https://www.virustotal.com) e observe os fabricantes
que acusarem detecção: falsos positivos por heurística são comuns em ferramentas
de monitoramento de hardware e os fornecedores costumam remover a detecção após
a análise.

## Instalação e execução

### Pré-requisitos

- .NET SDK 10.0 ou superior
- Visual Studio 2022 ou VS Code com suporte a .NET
- Windows com suporte ao WPF

### Clonar o projeto

```bash
git clone https://github.com/httpjeffersom/AquaControl.git
cd AquaControl
```

### Executar em modo de desenvolvimento

```bash
dotnet build AquaControl.sln
```

Em seguida, execute o aplicativo gerado ou abra a solução no Visual Studio.

### Compilar

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

O executável será publicado em
`bin/Release/net10.0-windows/win-x64/publish/AquaControl.exe`.

### Testes

```bash
dotnet test AquaControl.sln
```

A suíte cobre o protocolo do Pichau Aqua, o payload e a detecção do Rise Mode
Aura Ice. Estado atual: **18 testes, todos passando**.

## Estrutura do projeto

```text
AquaControl/
├── AquaControl.sln
├── AquaControl.csproj
├── Core/
│   ├── Aqua240XPortDiscovery.cs
│   ├── Aqua240XProtocol.cs
│   ├── Aqua240XSerialClient.cs
│   ├── AppLogger.cs
│   ├── CpuTemperatureReader.cs
│   ├── StartupManager.cs
│   ├── WatercoolerDeviceManager.cs
│   ├── WatercoolerMonitorService.cs
│   ├── Devices/
│   │   ├── IWatercoolerDeviceSession.cs
│   │   ├── PichauAqua240XSession.cs
│   │   └── RiseModeAuraIceSession.cs
│   └── RiseModeAuraIce/
│       ├── PROTOCOL.md
│       ├── RiseModeAuraIceClient.cs
│       ├── RiseModeAuraIceDeviceDiscovery.cs
│       ├── RiseModeAuraIceDeviceInfo.cs
│       ├── RiseModeAuraIceMonitorService.cs
│       └── RiseModeAuraIceProtocol.cs
├── Controls/
│   └── TemperatureRing.cs
├── Views/
│   ├── MainWindow.xaml
│   └── MainWindow.xaml.cs
├── ViewModels/
│   └── MainViewModel.cs
├── Tests/
│   ├── AquaControl.Tests.csproj
│   ├── Aqua240XProtocolTests.cs
│   └── RiseModeAuraIce/
│       ├── RiseModeAuraIceProtocolTests.cs
│       └── RiseModeAuraIceDetectionTests.cs
├── Assets/
│   ├── AquaControl.ico
│   └── AquaControl.png
├── App.xaml
├── App.xaml.cs
├── Program.cs
├── app.manifest
├── Sign-AquaControl.ps1
├── LICENSE
└── README.md
```

## Limitações conhecidas

- o software oficial do water cooler (Pichau ou Rise Mode) não deve estar em
  execução ao mesmo tempo
- o app trabalha com um water cooler por PC; se dois forem conectados, apenas o
  primeiro encontrado é controlado
- o Rise Mode não expõe nome comercial legível; quem apresentar os
  identificadores `0xAA88/0x8666` sem relatório de saída compatível é ignorado
- outros water coolers com o mesmo controlador (por exemplo, revendas como
  Mancer) podem ser aceitos pelo mesmo protocolo, mas não foram validados
- se o display recebeu driver WinUSB via Zadig em um uso anterior, o Aqua
  Control não o encontra; desinstale o driver no Gerenciador de Dispositivos e
  peça ao Windows para recriar o HID padrão
- infelizmente o programa não tem assinatura digital e é bloqueado pelo Windows
  Defender na primeira execução; permita dentro do histórico de proteção — não
  há nada malicioso e tudo é local

## Próximos passos

Agora vamos incorporar **diversos novos water coolers com visor de
temperatura**, mantendo o mesmo princípio que trouxe o Rise Mode Aura Ice até
aqui:

- suporte por transporte nativo (serial ou HID), sem driver de kernel
- implementação apenas de comandos confirmados por referência ou captura real
- detecção por identificadores, nunca pelo nome comercial
- sessões independentes, para que um modelo novo não quebre os já suportados
- um documento de protocolo por família, com as evidências e o que não foi
  implementado

A família **Rise Mode Aura Ice** é o primeiro passo desse caminho. As próximas
famílias serão anunciadas aqui conforme os protocolos forem confirmados em
hardware real.

Se você tem um water cooler com visor de temperatura e quer ajudar, abra uma
issue informando **modelo, VID/PID e uma captura USB** do software oficial. É
esse material que permite implementar o protocolo sem chutes.

## Status do projeto

- [x] Detecção automática da porta serial do Pichau Aqua
- [x] Leitura da temperatura da CPU
- [x] Envio para o display do watercooler
- [x] Interface WPF
- [x] Alertas de temperatura alta
- [x] Inicialização com o Windows
- [x] Sem driver personalizado de kernel
- [x] Suporte ao Rise Mode Aura Ice via HID nativo (driver independente)
- [x] Detecção única: Pichau **ou** Rise Mode, um water cooler por PC
- [x] Reconexão automática do dispositivo
- [x] Testes automatizados de protocolo e detecção
- [ ] Novas famílias de water coolers com visor de temperatura

## Créditos

Este projeto existe porque outras pessoas publicaram engenharia reversa e
implementações de referência. Crédito aos repositórios que tornaram o suporte ao
Rise Mode Aura Ice possível:

- [bmortella/wc-aura-ice-linux](https://github.com/bmortella/wc-aura-ice-linux)
  — engenharia reversa original do display via USB/HID.
- [rafaelwms/win-watercooler-display](https://github.com/rafaelwms/win-watercooler-display)
  — implementação Windows em C# que confirmou o mesmo payload via HID nativo,
  sem WinUSB/Zadig.
- [liquidctl/liquidctl (#781)](https://github.com/liquidctl/liquidctl/issues/781)
  — descritores USB do dispositivo e informação de que o VID/PID é compartilhado
  por rebrands.
- [Finallf/risemode](https://github.com/Finallf/risemode) — confirmação do
  VID/PID e de escritas curtas via hidraw.
- [Italord0/risemode-arch](https://github.com/Italord0/risemode-arch) — mesma
  detecção por VID/PID e escrita curta de 1 byte.
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
  — biblioteca de leitura de sensores usada pelo projeto.
- [Flaticon](https://www.flaticon.com/) — arte do ícone.

## Licença

Este projeto está licenciado sob a licença MIT. Consulte o arquivo
[LICENSE](LICENSE).

## Contribuindo

Se você quiser contribuir, abrir issues ou propor melhorias, fique à vontade
para enviar pull requests e comentários. Para novos modelos de water cooler, o
material mais útil é sempre o mesmo: modelo, VID/PID e captura USB do software
oficial.
