# Arquitetura

[English](../en/ARCHITECTURE.md) · **Português (Brasil)**

O OMSI Map Studio é um produto independente e não deve depender de código, serviços em execução ou versões do OMSI NavBR Multiplayer.

## MapStudio.Core

Responsável pela lógica de domínio relacionada ao OMSI:

- leitura de arquivos de configuração;
- detecção e preservação da codificação original dos arquivos;
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

Host Windows responsável por acesso nativo a arquivos e pastas, serviços do Core, ciclo de vida do WebView2 e comunicação entre C# e a interface.

O host desktop não deve se transformar na interface principal do editor.

### Ponte C# ↔ React

A interface envia comandos pequenos pelo WebView2, como `selectOmsiRoot`. O host executa somente operações que precisam de acesso nativo ao computador e devolve mensagens JSON com estado real.

No primeiro fluxo funcional:

1. React solicita a seleção da instalação do OMSI 2.
2. O host abre `OpenFolderDialog` nativo.
3. O host valida a presença da pasta `maps`.
4. `OmsiMapCatalog` lê os mapas reais.
5. O host envia nomes, caminhos e coordenadas reais de tiles para React.
6. O viewport usa essas coordenadas para desenhar a malha do mapa.

Erros do host são enviados por códigos estáveis. A interface é responsável por apresentar a mensagem apropriada ao usuário.

## MapStudio.UI

Interface principal do editor, responsável pelo viewport Babylon.js, biblioteca de recursos, inspetor de propriedades, ferramentas de construção, validações visuais e experiência de edição simplificada.

O estado de produção deve vir de dados reais fornecidos pelo Core/Desktop.

O grid exibido sem mapa é apenas referência visual do espaço de edição. Quando um mapa é carregado, a malha de tiles deve ser gerada a partir das coordenadas reais lidas do `global.cfg`.

## Estratégia de compatibilidade

Arquivos de configuração do OMSI são orientados por comandos de texto e mapas antigos podem utilizar codificações legadas.

O parser mantém o fluxo completo de linhas, detecta UTF-8/UTF-16 quando identificado e possui fallback para Windows-1252. A codificação original e a presença de BOM são mantidas no documento para permitir gravação segura.

Comandos desconhecidos continuam armazenados e devem sobreviver a um ciclo de leitura/gravação sem alterações.

## Primeiro fluxo vertical

1. Usuário seleciona a pasta raiz do OMSI 2.
2. O Core encontra mapas contendo `global.cfg`.
3. O `global.cfg` é lido sem alteração destrutiva.
4. Referências reais de tiles são extraídas.
5. O estado real é enviado à interface React.
6. A malha real de tiles é exibida no viewport.
7. Objetos, splines e terreno passam a ser renderizados progressivamente.

## Regra de documentação

Toda documentação oficial deve possuir versões equivalentes em `pt-BR` e `en`. Uma mudança documental só é considerada completa quando os dois idiomas forem atualizados.
