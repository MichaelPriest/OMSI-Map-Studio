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

### Splines posicionadas

Para `[spline]` e `[spline_h]`, o Core interpreta de forma conservadora o bloco-base usado pelo mapa: caminho `.sli`, IDs de encadeamento, posição, rotação, comprimento, raio e gradientes inicial/final. Valores posteriores permanecem em `ExtraValues`.

Em mapas cartesianos, o viewport usa esses dados para desenhar somente o eixo real de cada spline. Segmentos retos usam comprimento e rotação; curvas usam o raio declarado; a altura do eixo interpola os gradientes inicial e final. Essa visualização não inventa largura, perfil ou textura da rua.

`OmsiSplineDefinitionReader` lê o `.sli` selecionado sob demanda. `[texture]` fornece os nomes das texturas e cada `[profile]` é associado a dois `[profilepnt]`, preservando largura, altura, coordenada horizontal de textura e fator de repetição longitudinal. O caminho passa por `OmsiSplinePathResolver` e precisa permanecer dentro de `OMSI 2/Splines`.

Quando uma spline é selecionada, o viewport extruda as superfícies reconhecidas desse perfil ao longo de seu comprimento, raio e gradientes reais. As demais splines continuam como eixos leves. Texturas de imagem ainda não são carregadas nesta alpha; o perfil usa material neutro para não simular aparência inexistente.

Mapas com `[worldcoordinates]` ainda não exibem os eixos globalmente até existir a conversão geográfica correta.

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

Selecionar a pasta raiz do OMSI não faz mais descoberta automática de mapas. A raiz serve apenas como base segura para `maps`, `Sceneryobjects`, `Splines`, texturas e demais recursos.

O usuário abre explicitamente um mapa pelo botão **Abrir mapa**. O host aceita somente uma pasta dentro de `OMSI 2/maps` que contenha `global.cfg`. Apenas esse `global.cfg` é lido e, em seguida, os tiles do mapa escolhido são carregados sob demanda.

- `selectOmsiRoot` apenas valida e registra a raiz do OMSI;
- `selectMap` abre um seletor nativo e lê somente o `global.cfg` do mapa escolhido;
- `loadMapRegion` carrega somente uma janela de tiles ao redor do tile ativo;
- a janela padrão é 3×3 (raio 1), portanto mapas grandes não abrem todos os arquivos `.map` de uma vez;
- tiles já lidos são mantidos em cache pelo host durante a sessão do mapa;
- ao clicar em outro tile visível, ele se torna o centro da área ativa e os vizinhos necessários são carregados;
- os tiles da área ativa são processados com concorrência limitada para reduzir a espera sem saturar o disco;
- `loadSplineProfile` carrega o `.sli` apenas quando uma spline é selecionada e mantém a definição em cache;
- `loadSceneryObjectMetadata` carrega o `.sco` apenas quando um objeto é selecionado;
- `loadSceneryObjectGeometry` carrega somente os meshes `.o3d` não criptografados do objeto selecionado;
- metadados e geometria já lidos são armazenados em cache no React.

Isso elimina a varredura de todos os mapas ao escolher a instalação e também evita carregar todos os tiles de um mapa grande. Sem mapa aberto, o viewport permanece vazio; com um mapa aberto, apenas a área ativa recebe objetos, splines e demais dados pesados. Exceções do host continuam sendo convertidas em erro visível em vez de deixar a interface travada.

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

Objetos selecionados podem ser movidos e rotacionados em prévia. Quando disponível, a geometria O3D real é exibida usando os materiais embutidos no O3D: cor difusa, alpha, especular e emissão. Cada triângulo mantém seu índice de material. Somente transformações de objetos existentes possuem gravação nesta etapa; criação/exclusão e outros tipos de edição continuam bloqueados.

## Estratégia de compatibilidade

Arquivos de configuração do OMSI são orientados por comandos de texto e mapas antigos podem utilizar codificações legadas.

O parser mantém o fluxo completo de linhas, detecta UTF-8/UTF-16 quando identificado e possui fallback para Windows-1252. A codificação original e a presença de BOM são mantidas no documento para permitir gravação segura.

Comandos desconhecidos continuam armazenados e devem sobreviver a um ciclo de leitura/gravação sem alterações.

## Primeiro fluxo vertical

