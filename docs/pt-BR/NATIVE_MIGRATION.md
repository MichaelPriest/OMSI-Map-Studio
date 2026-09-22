# Migração nativa do OMSI Map Studio

Esta trilha recria o editor sobre a arquitetura nativa do Windows sem descartar o domínio OMSI já implementado.

## Stack alvo

- .NET 10;
- WinUI 3 / Windows App SDK;
- Direct3D 11;
- Vortice.Windows;
- MapStudio.Core preservado como autoridade de formatos, leitura, validação e gravação;
- SQLite/cache preservados;
- sem WebView2 no viewport principal;
- sem React no lifecycle do renderer.

A base usa Windows App SDK 2.5.1 e Vortice.Direct3D11 3.8.3.

## Estratégia

A migração acontece em paralelo ao editor atual.

### Fase N0 — fundação

- novo projeto `MapStudio.Renderer`;
- novo projeto `MapStudio.Native`;
- Direct3D 11 inicializado em runtime próprio;
- input de mouse recebido diretamente pelo WinUI;
- registry de picking com ID estável;
- codec para ID buffer;
- CI dedicado em Windows.

### Fase N1 — viewport OMSI mínimo

- ligar `SwapChainPanel` à swap chain DXGI;
- câmera perspectiva/top;
- terreno Gundorf;
- objetos O3D reais;
- splines;
- ID buffer;
- hover e seleção;
- validação de DPI 100/125/150/200%.

### Fase N2 — edição

- gizmo mover;
- gizmo rotacionar;
- snap;
- Inspector;
- salvar via MapStudio.Core;
- undo/redo.

### Fase N3 — interface completa

- Explorer nativo;
- bibliotecas;
- previews;
- construction tools;
- terreno;
- mapa real;
- diagnostics/Map Health;
- fullscreen;
- atalhos.

### Fase N4 — substituição

A versão nativa só substitui o host WebView2 quando atingir paridade funcional suficiente e passar os testes reais. Até lá, o aplicativo atual continua disponível para comparação.

## Seleção

A seleção nativa não dependerá de material, transparência ou textura do objeto. Cada entidade OMSI recebe um `PickingId`. Em uma passagem própria, o renderer grava esse ID num render target inteiro. O pixel sob o cursor identifica diretamente a entidade selecionada.

Isso elimina a cadeia de fallbacks que se tornou necessária no viewport WebView2/Babylon.


### Checkpoint N0.1 — apresentação Direct3D

A fundação agora cria uma `IDXGISwapChain1` para composição, associa a swap chain ao `SwapChainPanel` por `ISwapChainPanelNative`, cria o backbuffer/RTV e apresenta um primeiro frame Direct3D 11 real.

O tamanho do backbuffer usa `CompositionScaleX/Y` do WinUI, portanto o renderer trabalha em pixels físicos e reage a mudanças de DPI e redimensionamento sem passar por CSS/WebView2.


### Checkpoint N0.2 — sessão OMSI nativa

O host WinUI agora abre a pasta real do OMSI e uma pasta de mapa usando o seletor nativo do Windows. O `MapStudio.Core` é chamado diretamente, sem bridge WebView2, para descobrir mapas, abrir `global.cfg`, escolher o tile inicial e carregar a região 3×3 real.

O painel nativo já exibe contagens reais de tiles, objetos, splines e terrenos carregados. O próximo checkpoint transforma esse snapshot do Core em buffers GPU.


### Checkpoint N1.1 — navegação GPU nativa

O overview nativo agora possui transformação de viewport executada no vertex shader. Zoom por roda do mouse e pan por botão direito/meio alteram somente um constant buffer Direct3D; a geometria O3D não é reconstruída a cada movimento.

A mesma transformação é usada no passe visível e no ID buffer, mantendo o pixel de seleção alinhado ao objeto mesmo depois de navegar pelo mapa.


### Checkpoint N1.2 — terreno OMSI real

O renderer nativo agora transforma a grade de alturas real de cada tile em triângulos GPU. O primeiro modo de visualização continua top-down, mas já usa os dados reais de relevo para cor/profundidade e prepara a mesma malha para a futura câmera perspectiva.

Objetos SCO que não usam `[absheight]` também voltam a receber interpolação bilinear do terreno antes da transformação O3D, preservando a regra do editor existente.


### Checkpoint N1.3 — profundidade real no viewport e no ID buffer

O viewport visível e o passe de seleção agora possuem depth buffer Direct3D próprio. A seleção deixa de depender da ordem em que os triângulos foram desenhados: quando objetos/proxies se sobrepõem, o pixel de picking preserva a superfície mais próxima segundo a profundidade.

O mesmo critério é usado no frame visível e no ID buffer, aproximando o comportamento do seletor de um editor 3D nativo.


### Checkpoint N1.4 — câmera 3D perspectiva e geometria em espaço mundial

O viewport nativo deixou de pré-projetar terreno, splines e O3D em um overview 2D. Os buffers GPU agora preservam coordenadas mundiais X/Y/Z reais e o vertex shader recebe uma matriz `ViewProjection` perspectiva.

A câmera enquadra a região OMSI carregada, usa roda do mouse para dolly/zoom, botão do meio para pan sobre o plano do mapa e botão direito para órbita 3D. Redimensionamento e DPI recalculam a projeção sem reconstruir a geometria.

O terreno usa a altura OMSI como eixo Y real. Objetos O3D mantêm a transformação SCO/O3D e a interpolação bilinear do terreno antes de chegar à GPU. Splines e proxies de seleção também passaram para espaço mundial.

O frame visível e o ID Buffer usam exatamente a mesma matriz de câmera e seus depth buffers independentes, portanto o picking continua alinhado na perspectiva 3D.


### Checkpoint N1.5 — splines por perfil SLI real

O renderer nativo agora lê cada arquivo `.sli` pelo `MapStudio.Core` e extruda as superfícies reais definidas por `[profile]` / `[profilepnt]` ao longo do comprimento e da curvatura da spline.

A malha usa a largura e a altura do perfil real, raio, rotação e gradientes inicial/final da instância. A elevação longitudinal integra o gradiente percentual ao longo do comprimento em vez de achatar a spline no terreno.

Também foi corrigida a regra de altura: splines OMSI usam a cota absoluta da própria instância; a interpolação do terreno continua reservada aos objetos relativos. O ID Buffer recebe a mesma malha real da spline, mantendo o proxy apenas como área auxiliar de clique.

Neste checkpoint a geometria do perfil é real, mas a aplicação das texturas SLI ainda fica para o próximo refinamento de materiais.


### Checkpoint N1.6 — hover azul e seleção vermelha com depth

O comportamento de interação do editor OMSI foi reproduzido no viewport nativo: o item apenas apontado recebe highlight azul e o item selecionado recebe highlight vermelho.

Hover e seleção usam o mesmo `PickingId` do ID Buffer e preferem a geometria real O3D ou spline; os proxies continuam apenas como fallback de seleção. O hover é limpo ao sair do viewport ou iniciar pan/órbita.

Como o viewport agora usa depth buffer real, a cópia de highlight recebe um deslocamento muito pequeno na direção da câmera. Assim a sobreposição continua visível sem depender da ordem de desenho ou desabilitar a profundidade da cena.


### Checkpoint N2.1 — gizmos nativos de mover e rotacionar

A fase de edição começou no viewport Direct3D. Ao selecionar um objeto ou spline, o renderer cria um gizmo 3D real no ponto de inserção da entidade, com handles próprios no mesmo ID Buffer usado pelo restante da cena.

O modo **Mover** expõe X/Y/Z e mantém cada drag restrito ao eixo escolhido. O modo **Rotacionar** expõe X/Y/Z para objetos SCO/O3D; splines usam somente rotação Y, que corresponde ao campo de rotação disponível no formato de mapa OMSI.

