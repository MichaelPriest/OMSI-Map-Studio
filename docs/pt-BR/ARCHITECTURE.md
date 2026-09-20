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


## Prefetch visual limitado

Além do carregamento prioritário da seleção/prévia, a UI mantém uma janela de recursos próximos ao tile ativo. Até 20 caminhos de objeto e 12 caminhos de spline são considerados para prefetch.

Perfis `.sli` próximos são carregados um por vez. As texturas usam orçamento separado de 16 assets automáticos de objetos + 8 de splines (24 no total), em pequenos lotes de 4 + 2 por ciclo. O cache e `requestedTextureKeys` impedem pedidos duplicados.

A seleção e a prévia não consomem esse orçamento automático: continuam tendo prioridade e podem solicitar seus próprios assets.


## Overrides estáticos de material do SCO

`OmsiSceneryObjectReader` agora mantém a ordem dos `[mesh]` e associa `[matl]` ao mesh anterior por ordinal. O override estático guarda nome da textura, índice do material, `[matl_alpha]`, `[matl_noZwrite]` e `[matl_noZcheck]`.

`[matl_change]` encerra o contexto estático e não é aplicado como material fixo, porque depende de variável/script em runtime. No React, o override só é aceito quando índice e nome-base da textura correspondem ao material O3D.

Mapeamento atual: alpha 0 = opaco; alpha 1 = alpha-test/cutout; alpha 2 = alpha-blend; noZwrite desativa escrita de profundidade; noZcheck usa teste de profundidade ALWAYS.


## Bumpmap estático do SCO

`[matl_bumpmap]` é lido somente no contexto de um `[matl]` estático. Nome de textura e fator numérico são transportados com o mesmo override associado por mesh ordinal + material ID + textura.

O arquivo de bump usa o mesmo `OmsiTextureAssetPathResolver`, portanto continua restrito a `Sceneryobjects`, formatos suportados e limite de 16 MiB. No Babylon ele é aplicado em `StandardMaterial.bumpTexture`, com `level` igual ao fator explícito quando disponível.


## Preview manual de nightmap

`[matl_nightmap]` é armazenado somente para `[matl]` estático. O asset não é carregado automaticamente no modo diurno.

Quando **Nightmap** é ativado na UI, o resolver seguro carrega o arquivo e o Babylon o aplica como `emissiveTexture`. É uma inspeção manual, não uma simulação de horário, `NightMapMode` ou scripts do OMSI.


## Envmap estático

`[matl_envmap]` fornece um arquivo real e uma intensidade de reflexão. O Core guarda ambos no override estático; o asset usa o mesmo resolver seguro de `Sceneryobjects`.

No Babylon a imagem é aplicada como `reflectionTexture` em modo esférico. A intensidade é limitada a 0–1 no preview, conforme a faixa documentada do OMSI.


## Comandos de material dependentes de runtime

`[matl_transmap]` e `[matl_lightmap]` são detectados no contexto de `[matl]` estático, mas não são executados pelo preview nesta etapa.

`matl_transmap` pode apontar para fontes dinâmicas como `\S:` e `matl_lightmap` é controlado por estado/script do OMSI. O Core preserva a referência para inspeção, sem convertê-la em um estado visual inventado.


## Diagnóstico de comandos de material ainda não simulados

O Core registra por material comandos conhecidos que ainda não têm reprodução visual suficientemente fiel. Nesta etapa: `[matl_envmap_mask]`, `[alphascale]` e `[matl_allcolor]`.

A informação percorre Core → host → bridge → React e aparece no inspetor. Nenhum desses comandos altera o material até existir uma implementação segura.


## Cache LRU de texturas

`requestedTextureKeys` agora representa somente requisições em andamento. Quando o host responde — sucesso ou falha — a chave sai desse conjunto.

O cache React mantém no máximo 64 assets usando ordem LRU. Ao entrar o 65º asset, o mais antigo é removido; se voltar a ser necessário, pode ser solicitado novamente porque não fica marcado permanentemente como `requested`.

O orçamento de prefetch automático (24 chaves únicas) continua separado do limite de cache.