1. Usuário seleciona a pasta raiz do OMSI 2.
2. Nenhum mapa é carregado automaticamente.
3. Usuário clica em **Abrir mapa** e escolhe uma pasta dentro de `maps`.
4. O `global.cfg` desse mapa é lido sem alteração destrutiva.
5. Referências reais de tiles são extraídas.
6. Somente os arquivos `.map` desse mapa são inspecionados.
7. Cada tile é lido uma única vez para estatísticas, objetos e splines.
8. A malha real de tiles, os eixos reais das splines e as estatísticas do mapa são exibidos.
9. O bloco-base de objetos passa a ser interpretado de forma segura.
10. Objetos posicionados podem ser selecionados e inspecionados.
11. O `.sco` do objeto selecionado fornece metadados reais sob demanda.
12. O cabeçalho e as seções de meshes `.o3d` são validados e inventariados.
13. Vértices, normais, UVs e triângulos de meshes O3D não criptografados são carregados sob demanda.
14. O modelo real do objeto selecionado é exibido no viewport com material neutro.
15. Materiais O3D embutidos passam a ser aplicados por triângulo no preview.
16. Texturas O3D e extensões `[matl_*]` passam a ser carregadas progressivamente.
17. Splines podem ser selecionadas e inspecionadas diretamente no viewport.
18. O perfil real `.sli` da spline selecionada é carregado sob demanda e extrudado com material neutro.
19. Texturas de splines e terreno passam a ser implementados progressivamente.

## Regra de documentação

Toda documentação oficial deve possuir versões equivalentes em `pt-BR` e `en`. Uma mudança documental só é considerada completa quando os dois idiomas forem atualizados.


### Catálogo O3D do mapa completo

No modo **Mapa completo**, o React deriva a lista de caminhos `.sco` únicos a partir dos objetos posicionados recebidos do host. As geometrias são solicitadas sequencialmente pelo comando já existente `loadSceneryObjectGeometry` e armazenadas em cache por caminho.

O viewport agrupa colocações por `sceneryObjectPath`. Para cada modelo com geometria válida:

1. cria os meshes-base separados por material;
2. aplica o primeiro posicionamento como fonte;
3. reutiliza os mesmos buffers/materiais em clones para as demais colocações;
4. mantém marcadores apenas para modelos ainda não carregados ou formatos não suportados.

Isso permite exibir o mapa completo sem reler o mesmo O3D para cada instância.


## Escrita preservativa e backups

`OmsiTileObjectEditor` recebe um documento já parseado e uma lista de transformações de objetos. Cada objeto possui um `SourceSectionOrdinal` atribuído por `OmsiTileReader`, além de ID e caminho `.sco`.

Antes de alterar uma linha, o editor confirma os três identificadores. Isso protege contra alterações externas que mudem a estrutura do tile entre leitura e salvamento.

Somente seis linhas de dados do bloco `[object]` são substituídas:

- X;
- Y;
- Z;
- rotação;
- pitch;
- bank.

O restante do documento permanece byte-equivalente dentro das limitações das linhas modificadas, preservando encoding, BOM, newline, comentários, comandos desconhecidos e `ExtraValues`.

`SafeFileTransaction` prepara todo o lote antes de tocar nos arquivos de destino:

1. valida destinos únicos e existentes;
2. cria backups em `.mapstudio-backups/<timestamp>/`;
3. grava cada nova versão em arquivo temporário no mesmo diretório do tile;
4. substitui os destinos usando troca de arquivo;
5. em falha, tenta restaurar os arquivos já substituídos a partir dos backups;
6. mantém os backups mesmo quando ocorre erro.

O host nunca grava usando o conteúdo antigo do cache: no Save ele reabre o tile atual do disco, aplica a mutação mínima e só então executa a transação.


### Biblioteca de Sceneryobjects

O comando `loadSceneryLibrary` é disparado apenas sob demanda. O host percorre `Sceneryobjects` em uma tarefa de background usando `EnumerationOptions`, sem seguir reparse points e ignorando diretórios inacessíveis.

Somente caminhos relativos `Sceneryobjects\\...\\arquivo.sco` e nomes de arquivo são enviados ao React. Os caminhos descobertos também entram na whitelist de objetos conhecidos pelo host. O resultado fica em cache até a instalação OMSI ser alterada.


## Inserção preservativa de objetos

`OmsiTileObjectInserter` acrescenta um novo bloco `[object]` ao documento preservado sem reserializar as seções existentes.

O writer acrescenta:

- `[object]`;
- `HeaderValue` derivado de uma instância real do mesmo `.sco`;
- caminho `.sco`;
- ID global novo;
- X, Y, Z;
- rotação, pitch e bank;
- `ExtraValues` copiados do template real.

A linha textual opcional `Object Nr. ...` não é gerada, pois não pertence ao bloco funcional `[object]`.

### Alocação global de ID

Antes de inserir, o host lê os tiles atuais diretamente do disco. `OmsiTileElementIdScanner` procura IDs nos tipos conhecidos que participam da numeração do mapa:

- `[object]`;
- `[attachObj]`;
- `[splineAttachement]` / `[splineAttachment]`;
- `[splineAttachement_repeater]` / variante `Attachment`;
- `[spline]`;
- `[spline_h]`.

O novo ID é `maior ID encontrado + 1`. A leitura para conteúdo e análise de IDs usa o mesmo `OmsiConfigDocument` por tile para evitar parse duplicado.

