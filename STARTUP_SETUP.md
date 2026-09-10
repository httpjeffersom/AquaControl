# 🚀 Configuração - Inicializar com Windows

## ✅ O que foi feito

1. **app.manifest** foi alterado para `requireAdministrator`
   - Agora o app pede permissão de admin automaticamente ao iniciar
   - Sem isso, a RegistryKey não podia ser escrita

2. **WatercoolerTemp.csproj** foi atualizado
   - Garante que todas as dependências (LibreHardwareMonitorLib, System.IO.Ports) sejam incluídas no build

3. **Código já estava pronto**
   - `StartupManager.cs` - Gerencia a RegistryKey automaticamente
   - `MainWindow.xaml` - CheckBox "Iniciar com o Windows" já na UI
   - `MainViewModel.cs` - Ligação ao StartupManager já implementada

---

## 📋 Como usar

### Para Desenvolvedores

**Compilar em Release:**

```powershell
cd d:\Programação\AquaControl
dotnet publish -c Release -r win-x64 --self-contained false
```

**Resultado:** Executável em `bin/Release/net10.0-windows/win-x64/publish/Aqua Control.exe`

### Para Usuários Finais

1. **Abra o app como Administrador**
   - Clique direito → "Executar como administrador"
   - O Windows pedirá confirmação

2. **Marque o CheckBox**
   - Na tela principal, marque: `☑ Iniciar com o Windows`

3. **Reinicie o PC**
   - Na próxima inicialização, o app iniciará automaticamente
   - Ficará minimizado na bandeja do sistema

---

## 🔍 Se não funcionar

### Verificar RegistryKey

```powershell
# Abra PowerShell como admin
regedit
```

Navegue até:

```
HKEY_CURRENT_USER
  └─ Software
     └─ Microsoft
        └─ Windows
           └─ CurrentVersion
              └─ Run
```

Procure por `WatercoolerTemp` - deve estar lá com o caminho completo do executável.

### Se não aparecer

- Abra o app novamente como Admin
- Marque o CheckBox
- Feche e abra o Regedit novamente

### Se der erro

- Verifique se o app está rodando como Administrador
- Verifique se a COM3 está correta (Gerenciador de Dispositivos)
- Feche o software oficial do watercooler (usa a COM3 exclusivamente)

---

## 📝 Detalhes Técnicos

**Registry Entry:**

```
Chave: HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
Nome: WatercoolerTemp
Valor: "C:\Caminho\Aqua Control.exe" --startup
```

**Detecção no código:**

- `StartupManager.IsStartupLaunch(args)` detecta o flag `--startup`
- Se detectado, o app inicia minimizado na bandeja (`HideToTray()`)
- App fica rodando em background monitorando a temperatura

---

## 🛠️ Solução de Problemas

| Problema                                | Solução                                                  |
| --------------------------------------- | -------------------------------------------------------- |
| "Sem permissão ao escrever no Registry" | Rode como Admin (direito clique → Executar como admin)   |
| CheckBox não responde                   | Verifique se está rodando como Admin                     |
| App não inicia com Windows              | Reinicie o PC e verifique se app foi marcado no CheckBox |
| Erro na COM3                            | Feche software do watercooler e tente novamente          |

---

**Status:** ✅ Pronto para produção
