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