Durante o drag, a geometria vermelha selecionada recebe uma transformação de preview sem reconstruir o mapa inteiro a cada pixel. Ao soltar, a transformação é aplicada à entidade do snapshot e convertida diretamente em `OmsiObjectTransformEdit` ou `OmsiSplineTransformEdit`, ficando como edição real pendente de persistência pelo Core.

Os handles do gizmo usam `PickingKind.Gizmo` e IDs dedicados, portanto não colidem com IDs de objetos ou splines mesmo quando estão desenhados na frente da mesma geometria.


### Checkpoint N2.2 — persistência transacional das transformações

As transformações produzidas pelos gizmos agora podem ser acumuladas no host nativo e salvas no mapa OMSI real. O host deduplica edições sucessivas da mesma entidade e agrupa alterações por tile antes de gravar.

A persistência reutiliza `OmsiTileObjectEditor` e `OmsiTileSplineEditor`; nenhum formato paralelo é criado. Cada tile alterado é processado pelo `SafeFileTransaction`, que cria backup em `.mapstudio-backups`, escreve em arquivo temporário e usa substituição atômica com restauração em caso de falha.

Depois de uma gravação bem-sucedida, os tiles afetados são relidos pelo `MapStudio.Core` e o estado pendente é limpo. A interface WinUI expõe **Salvar alterações** apenas quando há transformações pendentes.


### Checkpoint N2.3 — undo/redo e snap nativos

O runtime mantém histórico de transformações em pares antes/depois usando os mesmos `OmsiObjectTransformEdit` e `OmsiSplineTransformEdit` usados para persistência. **Desfazer** e **Refazer** reaplicam esses estados ao snapshot, reconstroem somente o estado necessário do renderer e voltam a registrar a versão resultante como edição pendente para o salvamento seguro.

O snap pode ser ligado ou desligado pela interface. Nesta primeira configuração, movimento é quantizado em **0,25 m** e rotação em **5°**. O valor quantizado é aplicado ao preview vermelho, ao gizmo e ao edit OMSI final, evitando diferença entre o que o usuário vê durante o drag e o que será gravado.


### Checkpoint N2.4 — Inspector nativo ligado à seleção real

O painel Inspector do host WinUI passou a consumir diretamente o estado da entidade selecionada no renderer. Para objetos, exibe ID, tile, caminho SCO, coordenadas OMSI, rotação, pitch e bank. Para splines, exibe caminho SLI, coordenadas OMSI, rotação, comprimento, raio e gradientes inicial/final.

O Inspector é atualizado após seleção por ID Buffer, movimento/rotação por gizmo e operações de desfazer/refazer. Não existe cópia de estado separada no XAML: os valores exibidos vêm do mesmo snapshot nativo que gera a geometria e os edits persistidos.


### Checkpoint N2.5 — edição numérica pelo Inspector

O Inspector nativo deixou de ser apenas informativo e passou a editar a transformação da entidade selecionada. Objetos permitem alterar X/Y/Z, rotação, pitch e bank. Splines permitem alterar X/Y/Z, rotação, comprimento, raio e gradientes inicial/final.

Ao clicar em **Aplicar valores**, o runtime atualiza o mesmo snapshot usado pelo viewport, cria um par antes/depois no histórico, atualiza a geometria nativa, registra a transformação OMSI como pendente e mantém compatibilidade com Desfazer/Refazer e Salvar alterações.

Assim, gizmo e Inspector são duas interfaces sobre o mesmo modelo de edição nativo, sem duplicação de estado.


### Checkpoint N3.1 — Explorer nativo com seleção e foco

A fase N3 começou com um Explorer WinUI alimentado diretamente pelo snapshot do renderer. Objetos e splines reais são listados com tipo, ID, arquivo e tile, sem inventário paralelo.

A busca filtra nome exibido, caminho do asset e coordenadas do tile. Selecionar um item no Explorer usa o mesmo `PickingId` da cena e atualiza o highlight vermelho, o gizmo e o Inspector. Um duplo clique também reposiciona a câmera 3D sobre o ponto de inserção da entidade.

Seleções feitas diretamente no viewport são sincronizadas de volta para a lista e roladas para ficar visíveis, mantendo Explorer e viewport como duas visualizações do mesmo estado nativo.


### Checkpoint N3.2 — biblioteca nativa sobre o índice SQLite existente

A biblioteca de assets do host WinUI reutiliza o `OmsiAssetIndex` do `MapStudio.Core`. O índice SQLite fica no cache local do usuário e é separado por instalação OMSI, sem varrer a instalação inteira toda vez que o editor abre.

A interface alterna entre **Cena** e **Biblioteca**, permite filtrar todos os assets ou somente SCO, SLI, modelos e texturas, além de pesquisar pelo caminho relativo. O botão **Atualizar** executa o refresh incremental do índice e mostra progresso real de arquivos examinados e candidatos encontrados.

Quando um asset da biblioteca já está usado na região carregada, um duplo clique localiza o primeiro uso no Explorer e foca a câmera 3D. Assets ainda não usados permanecem disponíveis no catálogo para a próxima etapa de preview/placement nativo.


### Checkpoint N3.3 — prévia 3D nativa da biblioteca

A biblioteca indexada agora possui prévia visual real para objetos **SCO/O3D** e **splines SLI**. Selecionar um asset compatível carrega diretamente o arquivo da instalação OMSI, sem criar uma entidade falsa no mapa e sem substituir o snapshot atualmente aberto.

Para SCO, a prévia usa os meshes O3D/X reais, transformações declaradas no SCO, LOD e cores difusas dos materiais. Para SLI, o renderer extruda o perfil `[profile]/[profilepnt]` real em uma amostra 3D navegável.

O viewport entra temporariamente em modo de prévia e ajusta a câmera aos limites do asset. Órbita, pan e zoom continuam usando a mesma câmera Direct3D. Ao voltar para **Cena**, os buffers do mapa são restaurados a partir do snapshot e dos assets já carregados, sem nova leitura completa do mapa.

Modelos avulsos e texturas continuam listados pelo índice, mas a prévia deste checkpoint é deliberadamente limitada a SCO/O3D e SLI; essas categorias serão expandidas junto com materiais/texturas e placement.


### Checkpoint N3.4 — placement nativo de objetos SCO

A biblioteca agora pode iniciar o posicionamento de um objeto SCO diretamente sobre o mapa aberto. O viewport restaura a cena, carrega a geometria real do asset e exibe um **ghost 3D azul** separado do ID Buffer, de forma que a prévia não seja confundida com uma entidade já existente.

O cursor é convertido em um raio da câmera perspectiva para o mundo. A interseção é refinada contra a altura real do terreno carregado e, quando o snap está ativo, X/Z são quantizados em 0,25 m antes da amostragem final de altura. O placement é aceito somente dentro de um tile realmente carregado.

Ao clicar, o host converte a posição mundial para as coordenadas locais do tile OMSI e usa `OmsiTileObjectInserter` para inserir uma seção `[object]` real. O próximo ID é calculado considerando objetos e splines de todos os tiles do mapa, evitando colisão com IDs já usados. Quando há um objeto do mesmo asset no mapa, header e valores extras são preservados como template; árvores sem template usam os dados reais de textura/altura/aspecto declarados no SCO.

A inserção é persistida imediatamente por `SafeFileTransaction`, com backup em `.mapstudio-backups`, e o tile alterado é relido pelo Core antes de atualizar o viewport. Splines continuam fora deste checkpoint porque o placement correto exige a ferramenta de construção por pontos/curvas em vez de tratá-las como um objeto comum.


### Checkpoint N3.5 — construção nativa de splines por pontos e curvas

A biblioteca SLI agora inicia uma ferramenta de construção diretamente no viewport Direct3D. O asset selecionado usa o perfil real do arquivo `.sli` para desenhar um ghost 3D antes da gravação.

