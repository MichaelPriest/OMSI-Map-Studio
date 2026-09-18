# Arquitetura

[English](../en/ARCHITECTURE.md) · **Português (Brasil)**

O OMSI Map Studio é um produto independente e não deve depender de código, serviços em execução ou versões do OMSI NavBR Multiplayer.

## MapStudio.Core

Responsável pela lógica de domínio relacionada ao OMSI:

- leitura de arquivos de configuração;
- detecção e preservação da codificação original dos arquivos;
- descoberta de mapas;
- leitura segura dos arquivos de tile `.map`;
- leitura do bloco-base de objetos posicionados;
- tiles;
- futuramente objetos de cenário completos;
- splines;
- terreno;
- paths;
- backups;
- validação.

O Core não deve depender de WPF, WebView2 ou React.

### Leitura de tiles

`OmsiTileReader` recebe o caminho real de um arquivo `.map` e usa o mesmo parser preservativo dos demais arquivos de configuração.

Nesta fase ele extrai apenas informações que podemos identificar com segurança pela seção:

- quantidade de `[object]`;
- quantidade de `[spline]`;
- quantidade de `[splineAttachement]` / `[splineAttachment]`;
- existência ou ausência do arquivo do tile.

### Sistema de coordenadas

`OmsiMapCatalog` detecta a presença de `[worldcoordinates]` no `global.cfg`.

- sem `[worldcoordinates]`: a malha cartesiana usa tiles de 300 m;
- com `[worldcoordinates]`: o viewport inicial mostra apenas a topologia dos tiles de forma esquemática até existir conversão geográfica própria.

O editor não deve aplicar uma escala cartesiana de 300 m a mapas de coordenadas mundiais.

### Objetos posicionados

Para `[object]`, o Core interpreta somente o bloco-base confirmado:

1. valor de cabeçalho ainda sem semântica atribuída;
2. caminho do arquivo `.sco`;
3. ID do objeto;
4. posição `x`;
5. posição `y`;
6. posição `z`;
7. rotação;
8. pitch;
9. bank.

Valores posteriores são mantidos em `ExtraValues` e não recebem significado até existirem modelos e testes específicos. Se o bloco-base estiver incompleto ou inválido, o objeto é ignorado pela visão estruturada, mas o texto original continua preservado no documento.

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
5. Cada referência de tile é inspecionada por `OmsiTileReader`.
6. O host envia nomes, caminhos, coordenadas e contagens reais para React.
7. O viewport usa essas coordenadas para desenhar a malha do mapa.

Erros do host são enviados por códigos estáveis. A interface é responsável por apresentar a mensagem apropriada ao usuário.

## MapStudio.UI

Interface principal do editor, responsável pelo viewport Babylon.js, biblioteca de recursos, inspetor de propriedades, ferramentas de construção, validações visuais e experiência de edição simplificada.

O estado de produção deve vir de dados reais fornecidos pelo Core/Desktop.

O grid exibido sem mapa é apenas referência visual do espaço de edição. Quando um mapa é carregado, a malha de tiles deve ser gerada a partir das coordenadas reais lidas do `global.cfg`. Tiles referenciados cujo arquivo `.map` não existe são destacados separadamente.

## Estratégia de compatibilidade

Arquivos de configuração do OMSI são orientados por comandos de texto e mapas antigos podem utilizar codificações legadas.

O parser mantém o fluxo completo de linhas, detecta UTF-8/UTF-16 quando identificado e possui fallback para Windows-1252. A codificação original e a presença de BOM são mantidas no documento para permitir gravação segura.

Comandos desconhecidos continuam armazenados e devem sobreviver a um ciclo de leitura/gravação sem alterações.

## Primeiro fluxo vertical

1. Usuário seleciona a pasta raiz do OMSI 2.
2. O Core encontra mapas contendo `global.cfg`.
3. O `global.cfg` é lido sem alteração destrutiva.
4. Referências reais de tiles são extraídas.
5. Cada arquivo `.map` existente é inspecionado.
6. O estado real é enviado à interface React.
7. A malha real de tiles e estatísticas de objetos/splines são exibidas.
8. O bloco-base de objetos passa a ser interpretado de forma segura.
9. Objetos, splines e terreno passam a ser renderizados progressivamente.

## Regra de documentação

Toda documentação oficial deve possuir versões equivalentes em `pt-BR` e `en`. Uma mudança documental só é considerada completa quando os dois idiomas forem atualizados.