## LOD de objetos SCO

Cada `[mesh]` preserva o limiar `[LOD]` vigente no arquivo `.sco`. Meshes declarados antes de qualquer `[LOD]` são globais e permanecem ativos.

O viewport escolhe o maior limiar menor ou igual à fração projetada do objeto na altura da tela, calculada com raio geométrico, distância à câmera e FOV vertical. A atualização ocorre a cada 6 frames para limitar custo.


## Superfícies de splines próximas

O viewport reutiliza os perfis `.sli` reais já presentes em `splineProfilesByPath` para extrudar superfícies das splines próximas ao tile ativo.

A janela visual é 3×3 tiles (raio 1) e existe limite de 120 splines extrudadas por cena. A spline selecionada é excluída desse lote porque já possui preview próprio. Splines sem perfil carregado continuam exibidas pelo eixo.


## Diagnóstico de terreno por tile

`OmsiTileReader.ReadContent` detecta o marcador `[terrain]`. `ReadContentAsync(tilePath)` também verifica o sidecar real `<tile>.map.terrain` e registra existência + tamanho em bytes.

Esses dados viajam no mesmo `OmsiTileSummary` usado pelo carregamento completo e pelo modo 3×3, sem decodificar nem modificar o binário.


## Compatibilidade O3D — contagem de bones

Nos O3D com cabeçalho estendido, as contagens de vértices e triângulos podem usar 32 bits. A lista de bones é uma exceção: a quantidade de bones continua sendo armazenada em 16 bits. O reader dedicado agora trata explicitamente essa diferença, evitando desalinhamento do stream e falsos `invalidBoneSection` em modelos que possuem bones.

O carregamento visual também distingue “resposta recebida” de geometria realmente renderizável. Um caminho O3D só é considerado renderizável quando pelo menos uma malha possui posições e índices válidos.


## Pipeline de carregamento do mapa otimizado

O aquecimento visual deixou de carregar uma geometria O3D e um perfil SLI por vez. O React agora dispara lotes de até 8 geometrias O3D e 8 perfis SLI em paralelo, enquanto o host limita a leitura pesada de O3D ao número seguro de CPUs disponíveis.

Geometrias de scenery são mantidas em cache por caminho absoluto enquanto a raiz OMSI permanece a mesma. Texturas também são armazenadas em cache por arquivo físico: referências repetidas em vários materiais/objetos não provocam nova leitura, nova conversão nem novo Base64 do mesmo arquivo.

BMPs usados pelo OMSI seguem diretamente como RGBA para a GPU. O host não gera mais um PNG redundante para o mesmo BMP, reduzindo CPU, memória e tráfego de mensagens WebView2.

O modo desempenho 3×3 não possui mais limite artificial de 64 paths de objetos ou 48 paths de splines. Todos os assets realmente usados pelos tiles carregados entram na fila.


## Carregamento estrutural prioritário e caches físicos

A abertura do mapa separa agora duas fases: **estrutura necessária para editar** e **acabamento visual em segundo plano**. A interface continua carregando O3D, perfis SLI e texturas reais, mas o prefetch automático de texturas não compete com a leitura estrutural inicial.

O host mantém, durante a sessão da mesma instalação do OMSI, caches independentes para:

- metadados `.sco` já parseados;
- payload de geometria por `.sco`;
- geometria por arquivo físico `.o3d`;
- perfis `.sli`;
- assets de textura.

O cache por mesh físico evita reabrir e reprocessar o mesmo O3D quando arquivos `.sco` diferentes apontam para ele. A entrada usa `Lazy<T>` thread-safe para garantir uma única materialização mesmo quando vários objetos chegam em paralelo.

O preloading de geometria e SLI usa lotes limitados, enquanto o prefetch amplo de texturas só começa depois que os caminhos estruturais do recorte atual (ou do mapa completo) receberam resposta. Terreno/base continuam prioritários e não dependem desse prefetch.

A edição deixa de permanecer bloqueada apenas porque texturas automáticas ainda estão chegando. O bloqueio continua existindo durante seleção da instalação/mapa, leitura dos tiles, troca de região e varreduras explícitas de biblioteca.


