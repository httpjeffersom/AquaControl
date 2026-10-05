# Aqua Controll

Aqua Control é um aplicativo Windows para monitorar a temperatura da CPU e enviar esse valor para o display do watercooler Pichau Aqua 240X e suas variantes.

O projeto foi criado com foco em compatibilidade real com Windows 11, sem depender de driver kernel customizado, sem WinRing0 e sem instalação de serviço ou binário de baixo nível.

<p align="center">
  <img src="https://github.com/user-attachments/assets/eeed29b4-9daf-490b-bf00-96a22662d1d2" alt="Aqua Control" width="900" />
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
- driver CH340 instalado pelo sistema operacional
- execução como administrador (somente para tarefas de inicialização e acesso ao ambiente do Windows)

## Hardware suportado

Aqua 120x
Aqua 240X
Aqua 360X

O projeto foi validado para os modelos:
-
  Pichau Aqua 120X 
- Pichau Aqua 240X
- Pichau Aqua 360X


## Como funciona

1. O programa localiza a porta serial do watercooler pelo VID/PID do CH340.
2. Abre a conexão serial em modo usuário.
3. Lê a temperatura da CPU usando a biblioteca de sensores.
4. Monta o pacote de atualização do display.
5. Envia o valor para o cooler.

## Segurança e Defender

Este projeto foi desenvolvido para evitar uso de driver kernel vulnerável e não depende de WinRing0, WinRing0x64, WinRing0x86 ou qualquer driver similar.

A abordagem atual usa:

- `LibreHardwareMonitorLib` para leitura de hardware em modo usuário
- `System.IO.Ports` para acesso serial em modo usuário
- APIs do Windows e serviços do sistema, sem driver personalizado

Isso evita os riscos associados a drivers de baixo nível e mantém o projeto compatível com Windows 11 e com as políticas de segurança do Microsoft Defender.

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
dotnet build
```

Em seguida, execute o aplicativo gerado ou abra a solução no Visual Studio.

### Compilar

dotnet publish -c Release -r win-x64 --self-contained false
O executável será publicado em bin/Release/net10.0-windows/win-x64/publish/Aqua Control.exe.

O software oficial do watercooler deve permanecer fechado enquanto o Aqua Control usa a porta serial do dispositivo. A porta é detectada automaticamente pelo VID/PID do controlador CH340.

## Estrutura do projeto

```text
AquaControl/
├── Core/
│   ├── Aqua240XPortDiscovery.cs
│   ├── Aqua240XProtocol.cs
│   ├── Aqua240XSerialClient.cs
│   ├── CpuTemperatureReader.cs
│   ├── StartupManager.cs
│   ├── TemperatureRamp.cs
│   └── WatercoolerMonitorService.cs
├── Controls/
│   └── TemperatureRing.cs
├── Views/
│   ├── MainWindow.xaml
│   └── MainWindow.xaml.cs
├── ViewModels/
│   └── MainViewModel.cs
├── App.xaml
├── App.xaml.cs
├── Program.cs
├── WatercoolerTemp.csproj
├── app.manifest
├── LICENSE
├── README.md
└── Assets/
```

## Limitações conhecidas


- o software oficial do watercooler não deve estar em execução ao mesmo tempo


## Status do projeto

- [x] Detecção automática da porta serial
- [x] Leitura da temperatura da CPU
- [x] Envio para o display do watercooler
- [x] Interface WPF
- [x] Alertas de temperatura alta
- [x] Inicialização com o Windows
- [x] Sem driver personalizado de kernel

## Licença

Este projeto está licenciado sob a licença MIT. Consulte o arquivo [LICENSE](LICENSE).

## Agradecimentos

- Flaticon pela arte do ícone


## Observação importante


Se você quiser contribuir, abrir issues ou propor melhorias, fique à vontade para enviar pull requests e comentários.