Há dois fluxos:

- **Reta:** primeiro clique define o início e o segundo clique define o fim.
- **Curva:** primeiro clique define o início, o segundo fixa o fim e o terceiro ponto controla a curvatura. O editor resolve o círculo que passa pelos três pontos e converte o resultado para os campos reais do OMSI: rotação inicial, comprimento de arco e raio com sinal.

Os pontos são obtidos pelo raycast da câmera perspectiva contra a altura real do terreno. O snap de 0,25 m também é aplicado à construção. A diferença de altitude entre início e fim é convertida em gradiente percentual inicial/final, permitindo que a spline acompanhe a elevação entre os pontos em vez de ser achatada.

A inserção usa `OmsiTileSplineInserter`. O ID novo é calculado globalmente considerando objetos e splines de todos os tiles. Quando existe uma spline compatível, header e valores extras são reutilizados; caso contrário o Core procura um template neutro de spline normal. A gravação passa por `SafeFileTransaction`, cria backup e relê o tile modificado antes de atualizar a cena.

Este checkpoint estabelece a base da ferramenta de ruas estilo editor de cidades. Os próximos refinamentos serão continuidade entre segmentos, handles editáveis após a criação, encaixe em extremidades existentes e construção sequencial sem sair do modo.


### Checkpoint N3.6 — snap em extremidades de splines

O snap da ferramenta de construção passou a reconhecer as extremidades reais das splines já carregadas. Durante a criação de uma nova rua/spline, o ponto sob o cursor continua sendo obtido pelo terreno e pela grade de 0,25 m, mas também procura o início e o fim das splines existentes dentro de uma tolerância dependente da distância da câmera.

Quando uma extremidade é encontrada, a posição usa exatamente o X/Z e a altura Y do endpoint existente. Isso evita pequenas frestas e diferenças verticais ao começar ou terminar um segmento próximo de uma rua já existente.

O cálculo usa a geometria paramétrica real da spline por NativeSplinePathMath para obter o endpoint final, portanto também funciona em segmentos curvos e com gradiente.


### Checkpoint N3.7 — construção sequencial de splines

A ferramenta SLI ganhou o modo **Continuar segmentos**, ativado por padrão na biblioteca. Depois que um segmento é persistido e o tile é relido, o viewport reinicia a mesma ferramenta e usa exatamente o `EndWorld` do segmento anterior como início do próximo.

Isso elimina a necessidade de voltar à biblioteca e clicar novamente no ponto de junção. Em modo reto, cada segmento seguinte exige apenas o novo ponto final. Em modo curva, o início já fica fixado e o usuário define o novo fim e o ponto de curvatura.

O encadeamento deste checkpoint garante continuidade geométrica de posição e altura. A atualização automática dos campos `PreviousSplineId` / `NextSplineId` ficará em um refinamento separado do Core, porque o editor atual desses campos valida os vínculos existentes mas ainda não os reescreve.


### Checkpoint N3.8 — encadeamento lógico Previous/Next

A construção sequencial agora também preserva a ligação lógica usada pelo formato OMSI. O request do próximo segmento carrega o ID da spline anterior. Ao inserir, a nova seção `[spline]` recebe esse valor em `PreviousSplineId` e a spline anterior é atualizada para apontar o novo ID em `NextSplineId`.

Foi adicionado ao Core um editor dedicado de links de spline. Ele valida ordinal, caminho, ID e os valores Previous/Next originais antes de escrever, evitando alterar silenciosamente um arquivo que mudou desde a leitura.

Quando os dois segmentos estão no mesmo tile, a inserção e a atualização do link são combinadas no mesmo documento. Quando estão em tiles diferentes, os dois arquivos entram juntos no `SafeFileTransaction`; ambos recebem backup e a operação é tratada como uma única gravação transacional.

Com isso, a ferramenta contínua deixa de produzir apenas segmentos geometricamente encostados: a sequência também fica encadeada pelos IDs do mapa OMSI.


### Checkpoint N3.9 — tela cheia e atalhos nativos

O host WinUI passa a oferecer tela cheia real do Windows por `AppWindowPresenterKind.FullScreen`, alternada por **F11**. **Esc** sai da tela cheia e também cancela imediatamente uma ferramenta de placement/construção ativa antes de afetar a janela.

Os atalhos principais agora ficam no próprio host nativo:

- **W** ativa o gizmo Mover;
- **E** ativa o gizmo Rotacionar;
- **Ctrl+S** salva transformações pendentes pelo mesmo fluxo seguro com backup;
- **Ctrl+Z** desfaz a última transformação;
- **Ctrl+Y** refaz a transformação;
- **F11** alterna tela cheia;
- **Esc** cancela placement/construção ou sai da tela cheia.

W/E e Ctrl+Z/Ctrl+Y não interceptam teclas quando o foco está em `TextBox`, `RichEditBox`, `PasswordBox` ou `NumberBox`, preservando digitação e edição dos campos do Inspector. O foco é consultado pelo `FocusManager` usando o `XamlRoot` da janela.


### Checkpoint N3.10 — painéis nativos redimensionáveis e recolhíveis

O workspace WinUI deixa de usar larguras rígidas para Explorer e Inspector. Dois separadores nativos entre os painéis e o viewport permitem ajustar a largura por arraste sem afetar a superfície Direct3D.

O Explorer pode variar entre 220 e 520 px e o Inspector entre 240 e 560 px, com limites adicionais para preservar uma área mínima útil do viewport. As últimas larguras são mantidas em memória ao recolher o painel.

O menu **Visualizar** agora permite alternar Explorer e Inspector individualmente. Ao recolher, tanto o painel quanto a coluna do separador ficam com largura zero; ao restaurar, o painel retorna à última largura usada. O `SwapChainPanel` continua reagindo ao `SizeChanged`, então o backbuffer Direct3D acompanha imediatamente o novo espaço disponível.

### Checkpoint N3.11 — exclusão nativa segura de objetos e splines

A exclusão de entidades da versão React foi migrada para o host WinUI. O Inspector agora oferece **Excluir selecionado**, e a tecla **Delete** aciona o mesmo fluxo quando o foco não está em um campo de texto.

Para objetos, o host valida tile, ID, caminho SCO e ordinal da seção antes de remover a seção `[object]` pelo `OmsiTileObjectDeleter`.

Para splines, a operação lê os IDs do mapa inteiro, detecta IDs duplicados e usa `OmsiSplineLinkPlanner` para liberar com segurança os vínculos recíprocos Previous/Next dos vizinhos antes de remover a seção da spline selecionada. Alterações em vários tiles entram na mesma `SafeFileTransaction`.

A exclusão é bloqueada enquanto existem transformações pendentes ou uma ferramenta de placement/construção está ativa. Antes da gravação, a interface pede confirmação e informa que será criado backup em `.mapstudio-backups`.

Depois da operação, os tiles carregados afetados são relidos pelo Core e o viewport, Explorer e Inspector voltam a refletir o estado real do mapa.

### Checkpoint N3.12 — cópia nativa de objetos com placement real

O fluxo **Colocar cópia** da versão React foi migrado para o Inspector WinUI. Quando um objeto real está selecionado, o botão **Colocar cópia** e o atalho **Ctrl+D** iniciam um novo placement usando o mesmo arquivo SCO.

A cópia preserva inicialmente os campos reais de transformação da seleção: **Z, rotação, pitch e bank**. O próximo clique sobre o terreno define a nova posição horizontal X/Y. Para objetos relativos, o ghost soma o Z preservado à altura real do terreno; para objetos com altura absoluta, mantém o Z absoluto da seleção.

O ghost 3D usa a mesma rotação/pitch/bank que será persistida, evitando diferença entre a prévia e o resultado salvo. A nova instância continua recebendo ID global livre e é gravada pelo fluxo existente de `OmsiTileObjectInserter` + `SafeFileTransaction`, com backup automático.

