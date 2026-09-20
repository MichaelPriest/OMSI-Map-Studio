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