## Diagnóstico O3D protegido e mesh DirectX legado

O código `encrypted` é reservado para O3D de cabeçalho estendido cuja chave de criptografia não é `0xFFFFFFFF`. O editor não tenta interpretar os bytes protegidos como geometria comum e não inventa uma malha substituta.

Referências `[mesh]` terminadas em `.x` passam a ser classificadas como `legacyDirectXMesh`, em vez do genérico `unsupportedFormat`. Isso permite separar objetos que dependem do formato DirectX legado de formatos realmente desconhecidos.

Também foi alinhado o leitor estrutural ao leitor de geometria: a contagem da lista de bones da seção `0x54` permanece `UInt16` mesmo em O3D com cabeçalho estendido. Assim, um O3D longo válido com bones não é mais deslocado incorretamente pelo diagnóstico estrutural.


## Mesh DirectX `.x` texto

O host possui agora um leitor próprio para o formato legado DirectX `.x` em codificação **texto** (`xof ... txt ...`). Ele alimenta o mesmo payload de geometria real usado pelo viewport para O3D, portanto texturas e overrides estáticos continuam passando pelo pipeline normal do editor.

A primeira implementação cobre:

- blocos `Mesh` com vértices e faces poligonais;
- triangulação em leque para faces com quatro ou mais vértices;
- `MeshTextureCoords`;
- `MeshMaterialList`, materiais inline/referenciados já conhecidos e `TextureFilename`;
- `Frame` e `FrameTransformMatrix`;
- múltiplos meshes no mesmo arquivo;
- normais calculadas a partir da geometria final;
- limites de arquivo, vértices, faces, triângulos e materiais.

Arquivos `.x` binários ou comprimidos (`bin`, `tzip`, `bzip`) não são interpretados como texto e retornam `legacyDirectXUnsupportedEncoding`. O editor não cria geometria falsa quando a variante não é suportada.


## Transformações locais por mesh SCO

O metadata real do `.sco` agora preserva uma transformação local por entrada `[mesh]`: `[new_pos]`, `[rot_x]/[rotx]`, `[rot_y]/[roty]`, `[rot_z]/[rotz]` e `[scale]` (uniforme ou por eixo).

A transformação fica fora da geometria física cacheada. Assim, dois `.sco` podem reutilizar o mesmo `.o3d`/`.x` com posição, rotação ou escala local diferentes sem duplicar o parsing. O host envia a transformação junto ao mesh e o viewport a aplica antes de anexá-lo ao root do objeto.

O mapeamento segue o mesmo sistema OMSI → Babylon já usado no editor: posição X/Z/Y, escala X/Z/Y e rotações convertidas para yaw/pitch/roll com a mudança de handedness existente.


## Máscaras DDS fora do caminho crítico

A leitura inicial dos tiles não percorre mais todos os pixels de cada máscara A8 `texture/map/<tile>.map.N.dds`. Na abertura do mapa, o Core valida somente o cabeçalho DDS, dimensões, formato A8 e comprimento mínimo do arquivo.

As estatísticas de pixels (`coverage`, alpha mínimo e máximo) passam a ser calculadas somente quando o asset da máscara é realmente solicitado para renderização. Esses valores são enviados junto do `textureAssetLoaded` e continuam disponíveis no diagnóstico da interface.

Isso preserva a validação real e a pintura de terreno, mas remove leituras integrais de DDS que antes eram feitas para todas as máscaras de todos os tiles antes do mapa ficar utilizável.


## Parse fora da UI thread e paralelismo de tiles

O caminho de abertura do mapa foi movido para workers dedicados. O cache de conteúdo de tile agora inicia `OmsiTileReader.ReadContentAsync` dentro de `Task.Run`, evitando que parsing de `.map`, terrain e sidecars volte ao dispatcher WPF depois do I/O.

O mesmo princípio foi aplicado ao parse de metadata `.sco` e perfis `.sli`. A UI só recebe o resultado pronto para publicar no WebView.