O atalho Ctrl+D não intercepta digitação quando o foco está em campos editáveis e o fluxo permanece bloqueado para mapas com `worldcoordinates` até a migração específica de georreferenciamento.

### Checkpoint N3.13 — cópia desconectada e editor nativo de vínculos de spline

O Inspector WinUI agora migra mais dois recursos da versão React para splines reais.

**Colocar cópia** / **Ctrl+D** também funciona com uma spline selecionada. A ferramenta usa o mesmo SLI real como template, escolhe automaticamente construção reta ou curva a partir do raio da seleção e cria a nova spline desconectada, com Previous/Next iniciando em `-1`. Splines de altura permanecem bloqueadas até o fluxo específico de `[spline_h]` ser migrado.

O Inspector de spline também exibe **Anterior ID** e **Próxima ID**. **Salvar vínculos** executa `OmsiSplineLinkPlanner` contra os IDs do mapa inteiro, valida reciprocidade e disponibilidade das pontas e aplica todas as alterações necessárias através de `OmsiTileSplineLinkEditor`.

Quando a mudança afeta splines em tiles diferentes, todos os arquivos entram na mesma `SafeFileTransaction` e recebem backup. Depois da gravação, os tiles carregados afetados são relidos e a spline editada volta a ser selecionada no viewport/Explorer com os novos vínculos.

### Checkpoint N3.14 — nivelamento de terreno no host nativo

A primeira ferramenta real de edição de terreno da versão React foi migrada para o Inspector WinUI.

O painel **Terreno** permite ativar **Escolher ponto no mapa**. O próximo clique no viewport usa o raio da câmera Direct3D contra a altura real do terreno e identifica tile, coordenadas locais e altura atual. O Inspector preenche automaticamente a altura alvo com o valor encontrado.

O usuário pode então ajustar **altura alvo**, **raio do pincel** e **feather**. **Aplicar nivelamento** usa `OmsiTerrainLeveler.LevelCircularBrush` sobre o arquivo `.terrain` real e grava o resultado por `SafeFileTransaction`, com backup automático antes da substituição.

Depois da gravação, o tile é relido pelo `MapStudio.Core` e o viewport é reconstruído com a nova topografia. A operação é bloqueada enquanto existem transformações de objeto/spline pendentes, evitando misturar duas transações de edição diferentes.

Este checkpoint cobre nivelamento circular. Ele ainda não inclui elevar/abaixar por arraste, pintura de textura do terreno, criação/remoção de tile ou importação DEM.

### Checkpoint N3.15 — mapa completo, desempenho 3×3 e navegador de tiles

O host WinUI passa a migrar os dois modos de carregamento do editor React. **Mapa completo** é o padrão da arquitetura nativa e carrega todos os tiles do mapa; **Modo desempenho 3×3** mantém apenas a região de raio 1 ao redor do tile ativo.

A leitura do mapa completo é limitada a no máximo seis carregamentos de tile concorrentes, evitando disparar centenas de leituras/parses de uma vez em mapas grandes.

O menu **Mapa** permite alternar entre os dois modos sem reabrir o mapa. A troca é bloqueada enquanto existem transformações pendentes para não descartar edições não salvas.

O Explorer recebe um navegador de tiles com campos X/Y, botão **Ir** e deslocamento pelas quatro direções. No modo completo, navegar apenas altera o tile ativo e move a câmera para o centro dele. No modo 3×3, a navegação recarrega a região em torno do novo tile e reconstrói o viewport com os objetos, splines e terreno daquela área.

Após qualquer recarregamento, Inspector, histórico visual e ferramenta de terreno são sincronizados com o novo snapshot real do Core.

### Checkpoint N3.16 — texturas difusas O3D no Direct3D 11

O renderer nativo deixa de tratar todos os meshes O3D apenas pela cor difusa. O pipeline de vértices agora preserva as coordenadas UV reais do modelo e agrupa os triângulos em lotes por material/textura.

`NativeSceneryAssetLoader` resolve o caminho real das texturas declaradas pelos materiais O3D usando `OmsiTextureAssetPathResolver`, inclusive a busca segura por substitutos DDS já suportada pelo Core.

O Direct3D 11 carrega as texturas decodificáveis pelo Windows Imaging Component, cria `ID3D11Texture2D`/Shader Resource View e usa um pixel shader texturizado com sampler linear wrap. Alpha baixo usa descarte no shader, permitindo recortes básicos de grades, vegetação e materiais com máscara em vez de transformar toda a superfície em um bloco opaco.

Texturas ausentes ou não decodificáveis não derrubam o mapa: o lote continua usando a cor difusa O3D como fallback. O cache mantém somente texturas solicitadas pela cena atual e limita o primeiro lote a 256 caminhos distintos para evitar consumo descontrolado ao abrir mapas grandes.

Este checkpoint cobre a textura difusa base dos objetos O3D. Overrides avançados SCO (`[matl]`, night map, bump/environment/light map), texturas das splines e pintura do terreno continuam em checkpoints seguintes.

### Checkpoint N3.17 — texturas reais das splines SLI

O renderer nativo agora preserva os parâmetros de textura declarados em `[profilepnt]` e aplica as texturas reais das superfícies SLI.

Cada `NativeSplineAsset` resolve os arquivos de textura através de `OmsiTextureAssetPathResolver.TryResolveSplineTexture`. Durante a tesselação, o UV horizontal vem de `TextureX` e o UV longitudinal segue `distância × TextureScale`, compatível com a semântica usada pelo formato OMSI para repetição ao longo da spline.

As superfícies são agrupadas em lotes de material/textura e usam o mesmo cache WIC/Direct3D 11 introduzido para objetos O3D. Quando uma textura não existe ou não pode ser decodificada, a spline continua renderizando com fallback sem textura em vez de desaparecer ou abortar o mapa.

Com isso, ruas, calçadas, meios-fios e outras superfícies SLI deixam de depender apenas da cor cinza genérica no viewport nativo.

### Checkpoint N3.18 — textura base real do terreno

O renderer nativo passa a usar o primeiro `[groundtex]` real do `global.cfg` como textura base do terreno.

`NativeTerrainTriangleGeometryBuilder` recebe o descritor real do mapa e a raiz do OMSI, resolve `MainTexturePath` por `OmsiTextureAssetPathResolver.TryResolveGroundTexture` e gera UV por tile usando a repetição declarada em `MainTextureRepeating`.

A textura é enviada pelo mesmo cache WIC/Direct3D 11 já usado por O3D e SLI. Quando o arquivo não existe ou não pode ser resolvido, o terreno mantém o fallback de cor por altura em vez de falhar.

O mapeamento é reiniciado em cada tile e cobre 0–300 m com a repetição definida pelo mapa, preservando a escala esperada do OMSI.

Este checkpoint cobre a camada base. Camadas adicionais pintadas por `tile_*.map.N.dds`, detalhe secundário e edição de pintura permanecem para o próximo passo.

### Checkpoint N3.19 — camadas pintadas do terreno com máscaras DDS

O renderer nativo agora aplica também as camadas de terreno pintadas pelo OMSI através dos arquivos `tile_*.map.N.dds`.

Cada máscara válida usa seu `LayerIndex` para selecionar o `[groundtex]` correspondente do `global.cfg`. A textura principal da camada mantém sua própria repetição, enquanto a máscara usa UV normalizado de 0–1 sobre os 300 m do tile.

O vértice nativo passou a carregar dois conjuntos de UV: um para a textura repetida e outro exclusivo para a máscara. O shader `PSTerrainLayer` combina a textura da camada com o alpha da máscara e usa alpha blend sobre a textura base.

As máscaras A8 DDS do OMSI são lidas diretamente pelo renderer e convertidas para uma textura GPU RGBA, evitando depender do suporte variável do WIC para esse formato. Máscara ou textura inválida faz apenas aquela camada ser ignorada.

