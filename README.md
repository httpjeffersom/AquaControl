# Aqua Control

Aqua Control é um aplicativo Windows para monitorar a temperatura da CPU e enviar esse valor para o display do watercooler Pichau Aqua 240X e suas variantes, além do Rise Mode Aura Ice ARGB.

O projeto foi criado com foco em compatibilidade real com Windows 11, sem driver kernel próprio e sem instalação de serviço.

<p align="center">
  <img width="1113" height="591" alt="Aqua Control" src="https://github.com/user-attachments/assets/73fcdad7-a6cc-4f40-b2a2-07d0ba13af9b" />

</p>

## Visão geral

O Pichau Aqua 240X e modelos relacionados usam um controlador CH340 para comunicação USB em modo serial virtual. O projeto detecta a porta correta automaticamente, lê a temperatura do processador pelo Windows e envia o dado para o display do cooler.

A lógica de leitura usa monitoramento de hardware em modo usuário e a comunicação com o cooler é feita via porta serial do sistema operacional, sem a criação de um driver personalizado.

## Principais funcionalidades

- detecção automática da porta COM do cooler via VID/PID
- leitura da temperatura da CPU usando APIs do Windows e biblioteca de sensores
- envio da temperatura para o display do watercooler
- animação visual da temperatura em interface WPF
- exibição de mínima, média e máxima
- alerta de temperatura alta
- suporte a minimização para bandeja do sistema
- inicialização com o Windows
- reconexão automática se o dispositivo sair ou não estiver presente

## Requisitos

- Windows 10/11
- conector USB do cooler conectado
- Pichau Aqua: driver CH340 instalado pelo sistema operacional
- Rise Mode Aura Ice: nada extra, o Windows já expõe o dispositivo como HID (sem Zadig/WinUSB)
- execução como administrador (somente para tarefas de inicialização e acesso ao ambiente do Windows)

## Hardware suportado

O projeto foi validado para os modelos:

- Pichau Aqua 120X 
- Pichau Aqua 240X
- Pichau Aqua 360X
- Rise Mode Aura Ice ARGB 240 mm branco (`RM-WAI-02-ARGB`)

O protocolo do Rise Mode foi confirmado por engenharia reversa e por uma
implementação Windows independente. O projeto de referência Linux foi testado
em um Aura Ice Black 360 mm; o modelo branco de 240 mm usa o mesmo controlador
HID (VID `0xAA88` / PID `0x8666`) e o mesmo payload, mas a confirmação final
depende de teste com o hardware físico.

## Como funciona

1. O programa procura os dois tipos de water cooler: porta serial CH340 do
   Pichau e HID nativo do Rise Mode.
2. Para o Pichau, abre a porta serial em modo usuário. Para o Rise Mode, abre o
   dispositivo HID pelo caminho do Windows.
3. Lê a temperatura da CPU usando a biblioteca de sensores.
4. Monta o pacote de atualização no protocolo de cada modelo.
5. Envia o valor para o cooler. Os dois podem funcionar ao mesmo tempo.

O protocolo detalhado do Rise Mode está em
[`Core/RiseModeAuraIce/PROTOCOL.md`](Core/RiseModeAuraIce/PROTOCOL.md).

## Segurança e Defender

O AquaControl não instala nem carrega driver próprio. O aplicativo conversa com o cooler pela porta serial do Windows e não altera o registro além do necessário.

Para ler a temperatura da CPU, o projeto usa a biblioteca `LibreHardwareMonitorLib`, a mesma engine open source do projeto [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor). Para acessar sensores que o Windows não expõe (como a temperatura "Package" de CPUs Intel, lida via MSR), essa biblioteca carrega em modo kernel o driver **WinRing0**, que vem embutido nela — não é empacotado nem instalado à parte pelo AquaControl. Essa é a abordagem padrão de ferramentas conhecidas de monitoramento (LibreHardwareMonitor, FanControl e similares).

O WinRing0 é um driver genérico de acesso ao hardware com vulnerabilidades já documentadas publicamente, e é muito usado por malware real — por isso os antivírus aplicam heurística agressiva a qualquer aplicativo que o carregue. É exatamente isso que dispara o alerta do Windows Defender na primeira execução, e não qualquer comportamento malicioso do AquaControl.

Na prática:

- nenhum dado sai do computador: o programa não faz acesso à rede
- a biblioteca de sensores é usada apenas para leitura de sensores de hardware
- o executável não é empacotado, ofuscado nem faz download de nada
- o alerta do Defender é um falso-positivo de heurística conhecido nesta categoria de software

Código-fonte 100% aberto e auditável neste repositório: cada linha do que o programa faz está aqui.

Se o Defender bloquear a primeira execução, autorize dentro do histórico de proteção do Windows Defender. Se preferir conferir por conta própria, envie o executável ao [VirusTotal](https://www.virustotal.com) e observe os fabricantes que acusarem detecção: falsos positivos por heurística são comuns em ferramentas de monitoramento de hardware e os fornecedores costumam remover a deteção após a análise.

## Instalação e execução

### Pré-requisitos

- .NET SDK 10.0 ou superior
- Visual Studio 2022 ou VS Code com suporte a .NET
- Windows com suporte ao WPF

### Clonar o projeto

```bash
git clone https://github.com/seu-usuario/Aqua-Control.git
cd Aqua-Control
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

O executável será publicado em bin/Release/net10.0-windows/win-x64/publish/AquaControl.exe.

### Testes

```bash
dotnet test AquaControl.sln
```


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
├── App.xaml
├── App.xaml.cs
├── Program.cs
├── app.manifest
├── Sign-AquaControl.ps1
├── LICENSE
├── README.md
└── Assets/
```

## Limitações conhecidas


- o software oficial do watercooler (Rise Mode ou Pichau) não deve estar em execução ao mesmo tempo
- o Rise Mode não expõe nome comercial legível: quem explicar os identificadores `0xAA88/0x8666` sem relatório de saída compatível é ignorado; outros water coolers com o mesmo controlador (por exemplo, revendas como Mancer) podem ser aceitos pelo mesmo protocolo
- se o usuário usou script Python/libusb anteriormente e o display recebeu driver WinUSB via Zadig, o Aqua Control não encontra o sistema; desinstale o driver no Gerenciador de Dispositivos e verifique as alterações de hardware para o Windows recriar o HID padrão
- Infelizmente o programa não tem verificação e é bloqueado pelo Windows defender na primeira execução, você deve permitir dentro do histórico de proteção do Windows defender, não há nada malicioso e tudo é local.


## Status do projeto

- [x] Detecção automática da porta serial
- [x] Leitura da temperatura da CPU
- [x] Envio para o display do watercooler
- [x] Interface WPF
- [x] Alertas de temperatura alta
- [x] Inicialização com o Windows
- [x] Sem driver personalizado de kernel
- [x] Suporte ao Rise Mode Aura Ice via HID nativo (driver independente)

## Licença

Este projeto está licenciado sob a licença MIT. Consulte o arquivo [LICENSE](LICENSE).

## Agradecimentos

- Flaticon pela arte do ícone


## Observação importante


Se você quiser contribuir, abrir issues ou propor melhorias, fique à vontade para enviar pull requests e comentários.
