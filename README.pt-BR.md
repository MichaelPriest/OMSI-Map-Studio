# OMSI Map Studio

[English](README.en.md) · **Português (Brasil)**

O **OMSI Map Studio** é um editor moderno e independente de mapas para **OMSI 2**, focado em tornar a criação de mapas mais fácil, segura e visual.

> Este projeto é completamente independente do OMSI NavBR Multiplayer. Possui repositório, arquitetura, versões e ciclo de desenvolvimento próprios.

## Objetivos iniciais

- Detectar uma instalação real do OMSI 2 e seus mapas.
- Ler `global.cfg` preservando conteúdo ainda desconhecido.
- Extrair referências reais de tiles por meio das seções `[map]`.
- Fornecer um host desktop em .NET 10 + WPF + WebView2.
- Fornecer uma interface React + TypeScript + Babylon.js.
- Nunca substituir dados reais ausentes por dados fake/mock em produção.
- Tornar a construção de mapas simples para usuários que não conhecem os formatos internos do OMSI.

## Primeira versão de teste

A prerelease atual é **v0.1.0-alpha.8**. A gravação ainda é experimental, mas já inclui transformações de objetos existentes e inserção preservativa de novos objetos quando existe um template seguro do mesmo `.sco`, sempre com backup automático.

Ela permite selecionar uma instalação real do OMSI 2, abrir manualmente um mapa, trabalhar em Mapa completo ou modo 3×3, visualizar objetos O3D e splines reais, editar X/Y/Z/rotação/pitch/bank, desfazer/refazer prévias, inserir objetos pela Biblioteca e usar **Colocar cópia** no objeto selecionado. Consulte [Testes da Alpha](docs/pt-BR/TESTING.md) para limitações e roteiro de validação.

## Requisitos de desenvolvimento

- .NET 10 SDK
- Node.js 22+
- Windows para executar o host desktop

## Core

    dotnet build src/MapStudio.Core/MapStudio.Core.csproj
    dotnet test tests/MapStudio.Core.Tests/MapStudio.Core.Tests.csproj

## Interface

    cd src/MapStudio.UI
    npm install
    npm run dev

Para usar a interface empacotada no aplicativo desktop, execute `npm run build` antes de compilar o projeto desktop.

## Regra de compatibilidade

Entender um comando do OMSI é opcional; **preservá-lo não é**.

Se o editor abrir um comando que ainda não entende, esse conteúdo deve continuar existindo ao salvar o arquivo. Um documento que não foi alterado deve poder passar por leitura e gravação sem perda silenciosa de informações.

## Idiomas da documentação

Toda documentação oficial deve ser publicada em pelo menos:

- `pt-BR`
- `en`

Ao adicionar ou alterar documentação, as versões dos dois idiomas devem ser atualizadas no mesmo conjunto de mudanças.

Documentação de arquitetura:

- [Arquitetura — Português](docs/pt-BR/ARCHITECTURE.md)
- [Architecture — English](docs/en/ARCHITECTURE.md)


### Biblioteca de Splines

A Alpha.4 também varre `OMSI 2/Splines` sob demanda e permite pré-visualizar arquivos `.sli` instalados. A biblioteca oferece criação **Normal** e **Altura**. A persistência só é liberada quando o mapa contém um template real neutro do mesmo tipo: cinco extras explícitos zerados para `[spline]` e seis para `[spline_h]`. Header e extras são copiados desse template.


## Roadmap do projeto

A direção técnica e funcional oficial está documentada em:

- [Roadmap técnico e funcional — Português](docs/pt-BR/ROADMAP.md)
- [Technical and functional roadmap — English](docs/en/ROADMAP.md)

O roadmap define a stack mantida, Asset Index/cache, streaming por tiles, paridade com o editor OMSI, Traffic Rules, paths, tracks/trips, timetables, sinais, ferrovia, geodados e critérios para considerar o Map Studio um substituto funcional do editor original.