Para impedir z-fighting entre a base e as sobreposições sem alterar os dados do mapa, cada camada recebe apenas um deslocamento visual mínimo de milímetros no renderer. Nenhuma altura persistida do `.terrain` é modificada.

Ainda falta aplicar a textura de detalhe secundária de `[groundtex]` e migrar a pintura/edição das máscaras.

### Checkpoint N3.20 — céu OMSI nativo e preview dia/noite

O host WinUI passa a renderizar o céu real do OMSI no Direct3D 11 em vez de depender somente da cor de limpeza do viewport.

O runtime procura `Texture\himmel01.bmp` para o preview diurno e `Texture\himmel05.bmp` para o preview noturno. Quando esses arquivos não existem, mantém a mesma compatibilidade usada pela versão React e tenta `Texture\skybox\day01.bmp` ou `night01.bmp`.

O céu é desenhado em uma esfera texturizada centrada na câmera. A geometria fica fora do ID Buffer e usa um depth state sem escrita de profundidade, portanto não interfere na seleção nem pode ocultar objetos, splines ou terreno distantes.

O mapeamento horizontal replica a inversão usada no viewport React e o eixo vertical fica limitado na borda da textura para evitar costuras no polo.

A barra superior do host nativo recebe a opção **Noite**, que alterna imediatamente entre o céu diurno e noturno sem recarregar o mapa.

### Checkpoint N3.21 — overrides SCO de nightmap/lightmap no renderer nativo

O pipeline nativo passa a carregar overrides de material definidos no SCO para cada mesh e índice de material.

`NativeSceneryAssetLoader` usa os `MaterialOverrides` já parseados pelo `MapStudio.Core` e resolve `[matl_nightmap]` e `[matl_lightmap]` pelo mesmo resolvedor seguro de texturas usado nos materiais O3D normais.

Os lotes de material preservam agora textura base, night map, light map e o valor de `matl_alpha` para etapas seguintes. No modo diurno, o comportamento permanece idêntico ao pipeline difuso atual. Quando **Noite** está ativo, o renderer seleciona primeiro o night map e, quando ele não existe, usa o light map como textura secundária do material.

O shader noturno combina a textura base com a textura secundária sem alterar os arquivos do mapa. O mesmo botão **Noite** que troca o céu passa portanto a dar também uma prévia visual das fachadas, placas e superfícies que possuem mapa noturno.

Este checkpoint ainda não altera depth state nem blending de `matl_alpha`, `matl_noZwrite` e `matl_noZcheck`; bump map e environment map também permanecem para um checkpoint separado.

### Checkpoint N3.22 — matl_alpha e estados noZ nativos

O renderer nativo passa a respeitar os três modos de alpha definidos pelo SCO:

- `matl_alpha 0`: material opaco; o alpha da textura não recorta a geometria.
- `matl_alpha 1`: recorte binário; pixels abaixo do limiar de alpha são descartados e os demais permanecem opacos.
- `matl_alpha 2`: transparência parcial com alpha blending.

Os mesmos modos também são aplicados quando o material possui night map/light map, usando shaders noturnos equivalentes.

`matl_noZwrite` passa a usar depth test somente leitura, preservando a comparação com a cena sem gravar profundidade. `matl_noZcheck` desativa o depth test para o lote do material. Depois de cada lote, o renderer restaura os estados padrão para não contaminar terreno, splines ou outros objetos.

Os flags são carregados diretamente dos `MaterialOverrides` do Core e permanecem associados ao material correto de cada mesh.

### Checkpoint N3.23 — pintura nativa das máscaras de terreno

O host WinUI passa a editar as mesmas máscaras A8 DDS que o OMSI usa para pintar as camadas adicionais de `[groundtex]`.

O `MapStudio.Core` ganhou leitura dos pixels alpha, escrita DDS A8 válida e um pincel circular com raio, alpha alvo e feather. O pincel trabalha nas coordenadas locais reais de 0–300 m do tile e interpola o alpha existente em direção ao valor solicitado, permitindo tanto pintar quanto apagar uma camada.

No Inspector, o mesmo ponto escolhido pela ferramenta de terreno pode ser usado para **Pintar textura no ponto**. O usuário escolhe o índice da camada `groundtex`, alpha de 0–255, raio e feather. A camada 0 continua reservada como textura base; a pintura atua somente nas camadas 1 em diante.

Quando a máscara `tile_*.map.N.dds` já existe, ela é substituída por `SafeFileTransaction` com backup automático. Quando a camada ainda não possui máscara, o editor cria uma nova DDS usando a resolução declarada pelo `ResolutionCode` do `[groundtex]` e publica o arquivo de forma atômica por arquivo temporário + rename.

Depois da gravação, o tile é relido pelo Core e o viewport Direct3D reconstrói imediatamente as camadas pintadas. Nenhum caminho fornecido pela UI é aceito diretamente: nome e destino da máscara são derivados do tile real e da camada validada.

### Checkpoint N3.24 — detail texture completa de [groundtex]

O material de terreno nativo agora também usa `DetailTexturePath` e `DetailTextureRepeating` de cada `[groundtex]`.

O vértice Direct3D ganhou um terceiro conjunto de UV exclusivo para o detalhe. Isso permite manter simultaneamente:
- UV principal da textura do terreno;
- UV normalizado 0–1 da máscara DDS;
- UV independente da detail texture.

A textura principal e a detail texture são resolvidas pelo mesmo `OmsiTextureAssetPathResolver`. Base e camadas pintadas podem ter repetições diferentes, exatamente como declarado no `global.cfg`.

Os shaders `PSTerrainBaseDetail` e `PSTerrainLayerDetail` modulam o detalhe sobre a textura principal sem alterar o alpha da máscara. Quando a detail texture estiver ausente ou não puder ser decodificada, o renderer usa o caminho anterior sem detalhe.

O cache GPU também passa a acompanhar os arquivos de detalhe e os libera ao trocar de mapa/cena.

### Checkpoint N3.25 — câmera Top/Perspectiva no host nativo

A alternância de câmera existente na versão React foi migrada para o menu **Visualizar** do host WinUI.

**Vista superior** mantém o alvo e a distância atuais da câmera, mas coloca o pitch próximo de 90° com uma pequena margem para evitar a singularidade do `CreateLookAt` quando a direção da câmera fica exatamente paralela ao eixo Y.

**Perspectiva** restaura a orientação padrão do editor sem alterar o ponto focado nem o zoom atual. Assim é possível trabalhar no mesmo tile/objeto alternando entre inspeção ortográfica aproximada por cima e navegação 3D.

Os dois modos reutilizam a mesma câmera, o mesmo ID Buffer e os mesmos raycasts; não existe viewport paralelo ou estado React oculto.

### Checkpoint N3.26 — visibilidade real de Terreno, Objetos e Splines

Os controles de visibilidade da versão React foram migrados para o menu **Visualizar** do host WinUI.

**Terreno**, **Objetos** e **Splines** podem ser mostrados ou ocultados de forma independente sem descarregar o mapa nem reconstruir o snapshot do Core.

A visibilidade também passa a controlar o ID Buffer. Quando Objetos ou Splines são ocultados:
- a geometria correspondente deixa de ser desenhada;
- proxies e triângulos reais daquela categoria deixam de entrar no picking buffer;
- hover e seleção da categoria são removidos;
- gizmos deixam de permanecer ativos sobre uma entidade invisível;
- seleção via Explorer é recusada enquanto a categoria estiver oculta.

Isso evita o problema de uma entidade invisível continuar bloqueando o clique em algo visível atrás dela.

O terreno não participa do ID Buffer de objetos/splines; ocultá-lo altera apenas a renderização. As ferramentas de edição de terreno continuam usando o raycast matemático contra a malha real carregada.