A leitura de mapa completo e região permite até 8 tiles simultâneos, independentemente de o runtime expor poucas CPUs. O parse O3D continua limitado, mas garante pelo menos 4 workers e respeita o teto de 12.

Dentro de cada tile, a leitura assíncrona do `.terrain` é iniciada antes do diagnóstico `.rdy` e da descoberta de máscaras, permitindo sobrepor I/O e trabalho de metadata.


## Correção de O3D long-header e feedback de aquecimento visual

A seção de bones `0x54` volta a respeitar a largura do cabeçalho O3D: arquivos com cabeçalho estendido usam contagem de bones em `UInt32`, enquanto arquivos antigos usam `UInt16`. A contagem interna de pesos de cada bone continua em `UInt16`.

O aquecimento de malhas, perfis SLI e texturas volta a exibir uma animação contínua após a leitura estrutural. Nessa fase visual, a animação é informativa e não bloqueia a interação com o editor.

No modo desempenho 3×3, o inspetor passa a mostrar malhas realmente renderizáveis, falhas e pendências da área ativa, em vez de tratar apenas a presença de um payload como sucesso visual.


## O3D protegido no editor próprio

O OMSI Map Studio é um editor independente e não depende de arquivos-fonte alternativos para substituir assets protegidos. Um `.o3d` marcado como protegido permanece explicitamente identificado como tal até existir um caminho de compatibilidade real com o runtime instalado do OMSI.

A interface separa malhas realmente renderizáveis de assets protegidos. Marcadores de O3D protegido usam cor distinta dos marcadores de asset ausente/inválido, e o status informa quantos tipos de objeto e quantas malhas continuam protegidos.


## Compatibilidade nativa com vértices O3D protegidos

O leitor O3D passa a tratar o cabeçalho estendido protegido dentro do próprio Core. A transformação de vértices é aplicada em memória durante a leitura, antes da conversão de eixos para o viewport. O arquivo original nunca é regravado.

A implementação mantém limites defensivos. Malhas protegidas com domínio de vértices ainda não validado retornam `protectedVertexCountUnsupported` em vez de produzir geometria aproximada.


## Carregamento completo de texturas no modo mapa completo

O modo **Mapa completo** deixa de usar o teto de texturas pensado para o modo 3×3. O prefetch de objetos passa a aceitar até 1024 texturas e o de splines até 256, em lotes progressivos, com cache de até 1536 assets. O modo desempenho mantém os limites menores para preservar responsividade.

O Inspetor e a barra de estado passam a separar texturas carregadas, falhas de resolução/leitura e requisições pendentes. Isso evita considerar uma geometria O3D corretamente aberta como visualmente concluída quando sua textura ainda não foi carregada.


## Resolução de texturas compatível com conteúdo OMSI

O resolvedor de texturas de objetos e splines passa a reproduzir dois comportamentos comuns do ecossistema OMSI que antes geravam `textureNotFound` falsos:

- quando o arquivo declarado é BMP/TGA/PNG etc., um DDS de mesmo nome-base é aceito como substituto instalado;
- diretórios `Texture` compartilhados em níveis-pai do pacote são pesquisados progressivamente.

A busca continua confinada à raiz permitida de `Sceneryobjects` ou `Splines`; caminhos que escapem dessa raiz permanecem rejeitados.


## Objetos [tree] e meshes auxiliares

Quando um SCO possui `[tree]`, a visualização do mapa usa o billboard real definido pelo bloco de árvore e não renderiza, simultaneamente, o mesh auxiliar do mesmo SCO. Isso evita que helpers como `treehelper.x` apareçam como painéis cinza gigantes sobre o mapa.

A geometria do helper continua preservada no metadata/importador; apenas a composição visual padrão do mapa deixa de sobrepor helper + árvore.


## Posicionamento vertical de objetos conforme o OMSI

Objetos comuns de mapa usam altura relativa ao terreno: a coordenada Z gravada no bloco `[object]` é somada à altura interpolada do terrain no ponto X/Y. Objetos cujo SCO contém `[absheight]` permanecem em altura absoluta e não recebem esse deslocamento.

