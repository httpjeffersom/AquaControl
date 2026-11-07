# Aqua Control

Aplicativo Windows para monitorar a temperatura da CPU e enviar esse valor para o
display do water cooler **Pichau Aqua**, sem driver de kernel próprio e sem
serviço instalado.

O programa detecta o cooler pela porta serial do controlador CH340 e conversa
com ele pelo protocolo da família Aqua.

<p align="center">
  <img width="1113" height="591" alt="Aqua Control" src="https://github.com/user-attachments/assets/73fcdad7-a6cc-4f40-b2a2-07d0ba13af9b" />

</p>

## Visão geral

A leitura da temperatura usa monitoramento de hardware em modo usuário. A
comunicação com o cooler é feita pelo transporte que o próprio Windows já
expõe: a porta serial virtual do CH340 (`VID_1A86&PID_484A`).

O programa descobre o dispositivo, mantém uma sessão ativa, reconecta sozinho
quando ele é plugado ou removido e encaminha os eventos para a interface.
Nenhum dado sai do computador.

## Principais funcionalidades

- detecção automática da porta serial do Pichau Aqua por VID/PID
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
- **PawnIO** instalado (driver assinado usado pela biblioteca de sensores para
  ler a CPU): [pawnio.eu](https://pawnio.eu) ou
  `winget install -e --id namazso.PawnIO`
- driver CH340 instalado pelo próprio Windows
- execução como administrador (o manifesto do app solicita elevação)

## Hardware suportado

| Modelo | Conexão | Situação |
|---|---|---|
| Pichau Aqua 120X | serial CH340 | mesmo protocolo da família Aqua |
| Pichau Aqua 240X | serial CH340 | validado fisicamente por mim |
| Pichau Aqua 360X | serial CH340 | mesmo protocolo da família Aqua |

> O Aqua Control foi desenhado para **um water cooler por PC**. Se mais de uma
> porta compatível for encontrada (cenário não suportado), apenas a primeira é
> controlada.

## Como funciona

1. O gerenciador procura portas seriais CH340 compatíveis com o Pichau Aqua.
2. A primeira porta encontrada ganha uma sessão. Enquanto ela estiver ativa,
   nenhum outro candidato é aberto.
3. Um único `CpuTemperatureReader` compartilhado lê a temperatura da CPU.
4. O protocolo monta o pacote de 23 bytes enviado pela serial; o app envia
   quando a temperatura muda, com reenvio de manutenção a cada 2 segundos.
5. A interface reflete o estado do cooler: conectado, sem dispositivo ou
   monitoramento pausado.

## Interface

- **Com water cooler**: anel animado com a temperatura, valor em °C, uso de CPU,
  mínima/média/máxima da sessão e a opção de iniciar com o Windows.
- **Sem water cooler**: as mesmas 72 bolinhas do anel ficam cheias e vermelhas,
  na mesma posição, e o lugar da temperatura passa a mostrar
  `NENHUM WATERCOOLER ENCONTRADO`. O uso de CPU continua visível e atualizando.
- **Bandeja**: restaurar a janela, iniciar/parar o monitoramento e sair.
- **Fechar**: pede confirmação antes de encerrar o monitoramento.

## Segurança e Defender

O Aqua Control não instala driver próprio nem mantém serviço: fala com o cooler
pela porta serial que o Windows já oferece, e só altera o registro para a
inicialização opcional.

Para ler a temperatura da CPU, usa a `LibreHardwareMonitorLib`, que desde a
versão **0.9.5 deixou de usar o WinRing0**. O acesso aos MSR/SMU da CPU é feito
pelo **PawnIO**, um driver de kernel **assinado e open source**, mantido por
[namazso](https://pawnio.eu), que executa módulos verificados em vez de expor
memória física e MSR a qualquer processo. O PawnIO é instalado à parte
([pawnio.eu](https://pawnio.eu) ou `winget install -e --id namazso.PawnIO`) e o
app detecta quando ele não está presente, avisando na interface.

Essa troca elimina o alerta `VulnerableDriver:WinNT/Winring0` que o Windows
Defender passou a disparar em 2025 contra aplicativos que carregavam o WinRing0
— exatamente o motivo pelo qual LibreHardwareMonitor, FanControl, OpenRGB e
outros migraram para o PawnIO.

Na prática:

- nenhum dado sai do computador: o programa não faz acesso à rede
- o app não carrega driver próprio; o PawnIO é assinado e de código aberto
- o executável não é empacotado, ofuscado nem faz download de nada
- sem PawnIO o app continua abrindo, mas mostra "PAWNIO NÃO INSTALADO" e não
  envia temperatura

Código-fonte 100% aberto e auditável neste repositório: cada linha do que o
programa faz está aqui.

O executável não tem assinatura de código, então o SmartScreen pode exibir um
aviso na primeira execução de builds baixadas da internet. Se quiser conferir
por conta própria, envie o executável ao
[VirusTotal](https://www.virustotal.com).

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

A suíte cobre o protocolo do Pichau Aqua. Estado atual: **4 testes, todos
passando**.

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
│   └── Devices/
│       ├── IWatercoolerDeviceSession.cs
│       └── PichauAqua240XSession.cs
├── Controls/
│   └── TemperatureRing.cs
├── Views/
│   ├── MainWindow.xaml
│   └── MainWindow.xaml.cs
├── ViewModels/
│   └── MainViewModel.cs
├── Tests/
│   ├── AquaControl.Tests.csproj
│   └── Aqua240XProtocolTests.cs
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

- o software oficial do water cooler não deve estar em execução ao mesmo tempo
- o app trabalha com um water cooler por PC; se mais de uma porta compatível
  for encontrada, apenas a primeira é controlada
- a leitura da CPU depende do PawnIO instalado; sem ele o app abre, mas mostra
  `PAWNIO NÃO INSTALADO — SEM LEITURA DA CPU` e não envia temperatura
- o executável não tem assinatura de código; o SmartScreen pode exibir aviso na
  primeira execução (não é detecção de malware)

## Próximos passos

O suporte é focado na família Pichau Aqua. Novos modelos de water cooler com
visor de temperatura podem ser adicionados seguindo o mesmo princípio:

- suporte por transporte nativo (serial), sem driver de kernel
- implementação apenas de comandos confirmados por referência ou captura real
- detecção por identificadores, nunca pelo nome comercial
- sessões independentes, para que um modelo novo não quebre os já suportados
- um documento de protocolo por família, com as evidências e o que não foi
  implementado

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
- [x] Sem driver próprio de kernel (sensores de CPU via PawnIO assinado)
- [x] Reconexão automática do dispositivo
- [x] Testes automatizados de protocolo e detecção


## Créditos

- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
  — biblioteca de leitura de sensores usada pelo projeto.
- [PawnIO](https://pawnio.eu) — driver assinado que substituiu o WinRing0 como
  fonte de acesso a MSR/SMU da CPU.
- [Flaticon](https://www.flaticon.com/) — arte do ícone.

## Licença

Este projeto está licenciado sob a licença MIT. Consulte o arquivo
[LICENSE](LICENSE).

## Contribuindo

Se você quiser contribuir, abrir issues ou propor melhorias, fique à vontade
para enviar pull requests e comentários. Para novos modelos de water cooler, o
material mais útil é sempre o mesmo: modelo, VID/PID e captura USB do software
oficial.
