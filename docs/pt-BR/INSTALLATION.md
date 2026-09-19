# Instalação Windows (.exe)

O OMSI Map Studio passa a manter duas formas de distribuição para Windows x64:

- **Instalador EXE**: formato recomendado para usuários comuns. Instala em `%LOCALAPPDATA%\Programs\OMSI Map Studio`, cria entrada no menu Iniciar, oferece atalho opcional na área de trabalho e inclui desinstalador.
- **ZIP portátil**: continua disponível para testes e uso sem instalação.

## Como o EXE é produzido

O aplicativo continua sendo publicado como `.NET 10` self-contained para `win-x64`. A pasta publicada contém o host WPF, WebView2 e a UI React compilada em `ui/`. O instalador Inno Setup empacota essa pasta inteira em um único arquivo `OMSI-Map-Studio-Setup-<versão>-win-x64.exe`.

Isso é intencional: o usuário baixa **um único instalador .exe**, enquanto a instalação preserva os arquivos internos que o WebView2 precisa em tempo de execução.

O instalador é por usuário por padrão e não exige privilégios de administrador. O código fonte do instalador está em `installer/MapStudio.iss`, e `installer/build-installer.ps1` reproduz o processo localmente ou no CI.

## Dependências

O pacote é self-contained para o runtime .NET. O Windows precisa ter o Microsoft Edge WebView2 Runtime disponível; essa dependência continuará sendo tratada separadamente e deverá receber uma verificação amigável no host antes da release estável.

## Regra de release

A versão EXE deve ser publicada junto com o ZIP portátil e com seu arquivo SHA-256. Releases de teste continuam como prerelease e só devem ser geradas após Core, React e Desktop passarem.


## Desinstalação

O instalador cria um desinstalador próprio do OMSI Map Studio. Ele fica registrado em **Aplicativos instalados** do Windows e também ganha um atalho **Desinstalar OMSI Map Studio** no menu Iniciar.

A desinstalação remove os arquivos instalados em `%LOCALAPPDATA%\Programs\OMSI Map Studio` e os atalhos criados pelo instalador. Arquivos de mapas do OMSI, backups `.mapstudio-backups` e outros arquivos externos ao diretório do aplicativo não são removidos.

Logs e dados locais fora da pasta de instalação não são apagados automaticamente nesta etapa, para evitar perda de diagnóstico ou preferências durante a fase alpha.
