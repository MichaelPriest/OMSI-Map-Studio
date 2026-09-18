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
- leitura conservadora de metadados `.sco`;
- tiles;
- geometria real `.o3d` não criptografada para preview;
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

As referências de arquivos de tile passam por `OmsiMapPathResolver`. Depois de normalizado, o caminho deve continuar dentro da pasta do mapa. Referências com travessia de diretório, como `..\\`, são rejeitadas e tratadas como tile indisponível.

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

### Metadados de objetos de cenário

`OmsiSceneryObjectReader` usa o mesmo parser preservativo para arquivos `.sco` e expõe apenas metadados claramente identificáveis:

- `[friendlyname]`;
- hierarquia declarada em `[groups]`;
- referências declaradas por `[mesh]`;
- referências declaradas por `[collision_mesh]`.

A ordem dos blocos e todos os comandos ainda não interpretados continuam preservados no documento de origem. Nesta etapa o editor não interpreta materiais, scripts ou animações. Para arquivos `.o3d`, o Core valida o cabeçalho, inventaria as seções e pode decodificar posição, normal e UV dos vértices e índices dos triângulos quando o arquivo não está criptografado. Os dados são convertidos do sistema Z-up do OMSI para o sistema Y-up usado pelo viewport. Arquivos criptografados, `.x` e modelos acima dos limites de segurança permanecem sem preview geométrico.

`OmsiSceneryObjectPathResolver` restringe a resolução de `.sco` à pasta `Sceneryobjects` da instalação selecionada e rejeita travessia de diretório ou extensões diferentes.

`OmsiSceneryMeshPathResolver` interpreta referências `[mesh]` e `[collision_mesh]` a partir da pasta `model` associada ao `.sco`. São aceitos arquivos `.o3d` e `.x`, inclusive referências relativas entre pacotes com `..\`, desde que o caminho final continue dentro de `Sceneryobjects`. O React recebe apenas o caminho declarado e o estado encontrado/ausente; o caminho absoluto do computador não é exposto. Para meshes `.o3d` encontrados, o host também envia metadados seguros do cabeçalho, como versão e criptografia, além das contagens estruturais de vértices, triângulos, materiais e bones.

## MapStudio.Desktop

Host Windows responsável por acesso nativo a arquivos e pastas, serviços do Core, ciclo de vida do WebView2 e comunicação entre C# e a interface.

O host desktop não deve se transformar na interface principal do editor.

### Ponte C# ↔ React

A interface envia comandos pequenos pelo WebView2. O host executa somente operações que precisam de acesso nativo ao computador e devolve mensagens JSON com estado real.

O React não pode fornecer caminhos arbitrários para leitura. O host mantém uma lista dos caminhos `.sco` encontrados nos objetos dos mapas já carregados; somente esses caminhos podem solicitar metadados.

### Carregamento sob demanda

A descoberta inicial lê somente os `global.cfg` e as referências de tiles. Ela não abre todos os arquivos `.map` da instalação antes de liberar a interface.

- `loadMapContent` carrega o mapa selecionado sob demanda e, em uma única leitura de cada tile, obtém contagens e objetos;
- os tiles do mapa selecionado são processados com concorrência limitada para reduzir a espera sem saturar o disco;
- `loadSceneryObjectMetadata` carrega o `.sco` apenas quando um objeto é selecionado;
- `loadSceneryObjectGeometry` carrega somente os meshes `.o3d` não criptografados do objeto selecionado;
- metadados e geometria já lidos são armazenados em cache no React.

Isso evita ler todos os `.map` e `.sco` da instalação durante a abertura do programa. Ao voltar para um mapa já carregado, o React reutiliza o conteúdo em cache.

Para mapas cartesianos, o viewport representa a posição com:

- `worldX = tileX * 300 + objectX`;
- `worldZ = tileY * 300 + objectY`;
- `worldY = objectZ`.

Para mapas com `[worldcoordinates]`, os objetos são lidos e contabilizados, mas os marcadores globais ficam ocultos até existir a conversão geográfica correta.

## MapStudio.UI

Interface principal do editor, responsável pelo viewport Babylon.js, biblioteca de recursos, inspetor de propriedades, ferramentas de construção, validações visuais e experiência de edição simplificada.

O estado de produção deve vir de dados reais fornecidos pelo Core/Desktop.

### Seleção de objetos no viewport

Em mapas cartesianos, um clique curto no viewport calcula o objeto posicionado mais próximo do raio da câmera sem criar um mesh individual por objeto.

O inspetor exibe dados reais do `.map` e, quando disponível, metadados reais do `.sco`: nome amigável, grupos, meshes e collision meshes. Cada referência de mesh mostra também se o arquivo correspondente foi encontrado na instalação.

Arrastar a câmera não é tratado como seleção. Clicar em uma área sem objeto limpa a seleção.

Nesta fase a seleção é somente leitura. Quando disponível, a geometria O3D real do objeto selecionado é exibida com material neutro, sem texturas. O editor ainda não altera nem grava transformações.

## Estratégia de compatibilidade

Arquivos de configuração do OMSI são orientados por comandos de texto e mapas antigos podem utilizar codificações legadas.

O parser mantém o fluxo completo de linhas, detecta UTF-8/UTF-16 quando identificado e possui fallback para Windows-1252. A codificação original e a presença de BOM são mantidas no documento para permitir gravação segura.

Comandos desconhecidos continuam armazenados e devem sobreviver a um ciclo de leitura/gravação sem alterações.

## Primeiro fluxo vertical

1. Usuário seleciona a pasta raiz do OMSI 2.
2. O Core encontra mapas contendo `global.cfg`.
3. O `global.cfg` é lido sem alteração destrutiva.
4. Referências reais de tiles são extraídas.
5. A interface recebe imediatamente o catálogo leve da instalação.
6. Somente o mapa selecionado tem seus arquivos `.map` inspecionados.
7. Cada tile selecionado é lido uma única vez para estatísticas e objetos.
8. A malha real de tiles e estatísticas de objetos/splines são exibidas.
9. O bloco-base de objetos passa a ser interpretado de forma segura.
10. Objetos posicionados podem ser selecionados e inspecionados.
11. O `.sco` do objeto selecionado fornece metadados reais sob demanda.
12. O cabeçalho e as seções de meshes `.o3d` são validados e inventariados.
13. Vértices, normais, UVs e triângulos de meshes O3D não criptografados são carregados sob demanda.
14. O modelo real do objeto selecionado é exibido no viewport com material neutro.
15. Splines, terreno, materiais e texturas passam a ser implementados progressivamente.

## Regra de documentação

Toda documentação oficial deve possuir versões equivalentes em `pt-BR` e `en`. Uma mudança documental só é considerada completa quando os dois idiomas forem atualizados.