### Checkpoint N3.27 — filtro de seleção nativo por categoria

O filtro de seleção da versão React foi migrado para a barra superior do host WinUI.

O usuário pode alternar entre:
- **Selecionar: todos**;
- **Só objetos**;
- **Só splines**.

O filtro não altera a visibilidade da cena. Ele reconstrói apenas o ID Buffer usado por hover/click, removendo as categorias excluídas. Assim, por exemplo, em **Só splines** um objeto visualmente na frente não impede que a spline atrás dele seja selecionada.

Seleção atual, hover e gizmos são limpos quando deixam de ser válidos para o novo filtro. A seleção via Explorer também respeita o mesmo filtro, evitando estados diferentes entre lista e viewport.

O modo de terreno continua separado porque a edição de terreno no host nativo usa o raycast próprio da ferramenta de nivelamento/pintura, não o ID Buffer de objetos e splines.

### Checkpoint N3.28 — grade independente dos guias de cena

O controle **Grade** da versão React foi migrado para o menu **Visualizar** do host WinUI.

O antigo buffer de linhas do renderer foi separado em três conjuntos:
- grade/bordas dos tiles e subdivisões do terreno;
- marcadores-guia de objetos;
- linhas-guia de splines.

Com isso, desativar **Grade** oculta somente a malha de referência do terreno. Marcadores de objetos e linhas-guia de splines continuam acompanhando a visibilidade das respectivas categorias.

A mudança não reconstrói o mapa nem altera picking, materiais ou geometria OMSI; apenas habilita/desabilita o buffer de linhas da grade no Direct3D 11.

### Checkpoint N3.29 — paridade de atalhos de visualização e foco da seleção

A barra nativa passa a migrar os atalhos rápidos usados pela versão React:

- **F** foca a câmera no objeto ou spline selecionado;
- **N** alterna o snap de transformação;
- **T** mostra/oculta o terreno;
- **G** mostra/oculta somente a grade;
- **O** mostra/oculta objetos;
- **L** mostra/oculta splines;
- **P** mostra/oculta apenas os perfis 3D reais das splines.

O foco usa a própria seleção do ID Buffer e o mesmo anchor usado pelos gizmos, sem criar uma segunda câmera ou estado paralelo.

A opção **Perfis reais das splines** foi separada da visibilidade lógica de Splines. Ao desligar P, a malha SLI texturizada some, mas as linhas-guia de spline continuam visíveis e selecionáveis enquanto Splines estiver ativo. Isso replica melhor o fluxo da barra de visualização da versão React.

Os atalhos não interceptam digitação quando o foco está em TextBox, RichEditBox, PasswordBox ou NumberBox.

### Checkpoint N3.30 — modos de seleção Q/Alt+1…4 e enquadrar mapa

A seleção rápida da versão React foi migrada para o host WinUI:

- **Q** volta para seleção geral;
- **Alt+1** seleciona tudo;
- **Alt+2** filtra apenas objetos;
- **Alt+3** filtra apenas splines;
- **Alt+4** ativa seleção de terreno/tile;
- **Home** enquadra o mapa carregado.

O filtro nativo ganhou o modo `Terrain`. Nesse modo, objetos, splines e gizmos deixam de participar do ID Buffer de seleção e o clique passa a usar o raycast real contra a malha de terreno. O modo permanece ativo após cada clique até o usuário trocar de filtro.

Ao entrar em Terreno, a camada de terreno é reativada automaticamente caso estivesse oculta. O ponto selecionado continua alimentando o mesmo Inspector usado para nivelamento e pintura das máscaras DDS, então seleção e edição compartilham o mesmo estado real.

O botão **Enquadrar** e a tecla Home usam `NativeViewportNavigation.FitToScene`, preservando uma única câmera e recalculando o enquadramento a partir dos bounds reais da cena carregada.

### Checkpoint N3.31 — paridade final dos atalhos de câmera e Escape

Os atalhos restantes da barra React também foram migrados:

- **1** muda imediatamente para câmera em perspectiva;
- **2** muda para vista superior;
- **Esc**, quando não está cancelando placement nem saindo da tela cheia, retorna ao modo **Selecionar / todos**.

As teclas 1 e 2 usam exatamente os mesmos métodos nativos já expostos pelo menu Visualizar. Não existe câmera paralela nem estado duplicado entre atalho e menu.

O comportamento de Esc preserva a prioridade de segurança do host nativo: primeiro cancela ferramentas de placement/construção, depois sai da tela cheia e somente então restaura o filtro geral de seleção.

### Checkpoint N3.32 — terreno double-sided no rasterizer nativo

A validação da Alpha.4 Test 1 revelou que a malha de terreno podia existir e ser contabilizada pelo renderer, mas continuar invisível no viewport. Com o céu OMSI ativo, isso ficava evidente porque as nuvens apareciam através da grade como se fossem a textura do chão.

A versão React já renderizava o terreno com `backFaceCulling = false`. O host Direct3D 11 ainda usava o rasterizer padrão, que pode descartar a face visível do terreno dependendo do winding efetivo da malha/câmera.

O passe de terreno agora usa um `ID3D11RasterizerState` dedicado com `RasterizerDescription.CullNone`. Esse estado vale somente durante o desenho do terreno e é restaurado imediatamente depois, portanto objetos, splines, céu e gizmos continuam usando seus estados normais.

Nenhum dado do `.terrain`, UV, textura ou máscara é alterado; a correção é exclusivamente de rasterização e mantém paridade com o comportamento já validado no viewport React.



## Building Studio, Road Kit e IA agnóstica de provedor

A arquitetura nativa agora também serve como base para criação de conteúdo original do próprio Map Studio.

### Road Kit próprio

O Core pode gerar um pacote de splines originais sob `Splines/MapStudio_RoadKit`, com perfis e paths OMSI reais. O pacote inicial cobre mão única, rua de duas faixas, rua com calçada, avenida de quatro faixas, avenida dividida com canteiro e via de pedestres. As texturas também são geradas pelo próprio Map Studio. Atualizações preservam a versão anterior em backup.

### Building Studio

O Core possui writer O3D editável e gerador procedural de edificações. O fluxo inicial gera `.sco` + `.o3d` reais a partir de dimensões, número de andares, tipo/altura de telhado e uma imagem opcional de fachada. O WinUI expõe esse fluxo em **Ferramentas → Building Studio** e reindexa o asset criado na biblioteca.

A geração é determinística e local. A IA não escreve O3D diretamente.

### IA agnóstica de provedor

`MapStudio.Core.AI` define contratos neutros para análise de imagens de edificações, vias e cenas. Nenhum fornecedor é autoridade do domínio e nenhum SDK específico é exigido pelo Core.

Regras:

- o usuário poderá conectar o provedor que preferir;
- provedores de nuvem, locais/offline e endpoints personalizados usam o mesmo contrato;
- adaptadores podem ser adicionados sem alterar Building Studio, Road Kit ou formatos OMSI;
- a IA devolve sugestões estruturadas, por exemplo dimensões, andares, telhado, materiais e polylines de vias;
- valores recebidos são normalizados/validados antes de chegar aos geradores;
- o usuário revisa e pode editar os parâmetros antes de gerar;
- sem provedor conectado, todos os geradores continuam funcionando manualmente;
- não existem respostas simuladas em produção;
- chaves/tokens não devem ser gravados no mapa, no `.sco`, no `.o3d` ou em assets gerados;
- o resultado final deve continuar sendo dado OMSI real, inspecionável e independente do serviço de IA usado.


## Paridade avançada e geração procedural de vias

A migração nativa agora também cobre os fluxos avançados que permaneciam somente no React:

