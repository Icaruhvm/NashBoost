# Nash Boost

Versão de testes. O aplicativo ainda não está pronto. O que tem aqui pode mudar, e algum ajuste pode não ficar bom no seu PC. Se for testar, cria o ponto de restauração antes.

Programa de Windows 10 e 11 para quem joga. Muda mouse, teclado, Game Bar e app da Store que fica acordado sem você abrir. A tela de login está desligada nesta versão de testes. O programa abre direto.

Não mexe em antivírus, Windows Update nem na pesquisa. A limpeza de RAM fica na aba Limpeza, fora do 1 clique.

## Instalar

Baixa o NashBoost-Setup.exe em [Releases](https://github.com/Icaruhvm/NashBoost/releases). Não precisa baixar este repositório.

O instalador pega só o programa, coloca em `C:\Program Files\NashBoost` e cria atalho na área de trabalho e no Menu Iniciar. O Windows pede administrador porque os ajustes gravam no sistema.

A conta fica em `%LOCALAPPDATA%\GamerEdition`. Tirar o programa não apaga essa pasta.

Na primeira abertura nada vem marcado. O botão de 1 clique aplica o pacote recomendado. A GPU acelerada não entra nele: em umas placas ajuda, em outras engasga, então fica na aba Jogo para quem quiser testar. Nas outras abas, marca o que quiser e clica em aplicar. Na aba Aplicativos, "Marcar instalados" seleciona a lista de uma vez. "Tirar papel de parede" só tira Wallpaper Engine, Lively ou parecido da inicialização. O resto da lista a pessoa desliga um por um.

O programa não mexe em monitor, taxa de atualização nem na disposição das telas. Não para serviço de um programa específico, tipo banco de dados, e não fecha Spotify, launcher, Discord nem Steam. Na aba Limpeza, "Preparar para jogar" fecha papel de parede animado, se estiver aberto, e libera memória. Não entra no 1 clique. O SysMain só desliga no Windows 10, e só se a pessoa marcar.

## Compilar

Nesta pasta, no Windows:

```
set FW=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
set WPF=%FW%\WPF
%FW%\csc.exe /nologo /target:winexe /langversion:5 /win32icon:nash.ico /win32manifest:app.manifest /resource:logo.png,Nash.logo.png /out:NashBoost.exe /r:%FW%\System.dll /r:%FW%\System.Core.dll /r:%FW%\System.Xaml.dll /r:%FW%\System.Runtime.Serialization.dll /r:%FW%\System.Management.dll /r:%FW%\System.Security.dll /r:%WPF%\WindowsBase.dll /r:%WPF%\PresentationCore.dll /r:%WPF%\PresentationFramework.dll Accounts.cs Engine.cs Shell.cs
%FW%\csc.exe /nologo /target:winexe /langversion:5 /win32icon:nash.ico /win32manifest:setup.manifest /resource:logo.png,Nash.logo.png /out:NashBoost-Setup.exe /r:%FW%\System.dll /r:%FW%\System.Core.dll /r:%FW%\System.Xaml.dll /r:%WPF%\WindowsBase.dll /r:%WPF%\PresentationCore.dll /r:%WPF%\PresentationFramework.dll Setup.cs
```