Se não existir uma instância do mesmo `.sco` para servir como template, a gravação é recusada com `objectInsertTemplateUnavailable`.

A escrita final reutiliza `SafeFileTransaction`, portanto o tile recebe backup em `.mapstudio-backups/<timestamp>/` antes da troca atômica.


## Exclusão preservativa de objetos

`OmsiTileObjectDeleter` remove somente a linha `[object]` e as linhas de dados funcionais da instância selecionada.

Antes da remoção, o Core valida o ordinal da seção, o ID do objeto e o caminho `.sco`. Comentários e linhas em branco são preservados, inclusive quando o parser os associa ao corpo da seção; a seção seguinte nunca é removida.

O host relê o tile diretamente do disco antes da exclusão, usa `SafeFileTransaction`, cria backup em `.mapstudio-backups/<timestamp>/` e invalida o cache após sucesso. Se a identidade mudou desde a leitura da UI, a operação é recusada como conflito.


## Edição preservativa de splines existentes

Cada `[spline]` / `[spline_h]` recebe um `SourceSectionOrdinal` estável na ordem em que aparece no tile. `OmsiTileSplineEditor` pode alterar somente X, Z, Y, rotação, comprimento, raio e gradientes inicial/final.

Antes de gravar, o editor valida ordinal, tipo `spline`/ `spline_h`, caminho `.sli`, ID e vínculos `previous/next`. Esses vínculos não são editados nesta etapa. O host relê o tile atual, preserva extras/comentários/seções desconhecidas e usa o mesmo backup + troca atômica das transformações de objetos.


## Vínculos transacionais de splines

`OmsiSplineLinkPlanner` calcula a mudança de cadeia antes de qualquer gravação. A fonte mantém identidade por tile + ordinal + ID + caminho `.sli` + tipo + vínculos atuais.

Ao trocar `previous` ou `next`:

- a ponta recíproca do vizinho antigo é liberada;
- a ponta do vizinho novo só é usada se estiver livre ou já apontar para a fonte;
- IDs ausentes, duplicados, auto-referência e o mesmo vizinho nas duas pontas são recusados;
- inconsistência entre a fonte e seus vizinhos atuais cancela o lote.

`OmsiTileSplineLinkEditor` altera somente as duas linhas de vínculo. O host agrupa os edits por tile, relê apenas os tiles afetados e envia todos os arquivos para uma única `SafeFileTransaction`, que restaura os já substituídos se uma troca posterior falhar.


## Exclusão transacional de splines conectadas

A exclusão de spline reutiliza `OmsiSplineLinkPlanner` com alvo `previous = -1` / `next = -1`. Isso valida primeiro a reciprocidade dos vizinhos atuais.

Os vizinhos são editados antes da remoção da seção fonte. Quando vizinho e fonte ficam no mesmo tile, o host aplica os edits de vínculo em memória, reabre esses bytes com `OmsiConfigParser.ParseBytes` e só então remove a seção fonte. Todos os tiles afetados entram em uma única `SafeFileTransaction`.

Assim, apagar uma spline conectada libera as pontas recíprocas sem deixar IDs pendurados; conflito em qualquer tile cancela o lote inteiro.


## Biblioteca de Splines instalada

A Biblioteca de Splines varre `OMSI 2/Splines` somente sob demanda, ignora reparse points/diretórios inacessíveis e limita o índice a 50.000 arquivos `.sli`. Os caminhos encontrados passam a integrar `_knownSplinePaths`, permitindo carregar o perfil real.

Para criação persistente a partir de um `.sli` instalado, `OmsiSplinePlacementTemplateAnalyzer` exige um template real neutro do mesmo tipo no mapa. `[spline]` normal precisa ter exatamente cinco `ExtraValues` numéricos explícitos zerados; `[spline_h]` precisa de seis, incluindo o `delta_h`. Isso evita inventar cant, skew ou dados de altura.

O novo bloco usa o `HeaderValue` e os extras do template do tipo escolhido, troca o caminho para o `.sli` instalado, gera ID global novo e começa desconectado.


## Texturas reais sob demanda

Texturas não são pré-carregadas para o mapa inteiro. Quando um objeto, spline ou prévia de colocação precisa de uma textura, o React envia ao host o owner real, mesh (para O3D) e nome declarado.

`OmsiTextureAssetPathResolver` resolve somente extensões suportadas dentro de `Sceneryobjects` ou `Splines`, rejeitando caminhos absolutos/fora da raiz. O host limita cada textura a 16 MiB e devolve bytes Base64, extensão e MIME. O React mantém cache por owner/mesh/nome.

Babylon recebe a extensão forçada na criação de `Texture`; loaders DDS e TGA são registrados explicitamente. Nesta etapa, o carregamento é sob demanda para seleção e prévias, evitando transferir todas as texturas de um mapa grande.