- **Estrada fácil com prévia editável antes de salvar**: início/fim podem ser ajustados por coordenadas, offset de curva é atualizado em tempo real e a spline só é persistida após confirmação explícita;
- **catálogo pesquisável de mapas instalados**;
- **drag-and-drop da biblioteca para o mapa**;
- **thumbnails geométricos reais e persistentes** para assets SCO/SLI no Explorer, gerados a partir da própria geometria nativa;
- **nivelamento de spline pela altura real do terreno**;
- **[spline_h] nativo** com criação, cópia e validação de template;
- **camadas de pintura do terreno** ocultáveis individualmente.

### Grafo procedural de vias

O gerador procedural usa um único grafo independente do formato OMSI.

Entradas atualmente suportadas:

1. traçado manual por cliques no terreno;
2. GeoJSON georreferenciado (`LineString` e `MultiLineString`);
3. OSM XML georreferenciado;
4. saída estruturada de IA por provedor conectado.

O grafo:

- detecta cruzamentos X e T;
- divide as vias nos nós de interseção;
- preserva metadados de faixa, largura e mão;
- suaviza polylines antes de montar o grafo persistente, preservando os pontos de controle;
- cria preview D3D11 sobre a altura real do terreno usando a mesma geometria que será persistida;
- planeja auto-link em nós lineares seguros de grau 2, inclusive quando a continuidade veio de traçados separados;
- não atravessa nós de grau 3/4 com `Previous/Next`; esses nós seguem para junctions próprios.

### Road Kit e junctions próprios

O Map Studio gera seu próprio Road Kit e também assets de junction procedurais.

O fluxo atual:

1. traçado → grafo;
2. grafo → requests OMSI;
3. preview D3D11;
4. geração/atualização do Road Kit;
5. gravação das splines em batch transacional;
6. aplicação de auto-links lineares;
7. geração de junctions próprios conforme a topologia;
8. inserção dos junctions como scenery objects;
9. rollback das vias se a etapa de junction falhar.

### Importação GeoJSON

O GeoJSON utiliza a âncora de `.mapstudio/georeference.json`.

A projeção geográfica é mantida no Core e não depende do OMSI, para poder ser reutilizada por adapters de outros simuladores.

A importação lê, quando disponíveis:

- `highway`;
- `lanes`;
- `oneway`;
- `width`;
- `name`.

Os dados são classificados para perfis do Road Kit, entram primeiro no preview e só são persistidos quando o usuário escolhe **Gerar vias**.

### Edifícios OSM e multipolygons

A importação de edificações lê footprints `way` e relações `type=multipolygon` com `building=*`. Ways externos que formam um anel contínuo são montados em um único footprint, preservando metadados como `building:levels`, `height`, `roof:shape`, `roof:height`, nome e endereço.

Relações com anéis `inner` ainda são recusadas de forma conservadora, porque preencher um pátio/furo como volume sólido produziria geometria incorreta. Relações malformadas ou com referências ausentes também não são persistidas.

### Perfis de IA

O menu **IA → Configurar provedores...** armazena apenas:

- nome do perfil;
- adapter ID;
- endpoint;
- modelo;
- indicador local/offline.

Nenhuma chave/token é gravada nesse JSON. No host Windows, credenciais locais são armazenadas separadamente no Windows Credential Manager; um backend comercial futuro poderá resolver credenciais próprias sem colocar secret key no desktop.

Os adapters atualmente implementados no host são `openai-compatible`, `ollama` e `lmstudio`. Outros provedores permanecem extensíveis pelo contrato neutro e não devem ser anunciados como suportados antes de existir adapter real.


## Gerenciamento nativo de tiles

O host WinUI agora cria tiles OMSI reais sem depender do editor antigo.

### Criar tile

**Mapa → Criar tile...**:

- usa `template/NewMap` da fonte ativa: o **template próprio do Workspace Map Studio** no modo standalone ou o template da instalação quando a fonte opcional OMSI estiver ativa;
- copia o `.map`, `.terrain`, lightmap e demais arquivos auxiliares associados ao tile do template;
- não copia `.prt`, pois é um arquivo temporário regenerável do editor;
- acrescenta uma nova seção `[map]` ao fim de `global.cfg`, preservando a ordem e os índices existentes;
- recusa coordenada ou caminho já existente;
- cria backup do `global.cfg`;
- remove os arquivos recém-criados e restaura o global em caso de falha;
- foca o tile novo após recarregar o mapa.

### Excluir tile

A exclusão inicial é propositalmente conservadora.

**Mapa → Excluir tile ativo...** só grava quando todas estas condições são satisfeitas:

- o mapa possui mais de um tile;
- o tile está vazio de objetos e splines;
- o tile corresponde à última entrada válida `[map]`;
- nenhum `[entrypoints]` referencia o índice daquele tile;
- a lista de seções `[map]` é canônica e pode ser preservada sem reindexação.

Todos os arquivos `tile_X_Y.map*` são copiados para `.mapstudio-backups` antes da remoção. Se qualquer etapa falhar, `global.cfg` e arquivos do tile são restaurados.

A remoção arbitrária de tiles intermediários continua bloqueada até existir um reindexador validado para referências dependentes de índice em `global.cfg` e dados operacionais.


## Workspace standalone e fonte OMSI opcional

O editor nativo agora inicia em um **Workspace Map Studio próprio**, sem exigir que o OMSI esteja instalado.

O Workspace é uma raiz de conteúdo compatível com o pipeline OMSI e cria automaticamente:

- `maps`;
- `Sceneryobjects`;
- `Splines`;
- `Texture`;
- `template/NewMap`;
- metadados privados em `.mapstudio`.

O template de mapa e o terreno inicial são gerados pelo próprio Map Studio. O Workspace também semeia assets próprios quando ainda não existirem: Road Kit, túnel inicial, junction inicial e uma casa inicial gerada pelo Building Studio.

**Arquivo → Novo mapa...** cria e abre um mapa diretamente no Workspace. **Arquivo → Adicionar pasta de itens...** copia packs SCO/SLI para a biblioteca standalone, preservando a estrutura interna do pack.

**Arquivo → Importar mapa existente...** aceita uma pasta externa com `global.cfg`, valida o mapa pelo Core, copia o projeto para `Workspace\\maps` e o abre em modo standalone. Dependências SCO/SLI externas podem ser adicionadas depois por **Adicionar pasta de itens...**.

**Abrir OMSI** continua disponível no menu, na ribbon desktop e na barra do modo tela cheia. Ele apenas troca a fonte ativa para uma instalação OMSI escolhida pelo usuário; não é requisito de inicialização nem de criação/edição.

A UI mostra explicitamente `WORKSPACE` ou `OMSI` para evitar ambiguidade sobre a fonte ativa. A barra superior foi reorganizada em menu + ribbon de comandos, e o fullscreen possui barra própria com salvar/desfazer/refazer, novo mapa, Assets, Explorer, Inspector, Workspace e Abrir OMSI.


## Estado consolidado da migração nativa — 21/09/2026

> As frases “próximo checkpoint”, “ainda não inclui” e equivalentes nas seções históricas acima descrevem o estado no momento em que cada checkpoint foi criado. Elas não devem ser interpretadas como lista atual de pendências. Esta seção é a referência consolidada mais recente.

A arquitetura de produção do editor é **WinUI 3 + Direct3D 11 + MapStudio.Core**. A antiga proposta de manter React/WebView2 como viewport principal foi superada pela migração nativa e permanece documentada apenas como histórico da decisão.

Além dos checkpoints já descritos, o host nativo atualmente possui:

