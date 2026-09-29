# Inicializar com o Windows

O aplicativo exige privilégios de administrador para acessar os sensores de hardware. A opção "Iniciar com o Windows" registra uma tarefa no Agendador de Tarefas com nível de execução mais alto, evitando depender de uma entrada comum na chave `Run` do Registro.

## Configurar

1. Abra o Aqua Control. O Windows solicitará elevação por causa do manifesto do aplicativo.
2. Marque "Iniciar com o Windows".
3. A tarefa `AquaControl` será criada para iniciar no logon do usuário atual, com privilégios mais altos e o argumento `--startup`.

Na próxima entrada no Windows, o aplicativo será iniciado elevado e oculto na bandeja. A tarefa é executada na sessão interativa do usuário. Para desativar, desmarque a opção no aplicativo.

Ao habilitar a tarefa, o aplicativo também remove a antiga entrada `WatercoolerTemp` da chave `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, evitando duas tentativas de inicialização.

## Verificar ou reparar

No Agendador de Tarefas (`taskschd.msc`), procure `AquaControl` na Biblioteca do Agendador de Tarefas. Nas propriedades, confirme que a tarefa está configurada para executar com privilégios mais altos e no logon do usuário correto.

Também é possível consultar o estado pelo PowerShell:

```powershell
schtasks /Query /TN AquaControl /V /FO LIST
```

Se o executável tiver sido movido, desmarque e marque novamente a opção para atualizar o caminho salvo na tarefa. Se a tarefa não for criada, abra o aplicativo com uma conta administradora e aceite a solicitação do UAC.

## Compilar

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

O executável será publicado em `bin/Release/net10.0-windows/win-x64/publish/Aqua Control.exe`.

O software oficial do watercooler deve permanecer fechado enquanto o Aqua Control usa a `COM3`.