O mesmo cálculo é usado na renderização, seleção, foco da câmera, hit-test e gizmo de edição. Ao salvar um objeto relativo, o editor converte novamente a altura visual para o Z relativo do arquivo para não corromper a posição OMSI.


## Eixos nativos do O3D e cor de textura

O binário O3D usa um sistema local diferente das coordenadas de posicionamento gravadas no mapa/SCO. Para o viewport Babylon (Y-up), os vértices O3D devem permanecer em seus eixos nativos durante a renderização; a conversão X/Y/Z do mapa continua acontecendo apenas na composição da cena.

A seção O3D `0x79` é preservada como metadata de transformação e não é mais aplicada isoladamente como inversa sobre os vértices de prévia. Aplicar apenas a inversa deslocava/rotacionava meshes sem reaplicar o transform de objeto correspondente.

Quando existe textura difusa, o RGB da textura passa a ser usado diretamente como cor de superfície. O `diffuseColor` do material não multiplica mais a textura, evitando objetos OMSI escurecidos ou pretos.


## Conversão correta dos eixos de rotação OMSI → viewport

A rotação dos objetos de mapa passa a seguir a ordem usada pelo formato OMSI em coordenadas Z-up. Depois da conversão de coordenadas OMSI `(X,Y,Z)` para o viewport `(X,Z,Y)`, o mapeamento aplicado é:

- rotação principal/Z do OMSI → yaw/Y do viewport;
- pitch/X do OMSI → pitch/X do viewport;
- bank/Y do OMSI → roll/Z do viewport.

As transformações locais `[rot_x]`, `[rot_y]` e `[rot_z]` dos meshes SCO usam a mesma convenção de sinal. O gizmo de edição também converte de volta para os campos OMSI nessa ordem.

## Separação entre eixos do mapa e eixos locais SCO/O3D

Há duas conversões diferentes e elas não devem ser misturadas:

- colocação `[object]` no mapa: OMSI usa mapa Z-up, então o viewport converte `(X,Y,Z)` para Babylon `(X,Z,Y)`;
- transform de mesh dentro do SCO: `[new_pos]`, `[scale]`, `[rot_x]`, `[rot_y]` e `[rot_z]` pertencem ao sistema local do modelo/O3D, que já é Y-up para a composição no Babylon.

Portanto, transforms locais de mesh permanecem em X/Y/Z nativos. Trocar Y/Z nessa camada desloca componentes de objetos compostos e faz rotações locais acontecerem no eixo errado. A conversão Z-up → Y-up ocorre somente no container de colocação do objeto no mapa.



## Overrides de material OMSI e transparência

O preview deve reproduzir a forma como o OMSI associa comandos `[matl]` aos materiais do O3D:

- o nome da textura é a chave principal;
- o segundo valor de `[matl]` é o índice da ocorrência daquela mesma textura, começando em zero, e não o índice absoluto do slot de material O3D;
- `[matl_alpha]` usa o canal alpha da textura difusa conforme o modo 0/1/2;
- `[matl_transmap]` estático deve ser carregado como máscara de opacidade separada;
- referências dinâmicas como `\S:1` não são nomes de arquivo e não devem gerar `textureNotFound`.

Essa distinção é necessária para objetos com materiais repetidos, cruzamentos, placas, vegetação de fundo e outros planos recortados por alpha.


## Compatibilidade de meshes legados, splines e seleção

- Meshes O3D e DirectX texto `.x` usam eixos locais de modelo Y-up no preview. A conversão Z-up do mapa para Y-up do Babylon deve ocorrer somente no contêiner do objeto colocado no mapa.
- O leitor de splines deve respeitar a `[version]` do tile. Tiles antigos podem não ter os mesmos campos dos tiles modernos; em especial o `nextID` não deve ser presumido em versões anteriores.
- `[spline]`, `[spline_h]` e `[splineAbschnitt]` são tratados como splines editáveis quando a versão suportar seus campos.
- A geometria real de objetos e perfis de spline é selecionável no viewport. Clique simples seleciona; duplo clique seleciona e centraliza a câmera no item.
- Texturas BMP legadas são decodificadas no host e transcodificadas uma vez para PNG, com cache, antes de serem enviadas ao Babylon. Isso evita regressões do upload RGBA bruto em superfícies de ruas/cruzamentos.


