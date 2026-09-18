# Arquitetura

[English](../en/ARCHITECTURE.md) · **Português (Brasil)**

O OMSI Map Studio é um produto independente e não deve depender de código, serviços em execução ou versões do OMSI NavBR Multiplayer.

## MapStudio.Core

Responsável pela lógica de domínio relacionada ao OMSI:

- leitura de arquivos de configuração;
- descoberta de mapas;
- tiles;
- futuramente objetos de cenário;
- splines;
- terreno;
- paths;
- backups;
- validação.

O Core não deve depender de WPF, WebView2 ou React.

## MapStudio.Desktop

Host Windows responsável por:

- acesso nativo a arquivos e pastas;
- serviços do Core;
- ciclo de vida do WebView2;
- comunicação entre C# e a interface.

O host desktop não deve se transformar na interface principal do editor.

## MapStudio.UI

Interface principal do editor.

Responsabilidades:

- viewport Babylon.js;
- biblioteca de recursos;
- inspetor de propriedades;
- ferramentas de construção;
- validações visuais;
- experiência de edição simplificada.

O estado de produção deve vir de dados reais fornecidos pelo Core/Desktop.

## Estratégia de compatibilidade

Arquivos de configuração do OMSI são orientados por comandos de texto.

O parser mantém o fluxo completo de linhas original e acrescenta interpretações estruturadas sobre esse conteúdo. Comandos desconhecidos continuam armazenados no documento e devem sobreviver a um ciclo de leitura/gravação sem alterações.

## Primeiro fluxo vertical

1. Usuário seleciona a pasta raiz do OMSI 2.
2. O Core encontra mapas contendo `global.cfg`.
3. O `global.cfg` é lido sem alteração destrutiva.
4. Referências reais de tiles são extraídas.
5. O estado real é enviado à interface React.
6. Tiles, objetos, splines e terreno passam a ser renderizados progressivamente.

## Regra de documentação

Toda documentação oficial deve possuir versões equivalentes em `pt-BR` e `en`.

Uma mudança documental só é considerada completa quando os dois idiomas forem atualizados.
