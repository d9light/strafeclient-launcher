# Strafe Client — Launcher

Launcher customizado de Minecraft feito em **C# / WinForms com WebView2**. Interface construída em HTML/CSS/JavaScript e renderizada dentro do app nativo.

## Funcionalidades

- 🎮 Lançamento de múltiplas instâncias de Minecraft
- 🔐 Autenticação própria via **Strafe Client API** (Supabase Auth)
- 🎨 Upload e visualização de **skins customizadas**
- 📦 Gerenciador de **Mods** integrado com a API do Modrinth
- ⚙️ Suporte a **Fabric, Forge e NeoForge**
- 🔗 Compatibilidade com servidores usando **Authlib-Injector**

## Requisitos

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

## Como compilar

```bash
# Clone o repositório
git clone https://github.com/seu-usuario/strafeclient-launcher.git
cd strafeclient-launcher

# Compile e rode
dotnet run
```

## Estrutura

```
StrafeClient/
├── LauncherForm.cs          # Janela principal e lógica central
├── AccountManager.cs        # Gerenciamento de contas
├── InstanceManager.cs       # Criação e gerenciamento de instâncias
├── ModloaderInstaller.cs    # Instalação de Fabric/Forge/NeoForge
├── ModpackInstaller.cs      # Instalação de modpacks
├── ModrinthAPI.cs           # Integração com a API do Modrinth
├── MicrosoftAuthHelper.cs   # Autenticação Microsoft (opcional)
└── wwwroot/                 # Interface web (HTML/CSS/JS)
    ├── index.html
    ├── style.css
    └── app.js
```

## API Backend

Este launcher se comunica com a [Strafe Client API](https://github.com/seu-usuario/brlaucher-api) hospedada na Vercel.

## Tecnologias

- [C# / .NET 8](https://dotnet.microsoft.com/)
- [WinForms + WebView2](https://docs.microsoft.com/en-us/microsoft-edge/webview2/)
- HTML5 / CSS3 / JavaScript (Vanilla)