- criação, exclusão segura e gerenciamento visual de tiles;
- importação local de relevo por CSV e ESRI ASCII Grid;
- brush incremental para elevar/abaixar terreno, nivelamento e pintura real das máscaras DDS;
- visibilidade individual das camadas `groundtex`;
- thumbnails geométricos persistentes e cards visuais da biblioteca;
- preview visual nativo de texturas BMP/PNG/JPG/TGA e DDS compatíveis, incluindo máscaras DDS A8 sem depender de codec WIC;
- preview 3D nativo também para modelos avulsos `.o3d` e `.x`, sem depender de um SCO wrapper;
- o Workspace standalone semeia conteúdo original próprio: Road Kit, túnel, Bridge Kit, cruzamento, prédio, árvore, arbusto, poste com `light_enh`, banco, placa de ponto de ônibus, caixa de utilidades e semáforo editável, sem exigir arquivos do OMSI 2;
- o Criador de Pontes nativo gera SLI/texturas próprias, cria backup ao sobrescrever uma ponte existente e abre o resultado diretamente no fluxo de Estrada fácil;
- o semáforo inicial inclui programa de 60 s com fases OMSI reais e path ligado por `use_traffic_light`, portanto aparece no editor nativo de programas depois de colocado no mapa;
- dock inferior de criação adaptável/rolável e móvel, agora separado visualmente em **Editar / Criar / Cenário / Sistemas**, com indicação da ferramenta ativa e acesso direto aos criadores de ponte e túnel;
- barra do modo F11 usa controles compactos e uniformes para projeto, edição, painéis, Workspace e atalho para abrir OMSI;
- o painel Tráfego oferece **Adicionar semáforo...**, abrindo diretamente a sinalização da biblioteca para posicionamento;
- posicionamento de objetos com opção de encaixar/alinha à via mais próxima, usando a tangente real de splines retas ou curvas e alcance configurável;
- Easy Road com preview editável, confirmação explícita, snap e auto-link linear seguro;
- geração procedural de vias com suavização, grafo único, auto-link de continuidade em grau 2, junctions próprios e rollback;
- importação georreferenciada de GeoJSON e **OSM XML**, incluindo vias, footprints de edifícios, multipolygons externos seguros e vegetação por pontos, linhas, áreas e multipolygons externos seguros;
- análise de vias da referência Google por IA conectável;
- Building Studio com O3D/SCO próprios, telhados plano/duas águas/hip/shed e aberturas de fachada;
- configuração e teste de provedores de IA compatíveis com o contrato neutro do Core;
- adapters de visão reais para **OpenAI-compatible**, **Ollama**, **LM Studio**, **Anthropic Messages API** e **Google Gemini generateContent**, todos convergindo para o mesmo parser estruturado de edifícios e vias;
- o diálogo nativo mostra quais adapters são operacionais, sugere endpoints seguros para serviços conhecidos e mantém API keys no Windows Credential Manager;
- o Workspace standalone gera céu diurno/noturno próprio e texturas base/detail de terreno próprias; mapas novos já recebem uma camada `[groundtex]` funcional sem conteúdo do OMSI;
- **Arquivo → Exportar mapa como pacote OMSI...** cria uma árvore independente com `maps`, `Sceneryobjects`, `Splines` e texturas do Workspace para teste/distribuição; a instalação original do OMSI nunca é alterada automaticamente;
- o exportador exclui deliberadamente `himmel01/himmel05` e `Texture/skybox` do pacote para não sobrescrever o ambiente global do jogo, mas preserva as texturas de terreno e os assets necessários do Workspace;
- leitura de `attachObj`, `splineAttachment/splineAttachement` e repeaters;
- edição preservativa dos campos numéricos validados desses attachments, com backup e round-trip;
- camada comercial/entitlements em modo Development Preview, ainda sem enforcement;
- fronteira formal para adapters de simuladores; OMSI 2 permanece o único adapter operacional neste momento.

### Limitação preservada: mapas `[worldcoordinates]`

Inserção/cópia de objetos e splines continua bloqueada em mapas que usam `[worldcoordinates]`. Essa restrição já existia no editor React anterior e não representa regressão da migração nativa.

O renderer atual ainda constrói o espaço de edição a partir de tile × 300 m + coordenada local. Liberar escrita em `[worldcoordinates]` sem uma conversão formal e testada poderia persistir coordenadas incorretas. A restrição só deve ser removida quando leitura, visualização, picking, placement e round-trip de gravação estiverem cobertos por testes específicos.

### Importação OSM

**Ferramentas → Gerador procedural de vias → Importar OSM XML georreferenciado...** lê arquivos locais `.osm`/`.xml` sem executar DTD ou resolver entidades externas.

O importer reconhece ways com `highway`, resolve seus nodes e preserva quando disponíveis:

- `lanes`;
- `oneway`, incluindo `-1` com inversão do sentido da geometria;
- `width`;
- `name`;
- classificação `highway`.

As coordenadas WGS84 são projetadas pela mesma âncora de `.mapstudio/georeference.json` usada pelo GeoJSON. Depois disso, OSM, GeoJSON, traçado manual e IA alimentam exatamente o mesmo `MapStudioRoadGraph`, preview D3D11, Road Kit, planner de junctions e persistência transacional.

**Ferramentas → Importar edifícios OSM...** importa footprints `building=*` por way e também relations `type=multipolygon` quando os anéis externos podem ser montados de forma segura. Relações com anéis internos/courtyards continuam recusadas nesta etapa para evitar preencher áreas vazadas incorretamente. O gerador preserva altura/andares e `roof:shape`/`roof:height`: Gable permanece restrito a footprints quadriláteros; Hip — incluindo `pyramidal`/pyramid — é gerado em qualquer footprint convexo simples com 3 ou mais lados; Shed/Skillion usa um plano inclinado e também é gerado em footprints simples irregulares ou côncavos. Quando uma forma não puder ser construída com segurança, o topo permanece plano.

**Ferramentas → Importar vegetação OSM...** importa nodes individuais `natural=tree` e `natural=shrub`, ways `natural=tree_row` e `barrier=hedge`, além de ways fechados `natural=wood`, `landuse=forest` e `natural=scrub`. `species`, `genus`, `leaf_type` e `name` são preservados quando presentes.

Tree rows e hedges são projetados pela mesma âncora geográfica e amostrados em pontos regulares de 4 m. Áreas de floresta/bosque recebem dispersão determinística de aproximadamente 14 m e áreas de scrub de 7 m, sempre mantendo os pontos dentro do polígono OSM. O diálogo permite escolher **dois SCOs reais** já indexados na categoria Vegetação: um para árvores/tree rows/floresta e outro para arbustos/hedges/scrub. O viewport mostra a prévia sobre o terreno carregado; a confirmação alinha cada item à altura real do terreno e grava os grupos em uma única transação multi-batch com backup. O limite operacional permanece 256 pontos selecionados por execução e a rotação variada continua opcional e determinística por ID/amostra.

Relations OSM `type=multipolygon` de floresta/bosque/scrub agora são aceitas quando podem ser montadas apenas com **outer rings** seguros; os ways consumidos pela relation não são duplicados como áreas standalone. Relações com `inner` continuam recusadas nesta etapa para não preencher holes incorretamente.

O diálogo também usa `species`, `genus` e `leaf_type` como **sugestão** para pré-selecionar o SCO real mais compatível já existente na biblioteca. Essa heurística nunca cria assets nem bloqueia a escolha manual do usuário; uma seleção de vegetação já ativa na biblioteca continua tendo prioridade. Pontos sem terreno atualmente carregado também não são inseridos, evitando alturas inventadas.

### Attachments OMSI

O reader preserva as variantes de attachment reconhecidas sem alterar seções desconhecidas. O editor só habilita escrita para uma seção que foi parseada validamente e verifica se os valores brutos originais continuam iguais antes de aplicar a edição.

Atualmente podem ser editados:

- rotação, pitch e bank de `attachObj`;
- X/Z/Y, rotação, pitch, bank, intervalo e distância de spline attachments/repeaters.

Continuam somente leitura nesta etapa:

- IDs;
- asset path;
- parent ID;
- attach point;
- `varparent`;
- campos desconhecidos/extra.

A persistência passa por `SafeFileTransaction`, cria backup e relê o tile antes de atualizar o snapshot.
