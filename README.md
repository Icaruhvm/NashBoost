# Nash Boost

Otimizador de Windows 10 e 11 para jogos. Reduz atraso de mouse e teclado, segura apps da Microsoft Store em segundo plano e tira gravação do Game Bar, sem limpar RAM e sem desligar antivírus, Windows Update ou a pesquisa.

## Instalar

Baixe o **NashBoost-Setup.exe** na aba [Releases](https://github.com/Icaruhvm/NashBoost/releases). Não precisa clonar este repositório.

O instalador baixa só o `NashBoost.exe`, coloca em `C:\Program Files\NashBoost` e cria atalhos no Menu Iniciar e na área de trabalho. O Windows pede administrador porque os ajustes gravam no sistema.

A conta fica só neste PC, em `%LOCALAPPDATA%\GamerEdition`. Remover o programa não apaga essa conta.

## Compilar

No Windows, a partir desta pasta:

```
set FW=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
set WPF=%FW%\WPF
%FW%\csc.exe /nologo /target:winexe /langversion:5 /win32icon:nash.ico /win32manifest:app.manifest /resource:logo.png,Nash.logo.png /out:NashBoost.exe /r:%FW%\System.dll /r:%FW%\System.Core.dll /r:%FW%\System.Xaml.dll /r:%FW%\System.Runtime.Serialization.dll /r:%FW%\System.Management.dll /r:%FW%\System.Security.dll /r:%WPF%\WindowsBase.dll /r:%WPF%\PresentationCore.dll /r:%WPF%\PresentationFramework.dll Accounts.cs Engine.cs Shell.cs
%FW%\csc.exe /nologo /target:winexe /langversion:5 /win32icon:nash.ico /win32manifest:setup.manifest /resource:logo.png,Nash.logo.png /out:NashBoost-Setup.exe /r:%FW%\System.dll /r:%FW%\System.Core.dll /r:%FW%\System.Xaml.dll /r:%WPF%\WindowsBase.dll /r:%WPF%\PresentationCore.dll /r:%WPF%\PresentationFramework.dll Setup.cs
```
