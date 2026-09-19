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

A prerelease atual é **v0.1.0-alpha.3**. A gravação continua limitada: apenas transformações de objetos posicionados (mover/rotacionar) podem ser salvas com backup automático.

Ela permite selecionar uma instalação real do OMSI 2, abrir manualmente um mapa, trabalhar em Mapa completo ou modo 3×3, visualizar objetos O3D e splines reais, mover/rotacionar objetos em prévia e salvar essas transformações de forma preservativa com backup automático. Consulte [Testes da Alpha](docs/pt-BR/TESTING.md) para limitações e roteiro de validação.

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