### Alpha de splines e seleção no viewport

- Superfícies de spline são opacas por padrão. O canal alpha da textura só participa quando o `.sli` declara `[matl_alpha]`, seguindo a semântica do OMSI.
- Materiais de spline usam pequeno `zOffset` somente no preview para reduzir disputa de profundidade com o terreno, sem alterar as coordenadas persistidas.
- Meshes baixos e predominantemente horizontais de objetos, como cruzamentos, podem receber um pequeno lift/zOffset apenas de renderização; o `.map` não é modificado por isso.
- A seleção de objetos/splines ocorre no `pointerdown` do viewport, resolve metadata também pela cadeia de pais do mesh e mantém o estado do segundo clique em `useRef`, para sobreviver à recriação da cena React/Babylon após a primeira seleção.


## Diagnóstico de material, céu e seleção — pós-test.33

- superfícies SLI deixam de depender de uma cópia emissiva da textura: o albedo real é aplicado diretamente em material não iluminado, preservando UV, alpha declarado e coordenadas OMSI;
- meshes O3D horizontais já detectados como rua/cruzamento mantêm o mesmo `render lift` existente; quando possuem textura real, o preview ignora apenas a iluminação do editor para evitar superfícies pretas causadas por normais de pacote;
- não foram adicionados novos offsets arbitrários;
- `himmel01.bmp` / `himmel04.bmp` / `himmel05.bmp` passam pelo caminho BMP → PNG já validado para WebView2, enquanto terreno BMP continua no caminho RGBA bruto;
- clique real em mesh seleciona no `pointerdown` e o mesmo clique deixa de ser reprocessado no `pointerup`, evitando que a seleção válida seja apagada antes do segundo clique rápido;
- o segundo clique rápido no mesmo objeto/spline continua centralizando a câmera;
- o viewport exibe diagnóstico real da seleção com objeto/spline, mesh/material, textura declarada e caminho físico resolvido, faixa UV, posição mundial final, `render lift` e origem SCO/SLI/O3D;
- o caminho físico resolvido da textura é enviado pelo host somente para diagnóstico local do editor, sem criar dados fake/mock.


## Catálogo de mapas, seleção e construção — pós-test.34

- após selecionar a raiz do OMSI, o host usa `OmsiMapCatalog.DiscoverWithProgressAsync` para enumerar somente mapas reais com `global.cfg`;
- a interface lista nome, pasta, quantidade de tiles e uso de `[worldcoordinates]`, permitindo busca e abertura explícita pelo usuário; o seletor manual de pasta permanece apenas como fallback;
- a barra lateral principal pode ser recolhida sem ocultar o acesso às áreas do aplicativo;
- o viewport possui filtros de seleção para tudo, objetos, splines e terreno;
- atalhos de construção abrem as bibliotecas reais existentes: rua usa `.sli`; cruzamento, objeto, água, grama e árvore usam `.sco` / `[tree]`; nenhuma entrada fake é criada;
- o modo Terreno seleciona o tile real e mantém transparente que edição persistente de alturas só será liberada quando a gravação preservativa correspondente estiver validada;
- `[rendertype]` dos SCOs passa a ser preservado no Core e entregue ao viewport;
- objetos declarados como `surface`, `on_surface` ou `presurface` usam a textura diffuse real também no caminho não iluminado do preview, sem alterar coordenadas nem aumentar offsets;
- ruas SLI usam a própria textura OMSI nos canais diffuse/emissive do material não iluminado para evitar o preto causado pela combinação anterior de `disableLighting` com emissive ausente.


## Terreno real, criação por coordenadas e editor — pós-test.35

- o clique no cenário usa `multiPick` e percorre a hierarquia real dos meshes; o bloqueio antigo em mapas com `[worldcoordinates]` foi removido da seleção, permitindo selecionar objetos e splines visíveis;
- ao selecionar objeto ou spline pelo cenário, o Inspetor abre diretamente a aba de transformação para editar o item marcado;
- `Ctrl + setas/WASD` desloca a câmera em incrementos de um tile de 300 m e sincroniza o tile ativo;
- terreno e superfície plana de fallback são pickables no modo Terreno; o clique retorna tile, coordenadas locais e altura;
- objetos e splines escolhidos nas bibliotecas aparecem como prévia real no centro do tile ativo antes da colocação definitiva;
- o criador fácil de rua usa dois cliques: primeiro ponto, segundo ponto, cálculo de comprimento/rotação e sugestão de gradiente com base no terreno carregado;
- ruas existentes e ruas em prévia possuem ação de nivelamento pela altura real do terreno no início e fim;
- o nivelamento manual de terreno grava o `.terrain` real com pincel circular, raio e feather, sempre com backup preservativo;
- a referência de mapa real usa Google Static Maps somente quando o usuário fornece sua própria chave; a imagem é projetada sobre o relevo e mantém atribuição visual;
- a grade de elevação usa Google Elevation em amostras 9×9, 17×17, 25×25 ou 33×33; os valores são reamostrados bilinearmente para a resolução nativa do `.terrain` e escritos com backup;
- o offset vertical é explícito: zero aplica altitude real em metros; mapas que usam datum local podem informar um deslocamento antes de gravar;
- **Criar mapa real** clona exclusivamente `OMSI 2\template\NewMap` da instalação do usuário para `maps\<nova pasta>`, altera nome/friendlyname preservando encoding, cria `.mapstudio/georeference.json` e abre o mapa criado;
- essa criação por coordenadas ainda **não injeta nem converte automaticamente o formato oficial `[worldcoordinates]` do OMSI**. A âncora atual pertence ao Map Studio; a conversão oficial será liberada somente após validarmos todos os campos/arquivos exigidos pelo OMSI.


## Roadmap arquitetural

A arquitetura alvo, incluindo Asset Index persistente, cache incremental, streaming por tiles, etapas de paridade com o editor OMSI e critérios de conclusão, está consolidada em [ROADMAP.md](ROADMAP.md). Quando este documento e o roadmap tratarem do mesmo tema, a implementação atual pertence a este documento e a direção futura pertence ao roadmap.


## Asset Index e streaming incremental — Fase A

A primeira implementação da fundação de desempenho adiciona um **Asset Index SQLite v1** em `MapStudio.Core`.

- existe um banco separado por instalação OMSI, identificado por hash do caminho raiz;
- o banco é armazenado fora da pasta do OMSI, em `LocalApplicationData\OMSI Map Studio\Cache\<id>\assets-v1.sqlite`;
- são indexados `.sco`, `.sli`, `.o3d`, `.x` e formatos de textura reconhecidos;
- a varredura cobre `Sceneryobjects`, `Splines` e `Texture`, ignorando reparse points;
- cada entrada preserva path relativo, tipo, tamanho e `LastWriteTimeUtc`;
- gerações de varredura permitem retirar entradas de arquivos removidos;
- arquivos sem alteração são marcados como vistos sem serem recriados;
- bibliotecas de objetos e splines preferem o índice quando existem entradas e mantêm a varredura direta anterior como fallback;
- a atualização ocorre em segundo plano depois da seleção da instalação OMSI;
- falha do cache é não fatal e não bloqueia mapas, bibliotecas ou leitura direta.

O banco é **derivado** e nunca é fonte autoritativa de dados OMSI. A existência de uma entrada no índice não substitui as validações de path/arquivo antes de qualquer operação persistente.

O carregamento regional ganhou o primeiro modelo em anéis:

- anéis 0 e 1: conteúdo completo da área 3×3;
- anel 2: apenas summary/metadata leve do tile, sem decodificar terrain grid, RDY ou masks;
- ao mover a região ativa, a UI remove payloads pesados de terreno que ficaram fora da janela de streaming;
- objetos e splines visíveis continuam vindo apenas da região completa para manter picking e identidade reais.

Esta etapa é intermediária. Cache derivado de geometria/material, fila de prioridade completa, LOD/instancing e descarte explícito de recursos Babylon/GPU ainda pertencem à Fase A.
