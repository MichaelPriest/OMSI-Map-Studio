# Interface do usuário

[English](../en/UI.md) · **Português (Brasil)**

A interface principal da Alpha.3 segue a direção visual do conceito aprovado pelo projeto: navegação lateral permanente, telas dedicadas para configuração da instalação e abertura manual de mapas, e um editor de três áreas.

## Navegação

A barra lateral contém:

- Início;
- Abrir OMSI;
- Abrir mapa;
- Explorador;
- Ferramentas;
- Configurações.

Itens ainda não implementados podem aparecer como estrutura visual, mas não devem apresentar dados ou ações fictícias.

## Fluxo

1. **Abrir OMSI** registra a pasta raiz da instalação.
2. **Abrir mapa** permite escolher manualmente uma pasta dentro de `maps`.
3. Após abrir o mapa, a interface entra no **Editor**.

Nenhum mapa é carregado automaticamente ao selecionar a instalação.

## Editor

O editor é dividido em:

- **Explorador**, à esquerda, com contagens reais de objetos, splines e tiles;
- **Viewport**, ao centro, renderizado com Babylon.js;
- **Inspetor**, à direita, com propriedades do mapa ou do objeto selecionado;
- **Barra de status**, com contagens reais do mapa aberto.

As categorias ainda não suportadas, como terreno e rotas, aparecem claramente como “em desenvolvimento”.

## Inspetor de objeto

Quando um objeto é selecionado, o inspetor expõe abas:

- Geral;
- Transformação;
- Geometria;
- Materiais.

Todas as informações exibidas vêm dos arquivos reais do OMSI. Materiais usam dados O3D reais já interpretados pelo Core.

## Regra visual

A interface pode seguir o conceito visual aprovado, mas nunca deve apresentar miniaturas, mapas, contagens ou estados inventados para parecer completa. Estados sem suporte devem ser vazios, desabilitados ou identificados como em desenvolvimento.


## Superfície-base do viewport

Enquanto o formato binário `.terrain` ainda não é interpretado, os tiles existentes recebem uma superfície-base neutra do editor. Ela serve apenas para leitura espacial e não representa o relevo nem a textura real do terreno.

Tiles ausentes continuam sem preenchimento e destacados separadamente. Os eixos de splines e marcadores de objetos são desenhados acima dessa superfície.


## Modo desempenho 3×3

Quando o usuário ativa **Modo desempenho 3×3**, o editor trabalha com um **tile ativo** e uma janela padrão de 3×3 tiles ao redor dele. O `global.cfg` continua fornecendo a topologia completa do mapa, mas objetos, splines e outros dados pesados são carregados somente nessa área.

Clicar em outro tile visível muda o centro da área ativa. Tiles já lidos permanecem em cache durante a sessão para que voltar a uma área anterior não exija nova leitura do disco.

Contagens exibidas para objetos e splines na árvore/status são identificadas como contagens da **área ativa**, não do mapa inteiro.


## Seleção e inspeção de splines

Os eixos azuis das splines da área ativa são clicáveis. Quando uma spline é selecionada:

- o eixo selecionado muda de destaque;
- o inspetor mostra arquivo `.sli`, ID, encadeamento, tile, posição, rotação, comprimento, raio e gradientes;
- o `.sli` é lido somente nesse momento;
- a aba **Perfil** mostra texturas declaradas e superfícies reconhecidas;
- quando existem pares válidos de `[profilepnt]`, o viewport extruda a superfície real da spline ao longo do traçado.

A geometria usa material neutro nesta alpha. O nome real da textura é exibido no inspetor, mas a imagem ainda não é aplicada.


## Modos de carregamento do mapa

O comportamento padrão do editor agora é **Mapa completo**, seguindo a expectativa do editor padrão do OMSI:

- todos os tiles declarados são lidos;
- todos os objetos posicionados são mantidos disponíveis;
- todas as splines posicionadas são mantidas disponíveis;
- a navegação não descarta elementos apenas porque ficaram fora de uma janela 3×3;
- o carregamento usa concorrência limitada e cache por tile;
- a interface mostra progresso por número de tiles.

O antigo streaming 3×3 continua disponível como **Modo desempenho 3×3**. Ele é opcional e indicado para mapas muito grandes ou computadores com pouca memória.

Ao abrir um mapa novo, o editor sempre começa em **Mapa completo**.


## Identidade visual

O OMSI Map Studio possui ícone próprio: grade cartográfica em azul-marinho com uma rota estilizada em laranja/azul. O mesmo símbolo é usado no executável, na janela e no cabeçalho da interface. O projeto não reutiliza a marca ou o ícone oficial do OMSI.


## Objetos reais no modo Mapa completo

Depois que os tiles do mapa inteiro são lidos, a interface identifica os caminhos `.sco` únicos usados pelos objetos posicionados e carrega cada geometria O3D uma única vez.

O progresso aparece como **Modelos O3D carregados / modelos únicos**. Enquanto um modelo ainda não foi lido, suas posições continuam representadas por marcadores. Assim que a geometria chega, todos os objetos que usam aquele mesmo modelo passam a ser renderizados.

O Babylon reutiliza a geometria e os materiais por modelo e cria clones/instâncias transformadas para cada posição do mapa. Isso evita duplicar buffers de vértices para centenas de objetos idênticos.


## Ferramentas essenciais do editor

A barra de ferramentas ativa nesta etapa possui:

- **Selecionar (Q)** — seleciona objetos e splines;
- **Mover (W)** — ativa o gizmo de posição para o objeto selecionado;
- **Rotacionar (E)** — ativa o gizmo de rotação para o objeto selecionado;
- **Focar seleção (F)** — centraliza a câmera no objeto ou spline selecionada;
- **Enquadrar mapa (Home)** — retorna a câmera para o enquadramento geral;
- **Grade (G)** — mostra/oculta a superfície e os limites dos tiles;
- **Objetos (O)** — mostra/oculta objetos;
- **Splines (L)** — mostra/oculta splines.

Mover e rotacionar começam como **prévia temporária em memória**. O inspetor reflete os novos valores e a interface mostra **Prévia não salva** enquanto houver transformações pendentes.

O botão **Salvar** (ou `Ctrl+S`) grava somente essas transformações de objetos. Antes de substituir qualquer tile, o host cria uma cópia em `.mapstudio-backups/<timestamp>/` dentro da pasta do mapa. O botão ↶ descarta todas as transformações temporárias ainda não salvas.

Escala continua desabilitada nesta etapa porque objetos posicionados do OMSI não possuem um campo geral de escala equivalente aos campos de posição/rotação usados pelo editor.


## Salvamento seguro de transformações

O salvamento desta etapa é intencionalmente restrito a objetos `[object]` já existentes.

Para cada objeto alterado, o editor preserva a identificação original da seção no tile. Ao salvar:

1. o host reabre o arquivo `.map` atual diretamente do disco;
2. confirma que a seção, ID e caminho `.sco` ainda correspondem ao objeto editado;
3. altera somente as linhas de X, Y, Z, rotação, pitch e bank;
4. preserva comentários, seções desconhecidas, valores extras, encoding, BOM e estilo de quebra de linha;
5. cria backups de todos os tiles envolvidos;
6. somente depois faz a substituição atômica dos arquivos;
7. invalida o cache e recarrega o mapa gravado.

Se a identidade do objeto mudou desde a abertura do mapa, o lote é cancelado com conflito em vez de sobrescrever o arquivo.


### Histórico e edição numérica

As transformações temporárias possuem histórico de até 100 ações:

- `Ctrl+Z` desfaz a última transformação;
- `Ctrl+Y` ou `Ctrl+Shift+Z` refaz;
- ✕ descarta todas as prévias pendentes;
- após um Save confirmado, o histórico temporário é limpo.

Na aba **Transformação** do inspetor, X, Y, Z, rotação, pitch e bank também podem ser digitados diretamente. O valor entra na prévia ao pressionar Enter ou sair do campo, usando o mesmo histórico de desfazer/refazer do gizmo.


## Navegação de câmera e Snap

O viewport oferece dois modos rápidos de câmera:

- **Perspectiva (1)**;
- **Topo (2)**.

O botão **Snap (N)** ativa/desativa quantização das transformações. Os valores padrão são:

- movimento: **0,5 m**;
- rotação: **5°**.

Os dois valores podem ser alterados diretamente na barra do viewport e são aplicados aos gizmos Babylon.

## Busca de objetos posicionados

O Explorador possui busca real sobre os objetos carregados. É possível localizar uma instância por:

- nome do arquivo `.sco`;
- caminho completo;
- ID do objeto;
- coordenada do tile (`x,y`).

A lista exibe no máximo 250 linhas por vez para manter a interface leve. Clicar em uma entrada seleciona o objeto e foca a câmera. Objetos com transformação pendente aparecem marcados como **alterado**.

## Biblioteca de objetos

A aba **Biblioteca** lista arquivos `.sco` reais encontrados em `OMSI 2/Sceneryobjects`.

A varredura:

- só começa quando a aba Biblioteca é aberta;
- ignora reparse points;
- ignora diretórios inacessíveis;
- roda fora da thread principal da interface;
- fica em cache enquanto a mesma instalação OMSI permanece selecionada;
- possui limite de segurança de 50.000 entradas;
- mostra no máximo 300 resultados por vez na interface.

A busca aceita nome e caminho do arquivo. A ação **Colocar** usa o fluxo preservativo de inserção e só permite confirmação quando os parâmetros do bloco `[object]` podem ser derivados com segurança.


## Colocação de objetos pela Biblioteca

A Biblioteca agora possui a ação **Colocar**.

Fluxo:

1. abra **Biblioteca**;
2. escolha um arquivo `.sco` real;
3. clique **Colocar**;
4. clique em um tile existente no viewport;
5. a posição X/Y é calculada em coordenadas locais do tile e respeita o Snap;
6. ajuste **Z**, **Rotação**, **Pitch** e **Bank** na barra de colocação;
7. confirme com **Confirmar e salvar**.

A prévia usa a geometria O3D real quando disponível. Se o O3D não puder ser interpretado, o ponto de colocação continua visível como marcador.

### Limite conservador desta alpha

A gravação de um novo `[object]` só é permitida quando o mesmo `.sco` já existe em algum ponto do mapa.

Isso é intencional: blocos `[object]` podem possuir valores extras cuja quantidade depende do tipo de objeto. O Map Studio copia o `HeaderValue` e os valores extras de uma instância real do mesmo `.sco`, evitando inventar parâmetros.

Um `.sco` instalado mas nunca usado no mapa pode ser selecionado e pré-visualizado, porém **Confirmar e salvar** fica bloqueado no modo Mapa completo. Em modo desempenho, o host faz a verificação global ao confirmar.

O novo objeto recebe backup automático do tile antes da gravação.


## Cópia segura do objeto selecionado

Na aba **Geral** do inspetor, **Colocar cópia** inicia uma nova colocação usando o mesmo arquivo `.sco` real do objeto selecionado.

O próximo clique no viewport define X/Y. Como ponto de partida, a cópia preserva Z, rotação, pitch e bank da seleção atual — inclusive quando a seleção já contém uma prévia de transformação ainda não salva.

A confirmação reutiliza exatamente o mesmo pipeline seguro da Biblioteca: template real do mesmo `.sco`, novo ID global, backup do tile e escrita atômica. O comando não duplica texto antigo do tile nem cria um writer alternativo.


## Excluir objeto

A aba **Geral** do inspetor possui **Excluir objeto**. A exclusão é persistente e exige confirmação.

Para evitar perda de trabalho, o comando fica bloqueado enquanto existir prévia de transformação não salva ou colocação de objeto em andamento. O host valida ordinal, ID e caminho `.sco` novamente no tile atual em disco e cria backup antes da troca atômica.


## Edição numérica de spline

Ao selecionar uma spline, a aba **Traçado** permite criar uma prévia para X, Y, Z, rotação, comprimento, raio e gradientes inicial/final.

**Salvar spline** persiste a prévia com backup automático. IDs e vínculos **Anterior / Próxima** permanecem somente leitura nesta etapa. A prévia da spline é independente do histórico de desfazer/refazer de objetos e possui **Descartar prévia** próprio.


Enquanto existir uma prévia de spline não salva, o editor também bloqueia **Colocar**, **Colocar cópia** e **Excluir objeto**, evitando descarte indireto da prévia ao recarregar o mapa.


## Gizmo visual para splines

Com uma spline selecionada, **W** ativa o gizmo de movimento e **E** ativa o gizmo de rotação. O movimento permite ajustar X/Y/Z; a rotação visual fica restrita ao eixo vertical, correspondente ao campo de rotação do formato OMSI.

O eixo e o perfil selecionados acompanham o gizmo em tempo real. A alteração só entra no estado de prévia quando o arraste termina, evitando gravações ou atualizações React a cada frame. Snap de movimento/rotação usa os mesmos valores configurados na barra do viewport.

O botão global **Salvar** e `Ctrl+S` também salvam prévias de spline. O botão global ✕ descarta a prévia de spline quando ela for a edição pendente. ↶/↷ continuam exclusivos do histórico de objetos nesta etapa.


## Colocar cópia de spline

Na aba **Geral** de uma spline, **Colocar cópia desconectada** usa a spline selecionada como template real.

O fluxo:

1. selecione uma spline existente;
2. clique **Colocar cópia desconectada**;
3. clique em um tile para escolher o novo ponto inicial;
4. ajuste Z, rotação, comprimento, raio e gradientes;
5. confirme **Confirmar e salvar**.

O host relê a spline-fonte do disco e valida ordinal, caminho `.sli`, ID, tipo e vínculos antes da criação. A cópia preserva `HeaderValue`, tipo `[spline]`/`[spline_h]` e valores extras reais da fonte, recebe um novo ID global e é criada com `previous = -1` e `next = -1`.

A decisão de iniciar desconectada é deliberada: esta etapa não reescreve automaticamente a cadeia de splines vizinhas.


## Excluir spline

A aba **Geral** permite excluir splines desconectadas ou conectadas.

Para uma spline conectada, o host valida a reciprocidade dos vizinhos, libera as pontas que apontam para a spline e remove a seção fonte no mesmo lote transacional. Todos os tiles afetados recebem backup sob o mesmo timestamp.

Se qualquer spline vizinha mudou, desapareceu ou deixou de apontar reciprocamente para a fonte, a exclusão inteira é cancelada. Comentários, linhas em branco e seções desconhecidas continuam preservados.


## Busca de splines no Explorer

A busca principal do Explorer agora pesquisa objetos e splines carregados. Para splines, a busca aceita nome do arquivo `.sli`, caminho completo, ID e coordenada do tile.

Objetos e splines aparecem em seções separadas, cada uma limitada a 250 linhas renderizadas. Clicar em uma spline seleciona a instância real e foca a câmera; uma spline com prévia pendente aparece marcada como **alterada**.


## Editor de vínculos previous/next

Na aba **Geral** da spline, **Vínculos da cadeia** permite informar o ID anterior e o próximo. Use `-1` para deixar uma ponta livre.

A lista do campo sugere splines atualmente carregadas, mas o host pesquisa e valida IDs no mapa completo. Ao salvar, ele também altera reciprocamente as pontas dos vizinhos antigos e novos. Se uma ponta nova já estiver ocupada, se um vizinho tiver desaparecido ou se a cadeia atual estiver inconsistente, nenhuma parte da transação é gravada.

**Desconectar rascunho** apenas coloca `-1/-1` nos campos; a alteração só é persistida ao clicar **Salvar vínculos**.


## Biblioteca de Splines instaladas

A guia **Splines** no Explorer varre `OMSI 2/Splines` sob demanda e permite buscar até 50.000 arquivos `.sli` por nome/caminho. A lista renderiza no máximo 300 resultados.

Cada arquivo oferece **Normal** e **Altura**. A prévia usa o perfil real do `.sli`, começa desconectada e oferece Z, rotação, comprimento, raio e gradientes antes da confirmação.

A UI não inventa header/cant/skew/delta_h. No **Confirmar e salvar**, o host procura no mapa completo um template real neutro do tipo escolhido: cinco extras numéricos explícitos zerados para `[spline]` normal ou seis para `[spline_h]`. Se não existir, a prévia continua utilizável, mas a gravação é recusada com mensagem de template indisponível.


## Texturas O3D e de splines

Ao selecionar um objeto com material O3D texturizado, o Map Studio solicita somente as texturas usadas pelos meshes daquela seleção. O mesmo vale para as texturas declaradas em `[texture]` do perfil de uma spline selecionada ou em colocação.

BMP, PNG, JPG/JPEG, GIF, WebP, DDS e TGA são resolvidos dentro da instalação real do OMSI. DDS/TGA usam os loaders do Babylon. Arquivo ausente, caminho inseguro ou textura acima de 16 MiB mantém o material com a cor O3D/perfil já existente em vez de criar uma imagem fake.

As texturas carregadas ficam em cache no React e podem aparecer também em outras instâncias do mesmo objeto/spline enquanto a sessão estiver aberta.


## Estado das texturas no inspetor

A aba **Materiais** de objetos mostra agora o estado do asset real por material: **Carregada**, **Carregando**, **Arquivo ausente**, **Acima de 16 MiB**, **Acesso negado**, **Falha de leitura** ou **Sem textura**. Quando carregada, a extensão real também aparece.

O painel **Perfil** de splines apresenta o mesmo estado ao lado de cada superfície. Isso permite distinguir imediatamente uma geometria sem textura declarada de uma textura que existe no `.sli`/O3D mas não pôde ser resolvida.

Os textos antigos de “Somente leitura” foram removidos da interface: a Alpha.3 trabalha em modo de **edição preservativa** dentro das limitações documentadas.


## Texturas progressivas no mapa

O mapa agora pode receber texturas reais progressivamente mesmo sem selecionar cada instância. O prefetch prioriza tipos de objetos e splines mais próximos do tile ativo.

O limite automático é de **24 texturas por mapa/sessão**: 16 de objetos e 8 de splines. A barra inferior mostra `Texturas auto: X/24`. Seleções e prévias continuam carregando suas texturas independentemente desse limite.

Perfis `.sli` próximos também são preparados progressivamente, um por vez, para permitir que superfícies de spline recebam textura sem disparar leitura maciça.


## Transparência definida pelo SCO

O preview O3D passa a respeitar overrides estáticos do `.sco` para `[matl_alpha]`, `[matl_noZwrite]` e `[matl_noZcheck]`.

- `matl_alpha 0`: textura opaca;
- `matl_alpha 1`: recorte de alpha (folhas/grades/placas com pixels transparentes);
- `matl_alpha 2`: transparência parcial (por exemplo vidro);
- `matl_noZwrite`: não grava profundidade;
- `matl_noZcheck`: não bloqueia o desenho pelo teste Z.

A aba **Materiais** mostra a linha `SCO:` quando um override estático foi realmente associado ao material. Materiais `[matl_change]` continuam sem estado fake porque dependem de scripts/variáveis do OMSI.


## Bumpmap O3D/SCO

Materiais estáticos com `[matl_bumpmap]` agora carregam a imagem real de bump e aplicam o fator definido no `.sco`. A aba **Materiais** mostra o nome, estado de carregamento e fator do bump.

Assim como a textura difusa, bump ausente ou inseguro não recebe placeholder: o material continua renderizado sem bump.


## Camada Nightmap

O viewport possui a camada **Nightmap**. Desligada, mantém o preview diurno. Ligada, materiais estáticos com `[matl_nightmap]` carregam a textura real como emissão.

O inspetor mostra o arquivo e o estado; com a camada desligada exibe **preview desligado**.


## Reflexão por envmap

Materiais estáticos com `[matl_envmap]` agora carregam a textura de reflexão real e respeitam a força definida no `.sco`.

A aba **Materiais** mostra arquivo, estado e força do envmap. Se o asset estiver ausente/inseguro, o material permanece sem reflexão adicional.


## Materiais dependentes de runtime

O inspetor identifica `Transmap` e `Lightmap` quando declarados no `.sco`, exibindo **runtime não simulado**.

Isso é intencional: referências `\S:`/script e lightmaps controlados por variáveis do OMSI não são tratados como arquivos estáticos nem ligados automaticamente.


## Diagnóstico de material incompleto

Quando um material usa comandos reconhecidos mas ainda não simulados, a aba **Materiais** mostra `Não simulado:` seguido dos comandos.

Atualmente isso inclui `[matl_envmap_mask]`, `[alphascale]` e `[matl_allcolor]`. O objetivo é tornar a diferença visível, em vez de silenciosamente ignorar recursos do editor/OMSI.


## Controle do cache de texturas

A barra inferior mostra `Cache: X/64`. O cache de assets reais é limitado a 64 entradas e remove automaticamente a textura menos recente quando necessário.

A área de camadas ganhou **Limpar cache**, que remove texturas carregadas, cancela o histórico de requisições em andamento e reinicia o orçamento automático de prefetch sem descarregar o mapa.


## LOD real de objetos

Objetos `.sco` com vários blocos `[LOD]` não exibem mais todos os níveis de detalhe ao mesmo tempo. O nível ativo muda conforme o tamanho projetado na tela.

Na aba **Geometria**, cada mesh mostra **Global** ou o limiar correspondente, por exemplo **LOD 0,6**, facilitando a validação.


## Perfis de spline no mapa

A camada **Perfis spline** mostra as superfícies reais das splines próximas usando o perfil `.sli`, UVs, gradiente, curva e texturas já carregadas.

A renderização é limitada ao entorno 3×3 do tile ativo e a 120 splines por cena. Desmarque **Perfis spline** para voltar ao modo leve somente com eixos.


## Estado do terreno

O inspetor do mapa mostra para o tile ativo: coordenadas, presença do marcador `[terrain]`, presença do sidecar `.map.terrain` e tamanho do arquivo.

Também exibe um resumo da área carregada com `sidecars/marcadores`, facilitando detectar tiles inconsistentes antes de existir edição binária.

## Tela cheia e navegação do viewport

O editor possui **tela cheia nativa** pelo botão da barra de ferramentas ou por **F11**. O host WPF remove a moldura do Windows e maximiza a janela; **F11** ou **Esc** retornam ao estado anterior.

A câmera do viewport mantém posição, alvo e zoom quando a cena 3D é reconstruída durante carregamento progressivo de O3D/texturas. Isso evita saltos de câmera enquanto o mapa continua sendo preenchido.

Controles de navegação:

- botão direito + arrastar: orbitar a câmera;
- botão do meio + arrastar: deslocar/panoramizar;
- Shift durante o deslocamento: movimento mais rápido;
- roda do mouse: zoom progressivo;
- setas: deslocamento pelo mapa;
- Shift + setas: deslocamento acelerado;
- `+` / `-`: aproximar/afastar;
- **F**: focar objeto ou spline selecionada;
- **Home**: reenquadrar o mapa;
- **1 / 2**: perspectiva / topo.

O botão esquerdo permanece reservado para seleção, posicionamento e gizmos de edição.

## Terreno real do OMSI

O sidecar `.map.terrain` agora é decodificado como a grade de altura real do OMSI. O formato validado usa um cabeçalho `uint32` little-endian com o número de células (normalmente 60), seguido por `(N+1)²` alturas `float32` little-endian. Para o formato padrão isso resulta em **61×61 pontos / 3.721 alturas / 14.888 bytes**.

O viewport cria uma malha 3D usando esses valores reais e espaçamento derivado do tile de 300 m. A camada **Terreno** pode ser ligada/desligada. O material atual é neutro e serve apenas para visualizar a geometria; pintura/camadas `.rdy` ainda não são simuladas.

O inspetor do mapa mostra se a malha foi decodificada, número de células/pontos e intervalo de altitude do tile ativo. Arquivos com tamanho/grade inválidos continuam visíveis como sidecars no diagnóstico, mas não recebem malha inventada.

## Textura base real do terreno

O mapa agora lê as entradas **[groundtex]** reais do `global.cfg` na ordem declarada. A camada 0 fornece a textura principal e a textura de detalhe usadas como base do terreno.

A textura principal da camada 0 é resolvida dentro da instalação real do OMSI, carregada pelo host e aplicada à malha de altura. O valor de repetição declarado no `global.cfg` é aplicado ao UV da textura. Se o arquivo estiver ausente ou o caminho for inseguro, a malha mantém o material neutro em vez de inventar um asset.

A textura de detalhe da mesma camada também é resolvida e seu estado aparece no inspetor, mas **a mistura visual de detalhe ainda não é simulada**, pois o modo/fator de blend do OMSI ainda está sendo validado. As demais camadas [groundtex] permanecem disponíveis como dados reais, porém ainda não são pintadas sem a associação confirmada com os dados `.rdy`.

O inspetor mostra quantidade de camadas [groundtex], caminho/estado da textura principal, repetição, caminho/estado da textura de detalhe e sua repetição.

## Pintura de terreno por máscara numerada

Além da camada base, o Map Studio agora detecta os arquivos reais `texture/map/<tile>.map.N.dds`. O índice **N** é associado à entrada `[groundtex]` de mesmo índice no `global.cfg`.

Cada máscara DDS do tile é carregada somente quando necessária e aplicada como opacidade sobre uma cópia da mesma malha de altura. A textura principal da camada N usa o seu próprio valor de repetição. Assim, áreas pintadas pelo editor do OMSI podem aparecer sobre a camada 0 sem substituir o relevo real.

O inspetor mostra os índices de máscara presentes no tile ativo. Caminhos inválidos, índices sem `[groundtex]` correspondente ou assets ausentes não geram camada falsa.

O arquivo `.terrain_0.rdy` também passa por um reader específico de diagnóstico. O leitor valida as seções de vértices, triângulos, materiais e transform encontradas no render-data, mas **essas coordenadas ainda não substituem a malha de altura**, porque a semântica própria do `.rdy` continua sendo validada separadamente.

A textura de detalhe declarada em `[groundtex]` continua carregada apenas para diagnóstico; o blend de detalhe ainda não é simulado.

## Controle e validação das camadas de terreno

O inspetor agora lista todas as entradas `[groundtex]` do mapa com um controle de visibilidade individual. A camada 0 pode ser ocultada para comparar a base neutra; camadas numeradas podem ser desligadas sem alterar as máscaras ou o `global.cfg`.

O painel do viewport separa **Terreno** de **Pintura terreno**. Desligar a pintura mantém a malha de altura/base disponível e remove somente as camadas controladas pelas máscaras numeradas.

O host lê o cabeçalho DDS e informa largura, altura, formato de pixel e se a textura é alpha-only. Nesta etapa, uma máscara numerada só é usada no render quando foi validada como **DDS A8 alpha-only**. Formatos diferentes permanecem visíveis no diagnóstico como **não renderizados**, em vez de serem interpretados no chute.

Os caches de terreno são limitados: até **32 texturas [groundtex]** e **96 máscaras DDS**. O botão **Limpar cache** remove texturas de objetos, splines, terreno e máscaras sem descarregar o mapa.

## Validação das máscaras DDS de terreno

As máscaras numeradas `texture/map/<tile>.map.N.dds` agora são validadas antes da renderização. O formato aceito nesta etapa é o DDS A8 observado em mapas OMSI reais: assinatura `DDS `, cabeçalho padrão, 8 bits por pixel e canal alpha de 8 bits.

Para cada máscara o Core registra largura, altura, alpha mínimo/máximo e cobertura real (percentual de pixels com alpha maior que zero). O inspetor exibe esses dados por camada.

Máscaras inválidas ou completamente vazias não são carregadas/renderizadas. Máscaras 100% opacas continuam aplicando a camada, mas o editor evita carregar um `opacityTexture` desnecessário. Isso reduz I/O e uso de material sem mudar o resultado visual.

A visibilidade da camada base (índice 0) também é respeitada no preview do editor. O arquivo original do mapa nunca é modificado por essas opções de visualização.

## Resolução esperada das máscaras de terreno

O código de resolução de cada entrada `[groundtex]` agora é convertido para a dimensão real esperada da máscara de pintura. Para camadas pintáveis, a relação validada é potência de dois: **6 → 64 px**, **7 → 128 px**, **8 → 256 px**, **9 → 512 px** e **10 → 1024 px**. A camada 0 continua sem máscara própria e usa código 0.

Ao carregar `tile.map.N.dds`, o editor compara largura/altura reais com a resolução esperada da camada N. Uma máscara A8 válida, mas com dimensão incompatível, é mostrada no inspetor como incompatível e não é renderizada. Isso evita aplicar uma máscara de outro mapa/camada por engano.

## Fidelidade da textura base do terreno

A camada base do terreno usa a textura real declarada em `[groundtex]` como **albedo**. Formatos decodificados diretamente pelo WebView/Chromium, como BMP, PNG, JPEG, GIF e WebP, não são mais forçados pelo caminho de loaders de textura do Babylon; somente DDS e TGA continuam usando os loaders dedicados.

A camada 0 é tratada como opaca e o material do terreno não multiplica a textura pela iluminação arbitrária do editor. Isso evita que uma textura real carregada seja apresentada quase preta por uma combinação de loader/material/iluminação. A orientação UV e os valores de repetição continuam vindo dos dados reais já conhecidos; nenhum blend de textura de detalhe é inventado nesta etapa.


## Carregamento bloqueante

Carregamentos estruturais que alteram o estado editável do mapa agora usam uma sobreposição central animada. Durante seleção/leitura da instalação do OMSI, abertura de mapa, carregamento completo, troca da área 3×3, leitura de SCO/O3D/SLI e carregamento inicial das bibliotecas, a interface bloqueia clique, foco e atalhos de edição até o host concluir a operação.

Quando o host fornece progresso real, como no carregamento dos tiles do mapa completo e na preparação progressiva de O3D, a sobreposição exibe percentual e barra de progresso. Operações sem progresso numérico usam animação indeterminada. O bloqueio é liberado também em caso de erro para não deixar a interface presa.

O prefetch limitado de texturas continua sendo tratado como cache visual em segundo plano depois que a estrutura editável correspondente já está pronta; ele não é usado como motivo para congelar indefinidamente o editor.


## Tela cheia imersiva e ferramentas flutuantes

No modo tela cheia, as laterais e o chrome fixo do aplicativo são ocultados para o viewport ocupar toda a área útil. As funções principais ficam em um dock flutuante: selecionar, mover, rotacionar, enquadrar, focar, abrir Explorador/Inspetor, criar objeto pela Biblioteca, criar spline pela Biblioteca, snap, desfazer/refazer, salvar e controles de visibilidade.

Explorador e Inspetor continuam disponíveis como painéis flutuantes temporários, sem reduzir permanentemente a área 3D. Uma faixa de atalhos permanece visível no rodapé do viewport com Q/W/E, 1/2, N, F, Home, G, O, L, Ctrl+S, Ctrl+Z/Y e controles de mouse.

### Terreno e splines

Texturas BMP reais entregues pelo host são convertidas em memória para PNG antes de chegar ao WebView. A conversão preserva o conteúdo visual da textura e evita depender do suporte variável a BMP no caminho de textura do navegador/Babylon. O arquivo original no OMSI não é alterado.

Em mapa completo, todos os caminhos de spline realmente usados no mapa entram na fila de leitura de perfis SLI, em vez de limitar a preparação aos poucos tipos próximos ao tile ativo. O viewport pode renderizar até 500 superfícies de spline no mapa completo; o modo desempenho continua limitando perfis à área próxima. Materiais de spline são exibidos como albedo para não ficarem artificialmente escuros pela iluminação do editor.


## Upload real de textura para GPU

O diagnóstico visual do Grundorf mostrou que o host já convertia `gras.bmp` para PNG e o Inspetor recebia o asset corretamente, mas isso ainda não comprovava que a textura tinha sido criada com sucesso dentro do Babylon/WebGL.

Formatos de imagem decodificáveis pelo navegador agora usam explicitamente `Texture.CreateFromBase64String`, o caminho do Babylon destinado a payload Base64, em vez de depender de um URL `data:` genérico. DDS/TGA continuam no loader dedicado. Falhas de upload são registradas no console com extensão de origem, MIME e erro retornado pelo Babylon.

Para terreno e superfície real de spline, o preview usa a textura também como emissiva/albedo não iluminado. Isso elimina iluminação/material como causa de uma superfície quase preta sem inventar uma textura substituta. O arquivo real do OMSI continua sendo a única fonte visual.


## Carregamento contínuo sem modal por item

O bloqueio de tela inteira fica reservado às operações estruturais: selecionar a instalação do OMSI, abrir mapa, carregar mapa completo/região e carregar bibliotecas. Leitura individual de SCO, O3D, SLI e texturas continua em segundo plano e é mostrada na barra de status, sem abrir e fechar um modal para cada recurso.

## Texturas BMP por RGBA real

Além do PNG de diagnóstico, BMPs reais são decodificados pelo host para pixels RGBA e enviados ao Babylon como `RawTexture`. Isso remove o decoder de imagem do navegador do caminho crítico para terreno e splines baseadas em BMP. O arquivo original do OMSI não é alterado.

## Ícone no Windows

O executável continua incorporando `MapStudio.ico`. O processo e os atalhos agora usam também o AppUserModelID estável `MichaelPriest.OMSIMapStudio` e o próprio EXE como fonte explícita do ícone, evitando o ícone genérico na barra de tarefas e nos atalhos.


## Carregamento único e contínuo

Depois da leitura estrutural do mapa, o editor mantém **uma única animação de carregamento** durante o aquecimento dos recursos visuais da área: geometrias O3D, perfis SLI, texturas, `groundtex` e máscaras. A tela não fecha e reabre para cada item. O desbloqueio ocorre somente após uma pequena janela de estabilidade sem recursos pendentes, evitando piscar entre lotes sequenciais.

## Terreno BMP como material não iluminado

Quando a textura base real chega como RGBA, o material do terreno usa o mesmo asset também no canal emissivo. Isso garante que a textura real continue visível quando a iluminação do material está desativada. O Inspetor mantém o diagnóstico RGBA e o host inclui uma amostra de RGB médio dos pixels decodificados para separar “arquivo realmente escuro” de “problema de material/GPU”.

## Ícone Windows

O aplicativo voltou a usar a identidade padrão do próprio executável no shell do Windows, evitando um AppUserModelID personalizado não registrado quando o EXE é aberto diretamente. A janela usa URI de recurso explícita e também aplica o `MapStudio.ico` em runtime, mantendo o `ApplicationIcon` compilado como fallback.


## Árvores especiais do OMSI ([tree])

O editor agora trata os objetos especiais de árvore do OMSI separadamente do helper `.x` usado apenas pelo editor original. O bloco real `[tree]` do `.sco` fornece a definição da árvore, enquanto cada colocação `[object]` do mapa fornece em `ExtraValues` a textura efetivamente escolhida, a altura e a razão largura/altura.

No viewport, a árvore é desenhada como billboard vertical não iluminado com a textura, altura e proporção gravadas na própria colocação do mapa. Portanto, árvores com dimensões diferentes continuam diferentes no Map Studio, sem substituir os valores por médias ou mocks.

As texturas repetidas de árvores compartilham material dentro da mesma cena para evitar criar uma cópia GPU por árvore. Marcadores amarelos permanecem somente para objetos que ainda não possuem representação renderizável.


## Objetos O3D e splines completos

A visualização de O3D não usa mais o alfa difuso embutido no material como opacidade global do objeto. A transparência segue as diretivas reais do SCO, como `[matl_alpha]`, evitando que malhas válidas desapareçam quando o canal alfa do O3D não representa transparência de objeto.

O seletor de LOD do editor também passa a manter um LOD real visível mesmo quando o limiar recebido do SCO não encontra correspondência direta com a fração de tela calculada pelo preview. Isso evita o estado em que todos os meshes LOD eram desativados ao mesmo tempo.

Perfis `.sli` com três ou mais `[profilepnt]` agora geram todas as faixas entre pontos consecutivos. Antes, somente o primeiro par era usado, o que podia mostrar apenas uma parte da rua/calçada.

## Navegação lateral

Com o viewport focado, `A/D` e as setas esquerda/direita deslocam a câmera lateralmente; `W/S` e as setas cima/baixo deslocam para frente/trás. `Shift` acelera. O botão do meio continua fazendo pan e `Shift + botão direito` também desloca em vez de orbitar.


## Fluxo de mapas e atalhos de edição

Depois de conectar a pasta do OMSI 2, **Abrir mapa** mostra a lista real de mapas encontrados em `maps`. O usuário pesquisa e escolhe o mapa pelo nome; a seleção manual de pasta continua disponível como fallback.

A barra lateral pode ser recolhida. No editor, **Tudo / Objetos / Splines / Terreno** controlam o tipo clicável. Atalhos: `Alt+1..4` alternam esses filtros; `Alt+R` abre criação de rua por spline; `Alt+C` cruzamento; `Alt+O` objeto; `Alt+T` terreno; `Alt+A` água; `Alt+G` grama; `Alt+Y` árvore. Os atalhos reutilizam somente bibliotecas e assets reais da instalação.


## Edição direta, rua fácil e mapa real

No viewport, clique em um objeto ou spline para selecionar e abrir os controles de transformação. No modo Terreno, o clique marca um ponto exato para o pincel de nivelamento. `Ctrl + setas/WASD` avança um bloco/tile de 300 m.

O fluxo **Rua** permite escolher uma spline real e marcar início/fim com dois cliques. Comprimento, rotação e gradiente são calculados; **Nivelar ao terreno** recalcula Z/gradiente pelas alturas do relevo carregado.

Em **Mapa real por coordenadas**, o usuário informa sua própria chave das APIs Google, latitude/longitude e escolhe roadmap/satellite/hybrid/terrain. A imagem fica sobre o terreno como referência. A grade de elevação pode ser buscada para o tile ativo e aplicada ao `.terrain` com backup.

Na tela **Abrir mapa**, **Criar mapa real** clona o `template\NewMap` instalado e salva a âncora em metadados do Map Studio. Isso ainda não substitui a conversão oficial de `[worldcoordinates]` do OMSI.


### Navegador de blocos 3×3

O editor mostra um navegador compacto 3×3 sobre o viewport. O centro representa o tile ativo e os oito botões ao redor correspondem aos tiles vizinhos existentes. Clicar em um bloco troca o tile ativo e move a câmera para o centro daquele tile, preservando aproximadamente o zoom atual. Tiles inexistentes ficam desabilitados. `Ctrl + setas/WASD` continua disponível como alternativa de teclado.


### Prévia real de itens da biblioteca

Ao escolher um `.sco` ou `.sli`, o editor cria a prévia no centro do tile ativo usando a geometria/perfil real do arquivo. O painel da biblioteca mostra o item ativo, quantidade de meshes ou superfícies/texturas carregadas e um botão **Focar prévia**. Trocar de item substitui somente a prévia e não grava o mapa até a confirmação.


### Fluxo de colocação estilo city-builder

A colocação de itens foi aproximada do fluxo de editores city-builder, sem substituir o formato real do OMSI. Ao escolher um objeto `.sco` (incluindo árvores, água, grama e cruzamentos quando representados por assets reais), a geometria real aparece como uma **prévia 3D translúcida** e acompanha o cursor sobre os tiles válidos. O clique fixa a posição da prévia e o arquivo do mapa só é alterado ao confirmar o salvamento seguro.

Splines normais também exibem uma prévia 3D real que acompanha o cursor. No criador de rua, o usuário escolhe uma `.sli`, pressiona no ponto inicial e arrasta até o ponto final. Depois pode ajustar um controle lateral de **curvatura**, que é convertido para os parâmetros reais de arco do OMSI (`rotation`, `length` e `radius`). O nivelamento usa os pontos inicial/final reais do arco, não uma aproximação reta.

A Biblioteca possui uma janela de prévia 3D do item selecionado usando a geometria/perfil e texturas reais carregadas. Ela não cria thumbnails sintéticos.

O painel **Mapa real por coordenadas** não fica mais permanentemente sobre o viewport. Ele é aberto pelo menu superior **Mapa > Mapa real por coordenadas…** ou pela opção correspondente em **Visualizar**.

Os painéis flutuantes do viewport possuem alças de arraste: navegador de blocos, nivelamento de terreno, mapa real, barras de colocação, ferramentas/câmera, camadas e dock de tela cheia podem ser reposicionados durante a edição.

A seleção por clique percorre os pais do mesh atingido, portanto filhos de um objeto continuam resolvendo para o objeto real mesmo quando o metadado de seleção está no nó pai.

No criador de rua, depois que início e fim existem, o viewport mostra uma **esfera azul de controle** ligada ao meio do traçado. Arrastar essa esfera lateralmente altera a curvatura em tempo real, no estilo de um editor city-builder; o slider da barra continua disponível para ajuste fino. O controle visual continua gerando somente os parâmetros reais suportados pela spline do OMSI.


### Seleção durante mover/rotacionar

A geometria temporária usada pelas ferramentas **Mover (W)** e **Rotacionar (E)** mantém a identidade do objeto/spline OMSI original nos metadados de picking. Assim, clicar diretamente na prévia editável continua selecionando o mesmo item real, inclusive para árvores `[tree]` e perfis de spline, sem depender do objeto antigo que permanece no mapa até o salvamento. Isso altera apenas hit-test/seleção e não modifica coordenadas, materiais, texturas ou arquivos OMSI.


### Biblioteca de construção por grupos

As bibliotecas reais de `.sco` e `.sli` agora usam uma navegação visual no estilo city-builder. Objetos são separados em **Cruzamentos**, **Pontes**, **Casas / prédios**, **Árvores / verde**, **Transporte**, **Mobiliário**, **Infraestrutura** e **Outros**. Splines são separadas em **Ruas**, **Calçadas / caminhos**, **Trilhos**, **Pontes / túneis**, **Faixas / marcas** e **Outras**.

A classificação é derivada do nome/caminho real do asset, metadados `.sco` já lidos e da definição real `[tree]` quando disponível. Um item que não possa ser identificado com segurança permanece em **Outros**; nenhum asset é inventado ou substituído por mock.

Cada cartão possui categoria visual e ações separadas para **Prévia** e **Colocar/Criar**. **Prévia** carrega a geometria/perfil real no visualizador 3D sem iniciar uma alteração no mapa. A criação continua usando somente `.sco/.sli` reais e as regras preservativas existentes.


### Biblioteca avançada: favoritos, recentes, coleções e filtros

A biblioteca city-builder passou a manter localmente **Favoritos**, **Recentes** e **Mais usados** para objetos e splines. Também é possível criar **Coleções** personalizadas, inclusive misturando referências `.sco` e `.sli`, sem alterar a instalação do OMSI nem os arquivos do mapa.

A busca reconhece sinônimos comuns em português, inglês e alemão (por exemplo rua/road/straße, árvore/tree/baum e ponte/bridge/brücke). Grupos possuem subcategorias contextuais, como residencial/comercial/industrial, iluminação/sinalização, avenidas/estradas/mão única, ciclovias e trilhos.

Filtros técnicos permitem destacar assets usados no mapa, árvores `[tree]` realmente detectadas, geometria `.sco` já carregada e perfis `.sli` carregados. Os cartões e o inspetor mostram uso no mapa, frequência na biblioteca, subcategoria e estado do 3D/perfil. Preferências ficam no armazenamento local da interface; falhas ou limite de armazenamento nunca bloqueiam edição ou salvamento real.


### Miniaturas 3D em cache e drag-and-drop

Quando uma prévia 3D real termina de renderizar, a interface captura uma miniatura JPEG leve e a mantém em cache local (até 48 entradas). A miniatura passa a substituir o ícone genérico no cartão, deixando a biblioteca visual sem reler ou renderizar todos os assets ao mesmo tempo.

Cartões de `.sco` e `.sli` podem ser arrastados diretamente para o viewport. O drop usa o mesmo raycast do editor para converter a posição da tela em tile/coordenada real. Objetos entram no fluxo normal de colocação; splines entram como trecho normal inicial de 20 m. O salvamento continua passando pelos bridges existentes e suas regras de backup.


### Colocação em lote, linha e pincel

Objetos `.sco` possuem quatro modos de construção: **Único**, **Repetir**, **Linha** e **Pincel**. Linha distribui o asset entre dois pontos respeitando o espaçamento configurado; Pincel distribui até 256 itens em uma área circular; Repetir acumula pontos clicados antes de salvar. A opção **Rotação variada** cria variação determinística no lote.

Operações com mais de um item usam o comando nativo `insertObjectBatch`. O host valida o asset e os tiles, reserva IDs globais únicos, agrupa gravações por tile e usa uma única `SafeFileTransaction` e um único diretório de backup para toda a operação.

**Encaixar/alinha à rua** procura a spline normal mais próxima dentro do alcance configurado, projeta o ponto no eixo da spline e adota a direção local da rua. O viewport mostra até 96 ghosts simultâneos para manter a prévia leve; o lote salvo continua limitado a 256 itens.


### Matriz, círculo, lotes e presets

A colocação em lote ganhou **Matriz**, **Círculo** e **Lotes**. Matriz cria linhas/colunas com espaçamento X/Y; Círculo distribui itens no perímetro e pode orientar cada asset pela tangente; Lotes usa dois pontos como frente de rua e distribui casas/prédios com espaçamento e recuo configuráveis.

Presets incluídos: **Arborização de avenida**, **Postes de iluminação**, **Casas alinhadas**, **Praça arborizada** e **Grade/estacionamento**. Os presets só configuram as ferramentas; a gravação continua usando o `.sco` real selecionado e o fluxo transacional de lote.


### Barra Construção e ponte/elevado

A barra principal ganhou um grupo **Construção** dedicado para abrir diretamente **Ruas**, **Cruzamentos**, **Pontes**, **Prédios**, **Vegetação**, **Transporte**, **Mobiliário**, **Infraestrutura** e **Terreno**. Cada botão apenas direciona a biblioteca real correspondente; nenhum catálogo paralelo de assets é criado.

O modo **Ponte/Elevado** abre o grupo real de splines de pontes/túneis e reutiliza o criador de rua por pontos e alça de curva. Uma elevação configurável é somada às alturas reais do terreno no início e no fim da spline, preservando a conversão para rotação, comprimento, raio e gradiente OMSI.


### Auditoria de dependências e duplicação rápida

**Verificar dependências** carrega os catálogos reais de `Sceneryobjects` e `Splines` e compara seus caminhos com tudo que o mapa aberto referencia. Arquivos `.sco` e `.sli` ausentes aparecem em um painel flutuante no viewport, separados de falhas de textura ou O3D.

`Ctrl+D` inicia a colocação de uma cópia do objeto selecionado usando exatamente o mesmo `.sco`, Z, rotação, pitch e bank como base; o próximo clique define a nova posição e continua usando o fluxo seguro de inserção.


### Assistente geométrico de cruzamentos

Ao abrir **Construção > Cruzamentos**, o editor analisa até 400 splines normais carregadas. Splines curvas são amostradas ao longo do arco real; o sistema procura encontros com ângulo útil, ignora simples conexões ponta-a-ponta e agrupa pontos muito próximos.

O painel **Assistente de cruzamentos** lista até 64 sugestões e permite escolher o ponto. A sugestão só define posição/rotação inicial; o usuário ainda escolhe um `.sco` real de cruzamento na biblioteca. Portanto o assistente não inventa geometria nem substitui dependências OMSI.


### Histórico transacional de construção

Inserções individuais de objetos, lotes e novas splines entram em um histórico de construção. **↶ Construção** restaura o diretório de backup produzido pela própria operação.

A restauração é feita pelo host desktop com validação rígida para aceitar somente diretórios dentro de `.mapstudio-backups` do mapa aberto. Antes de restaurar, o estado atual dos arquivos é salvo em outro backup transacional; esse backup alimenta **Refazer**. O histórico de sessão é limitado às 40 ações mais recentes e é zerado ao trocar de mapa.


### Escala contextual da prévia 3D

A prévia de `.sco` calcula as dimensões reais da geometria O3D depois de aplicar escala, rotação e translação declaradas por mesh e mostra **L × A × P** em metros. Objetos `[tree]` usam a altura/aspecto máximos reais quando não possuem malha O3D.

Para `.sli`, a prévia mostra a largura total e a variação vertical do perfil real a partir dos pontos das superfícies. Isso substitui a necessidade de inserir bonecos ou ônibus de escala fictícios.


### Snap inteligente de pontas de rua

No criador de rua/ponte, **Encaixar nas pontas de vias existentes** procura a extremidade de spline normal mais próxima dentro do alcance configurado. Início e fim da nova rua passam a usar exatamente a coordenada da ponta encontrada, inclusive quando a spline existente é curva.

O painel informa o ID da spline, qual extremidade foi usada e a distância original do clique. O snap não altera, recorta nem reconecta automaticamente a spline existente; apenas posiciona o novo trecho sobre a ponta real para reduzir ajustes manuais.


### Diagnóstico técnico de assets na biblioteca

A prévia de biblioteca agora participa do mesmo carregamento real de texturas usado por seleção/colocação. Ao apenas clicar em **Prévia**, o Map Studio solicita as texturas do `.sco`/O3D ou do perfil `.sli`, melhorando também as miniaturas 3D persistidas.

O inspetor de `.sco` mostra meshes carregados/falhos, arquivos O3D ausentes, vértices, triângulos, materiais, texturas carregadas/ausentes/pendentes, comandos de material ainda não suportados e meshes de colisão declarados. O status visual indica **íntegro**, **atenção/carregando** ou **problemas detectados**.

Para `.sli`, o inspetor mostra quantidade de superfícies, largura real do perfil, texturas carregadas/ausentes/pendentes e superfícies com alpha. Esses diagnósticos são apenas leitura; não alteram o asset nem substituem dependências.


### Multi-lote transacional de objetos

O bridge desktop aceita `insertObjectMultiBatch` para inserir vários grupos de `.sco` diferentes na mesma operação. O host valida cada asset real, encontra o template preservativo correspondente no mapa, reserva uma sequência global de IDs e agrupa todos os objetos por tile antes de gravar.

O multi-lote aceita até 16 tipos de asset e até 512 objetos totais, com no máximo 256 por tipo. Toda a operação usa uma única `SafeFileTransaction` e um único diretório de backup, evitando corridas de ID quando um conjunto de construção mistura, por exemplo, árvores e postes.


### Conjuntos de construção compostos

O painel móvel **Conjuntos** permite salvar combinações reutilizáveis formadas por uma spline `.sli` real e até 16 tipos de objetos `.sco` companheiros. Para cada companheiro é possível configurar **lado** (esquerda/direita/ambos), **espaçamento**, **afastamento lateral** e **rotação adicional**.

Ao clicar **Construir conjunto**, o criador de rua usa a spline-base real. Depois que a spline é efetivamente salva pelo host, os objetos são distribuídos ao longo da geometria da spline retornada — inclusive curvas e gradiente — e enviados pelo `insertObjectMultiBatch`, com até 512 objetos no total.

O editor exige que cada `.sco` companheiro possua template preservativo no mapa atual. Isso mantém os valores extras específicos do OMSI e evita inventar estrutura de objeto. Os conjuntos ficam no armazenamento local da interface e apenas guardam referências/cfg de colocação; não copiam assets.


### Alças de início e fim no criador de vias

Além da alça azul de curvatura, o traçado de rua/ponte agora mostra uma alça **verde** no início e uma **vermelha** no fim. As duas podem ser arrastadas diretamente no viewport; o comprimento, rotação, raio e gradiente do preview são recalculados enquanto o ponto se move.

As alças de extremidade reutilizam o snap de pontas de splines existentes. Quando uma alça entra no alcance configurado, ela pode encaixar exatamente numa ponta real de via. A spline existente continua intacta; somente o novo traçado é ajustado antes do salvamento.


### Conexão automática previous/next ao encaixar vias

O snap de ponta pode agora criar também os vínculos OMSI da nova spline. A conexão automática só é armada em casos seguros:

- início da nova via encaixado no fim de uma spline existente → previous;
- fim da nova via encaixado no início de uma spline existente → next;
- a ponta-alvo precisa estar livre no vínculo correspondente.

Ponta incompatível ou já ocupada continua apenas encaixada visualmente e é marcada no painel como bloqueada. O salvamento usa o OmsiSplineLinkPlanner existente, que atualiza reciprocidade e recusa conflito.

Quando um Conjunto de Construção também está ativo, as operações são serializadas: primeiro a spline é inserida, depois os vínculos são atualizados e só então o multi-lote de objetos companheiros é gravado. Isso evita transações concorrentes sobre os mesmos tiles.


### Substituição preservativa de dependência

O host desktop aceita a operação replaceMapAssetPath para trocar um caminho ausente por outro asset real instalado. Para objetos, apenas a linha de caminho dentro de [object] é alterada. Para splines, apenas o campo de caminho de [spline]/[spline_h] é alterado segundo o layout da versão do mapa.

IDs, vínculos, posição, rotação, comprimento, raio, gradientes e valores extras permanecem intactos. A operação percorre os tiles do mapa, grava somente os arquivos realmente alterados e usa uma única SafeFileTransaction com backup.


### Reparar dependências ausentes pela biblioteca

O painel de dependências ausentes agora permite selecionar explicitamente um caminho quebrado `.sco` ou `.sli`. Ao selecionar, o editor abre a biblioteca correspondente; o item atualmente em **Prévia** passa a ser o candidato de substituição.

Antes de confirmar, o painel mostra o caminho ausente, o substituto escolhido e quantas referências desse caminho estão atualmente carregadas. A operação no host sempre varre todos os tiles do mapa, inclusive os que não estão no viewport.

A substituição altera somente o caminho do asset e cria backup transacional. Quando há alterações, o backup é registrado no histórico **↶ Construção**, permitindo desfazer. Nenhuma substituição é feita automaticamente.


### Saúde do mapa e filtro de problemas

O botão **Saúde** reúne o diagnóstico técnico disponível sem transformar ausência de dados em falso erro. O painel conta separadamente dependências ausentes, objetos `.sco` carregados com O3D/textura problemáticos, splines `.sli` carregadas com perfil/textura problemáticos, texturas solicitadas com falha e geometria sem visual renderizável.

As bibliotecas ganharam o filtro técnico **⚠ Com problemas**. Ele lista apenas assets para os quais o Map Studio já possui evidência de falha — por exemplo O3D declarado ausente, geometria que falhou ao carregar ou textura solicitada que retornou ausente/erro. Um asset que ainda não foi carregado não entra nesse filtro.

Os cartões do painel Saúde abrem diretamente a biblioteca filtrada ou a auditoria de dependências, acelerando a correção de mapas incompletos.


### Posição persistente das ferramentas móveis

As janelas e barras marcadas como ferramentas móveis agora guardam sua última posição no armazenamento local da interface. A restauração também funciona para painéis que são montados dinamicamente, como **Saúde do mapa**, **Conjuntos**, assistente de cruzamentos, barras de colocação e ferramentas em tela cheia.

Ao restaurar, a posição é limitada às dimensões atuais do contêiner. Assim, mudar resolução, tamanho da janela ou modo de tela cheia não deixa uma ferramenta permanentemente fora da área visível. Falhas de armazenamento local não interferem na edição do mapa.


### UX city-builder v2 — desktop e tela cheia

O viewport passa a ser a área principal do editor. **Explorador/Biblioteca** e **Inspetor** funcionam como drawers sobrepostos e recolhíveis, em vez de colunas permanentes. Selecionar um objeto ou spline abre o Inspetor automaticamente; escolher uma ferramenta de construção abre a biblioteca apropriada.

A construção fica em uma **dock inferior central** compartilhada entre desktop e tela cheia, com categorias de ruas, cruzamentos, pontes, prédios, vegetação, transporte, mobiliário, infraestrutura e terreno. Uma faixa contextual exibe o item/modo atual, filtros de seleção e Snap.

O topo fica reservado para operações de edição, câmera e salvamento. Na tela cheia a barra superior é compactada e a faixa permanente de atalhos é removida para deixar a cena mais limpa.

### Seleção robusta no mapa

O clique usa três níveis de resolução: mesh real pickável, metadados herdados pelos nós pais e fallback geométrico pela posição/eixo real do OMSI. Isso mantém selecionáveis objetos com O3D incompleto/protegido, árvores/billboards e splines muito finas.

Quando uma ferramenta de colocação está aberta, clicar diretamente em um item real existente prioriza a seleção desse item; clicar no terreno livre continua sendo usado para posicionamento.


### Tela cheia robusta e seleção universal

O modo tela cheia agora altera imediatamente o layout React e, no aplicativo desktop, reforça a janela WPF como borderless/maximizada, trazendo-a para frente e restaurando estado/tamanho anteriores ao sair. F11, Esc e o botão da interface usam o mesmo fluxo. A camada do editor também ocupa 100vw × 100vh enquanto o host aplica a transição.

Objetos e splines visíveis passam a ser sempre selecionáveis por clique, independentemente do filtro atual Objeto/Spline/Tudo. O modo Terreno só recebe o clique quando nenhum objeto/spline real foi atingido.

Objetos cujo O3D não possui visual renderizável ou está ausente agora recebem um marcador individual pickable com a identidade do objeto real, permitindo selecionar e reparar também itens incompletos. Os filtros de seleção ficam como filtros visuais/de organização, não como bloqueadores do picking.


### Referências funcionais do editor OMSI e fluxo city-builder

A interface preserva a semântica do editor de mapas do OMSI: objetos `.sco`, splines `.sli`, terreno e ferramentas de tráfego continuam sendo entidades/modos distintos, e a edição de spline respeita comprimento, raio, gradientes e vínculos reais do mapa. A modernização não substitui esses dados por abstrações fictícias.

O fluxo visual adota padrões de editores city-builder modernos: construção principal concentrada na parte inferior, biblioteca pesquisável, inspetor contextual e guias visuais de via/snap. Esses padrões servem apenas como referência de interação; nenhum asset proprietário de terceiros é reutilizado.

O fullscreen desktop usa agora os limites físicos do monitor atual em vez de depender apenas de `WindowState.Maximized`. O menu compacto e a barra de status permanecem acessíveis. O viewport Babylon usa `ResizeObserver` no contêiner real para manter renderização e coordenadas de picking sincronizadas durante fullscreen, redimensionamento e movimentação de painéis.


### Feedback de seleção e guia de via

O viewport agora usa outline leve no item real sob o cursor e um outline distinto na seleção ativa. O efeito não substitui material/textura e não torna ghosts, gizmos, terreno ou overlays selecionáveis como objetos reais. Esse feedback segue a ideia do editor OMSI de deixar evidente qual item receberá o clique, sem aplicar o preenchimento azul intenso típico de alguns city-builders.

Quando o criador fácil de rua/ponte está ativo, a barra inferior mostra um guia contextual compacto com a sequência início → fim → curva, estado de snap de pontas, conexão automática `previous/next`, confirmação dos endpoints encaixados e elevação no modo ponte.


## Menu superior funcional e fluxo de edição

O menu superior do editor foi simplificado para usar somente ações reais já suportadas pelo OMSI Map Studio:

- **Arquivo** salva transformações pendentes de objetos ou splines, descarta prévias e abre outro mapa;
- **Editar** expõe desfazer/refazer de transformações, histórico de construção e as ferramentas Selecionar/Mover/Rotacionar;
- **Visualizar** controla Explorador, Inspetor, navegador de blocos, enquadramento, foco e tela cheia;
- **Mapa** reúne mapa real por coordenadas, Saúde do mapa, auditoria de dependências e Conjuntos de construção.

Os antigos placeholders desabilitados foram removidos da superfície ativa. O topo também mostra de forma compacta a quantidade de alterações pendentes e o modo de carregamento atual. O mesmo padrão permanece disponível no modo desktop e em tela cheia, enquanto a barra inferior continua sendo o ponto principal para construção por categoria.


### Painéis móveis

Explorador e Inspetor agora usam o mesmo sistema persistente das ferramentas flutuantes. Cada drawer possui uma alça própria para arrastar; a posição é limitada à área útil do editor e reaproveitada na sessão seguinte pelo armazenamento local da interface. O tamanho do painel permanece estável durante o arraste tanto no desktop quanto na tela cheia.

## Prateleira contextual de construção

As categorias principais da barra inferior agora exibem uma prateleira horizontal de assets reais diretamente sobre o viewport:

- **Ruas/Pontes** usam entradas `.sli` reais da categoria ativa;
- **Cruzamentos, prédios, vegetação, transporte, mobiliário, infraestrutura e objetos** usam `.sco` reais;
- a lista prioriza favoritos, itens recentes e assets mais usados;
- thumbnails gerados pela prévia 3D real são reutilizados quando já existem; caso contrário, o grupo mostra um ícone sem criar imagem falsa;
- um clique seleciona o asset para colocação/construção;
- duplo clique abre a biblioteca completa e a prévia 3D;
- o botão **Biblioteca** continua disponível para busca, filtros, coleções, favoritos e diagnóstico técnico.

Ao selecionar um item pela prateleira, a interface troca para o **Inspetor contextual**. Durante a colocação de `.sco`, ele mostra caminho, grupo/subcategoria, disponibilidade da geometria 3D, template seguro, tile/posição e rotação. Durante a criação de `.sli`, mostra perfil real, tipo `[spline]`/`[spline_h]`, comprimento/raio/rotação e estado de snap/`previous`/`next`. Esse estado não simula dados: ele acompanha somente informações reais já carregadas ou parâmetros da construção ativa.

### Controles rápidos de via

O guia contextual de ruas/pontes na barra inferior agora também é interativo. **Snap pontas** liga/desliga o encaixe nas extremidades de splines existentes, **previous/next** alterna a conexão automática da cadeia e, no modo ponte/elevado, os botões `−`/`+` ajustam a elevação em passos de 0,5 m. O campo detalhado de elevação continua disponível no painel completo e ambos usam a mesma atualização real do preview.


### Inspector contextual automático

Ao escolher um objeto ou spline na prateleira de construção ou na biblioteca, o **Inspetor** abre automaticamente e passa a acompanhar o asset real em colocação. Em tela cheia ele substitui o drawer anterior, liberando o viewport; no desktop ele abre como painel flutuante. O conteúdo mostra arquivo, grupo, subcategoria, estado da prévia/perfil, destino ou geometria pendente e ações para abrir a prévia completa ou cancelar a colocação.


### Barra rápida unificada

O editor desktop e o modo tela cheia agora usam a mesma **barra rápida flutuante e movível** para seleção, mover/rotacionar, enquadrar/focar, snap, desfazer/refazer, salvar e controlar camadas. A posição é persistida junto das demais ferramentas flutuantes.

A toolbar horizontal antiga foi retirada do layout ativo para reduzir duplicação visual. Suas funções continuam disponíveis na barra rápida, na barra de construção inferior e nos menus superiores. Os modos **Mapa completo** e **Desempenho 3×3** ficam em **Visualizar** e o modo atual continua visível no status do menu.


### Filtros de seleção no picking real

Os filtros **Tudo / Objetos / Splines / Terreno** agora são aplicados diretamente no picking do viewport, incluindo mesh real, hierarquia de filhos e fallback geométrico. No modo **Objetos**, splines não roubam o clique; no modo **Splines**, objetos não roubam o clique; e no modo **Terreno** o pointerdown não seleciona cenário antes do clique de terreno ser processado.

### Alinhamento visual do modo tela cheia

O modo tela cheia passa a seguir a direção visual aprovada para o editor:

- trilho lateral compacto com identidade do Map Studio e navegação por ícones;
- menu funcional e status reais permanecem visíveis no topo;
- categorias de construção ficam no topo do viewport;
- a barra rápida de edição fica logo abaixo das categorias;
- Explorer, Inspetor e navegador de blocos continuam como painéis flutuantes sobre o mapa;
- a subbarra contextual permanece acessível próxima à base do viewport;
- o status superior expõe mapa pronto/alterações, modo de carregamento, índice local e saúde do mapa;
- a abertura da prateleira de assets reposiciona dock e painéis para evitar sobreposição.

A alteração é somente de UX/layout: o viewport continua consumindo o estado real do host C# e não introduz dados fictícios.


### Menus Ferramentas e Ajuda

A barra superior do editor passa a seguir a referência visual aprovada sem criar comandos decorativos:

- **Ferramentas** concentra a auditoria real de dependências e os conjuntos de construção;
- **Ajuda** abre um painel móvel de atalhos do editor;
- o painel de atalhos reutiliza o sistema `floating-tool`, portanto pode ser reposicionado sem bloquear o viewport;
- o bloco antigo de atalhos exclusivo do fullscreen foi removido em favor de um único painel compartilhado entre desktop e F11;
- **Mapa** permanece focado em ações do mapa, como mapa real por coordenadas e saúde do mapa.

Todo o comportamento continua ligado ao estado real do editor; não há comandos fictícios.


### Refinamento dos painéis flutuantes e barra de status

- Explorer passa a ter cabeçalho próprio com nome do mapa e mantém as abas Explorador/Biblioteca/Splines;
- Inspector ganha cabeçalho com ícone e estado contextual da seleção;
- navegador de blocos recebe cabeçalho visual, botão de fechar e grade com maior legibilidade;
- barra inferior troca o diagnóstico contínuo por segmentos compactos com seleção, tile ativo, snap, modo de carregamento, contagem de objetos/splines e estado;
- diagnósticos detalhados de O3D/SLI/texturas continuam disponíveis no tooltip do estado e nos painéis próprios;
- nenhuma informação fictícia foi adicionada: os segmentos usam apenas estado real do editor.


### Cartões visuais da Biblioteca

- objetos `.sco` e splines `.sli` passam a usar cartões mais visuais, com miniatura maior;
- a miniatura continua vindo exclusivamente do cache real produzido por `AssetPreview3D`;
- quando ainda não há thumbnail real, o cartão usa somente o ícone semântico da categoria, sem imagem fictícia;
- ações Favoritar, Coleção, Prévia, Colocar, Normal e Altura passam a usar o pacote SVG compartilhado;
- o cartão selecionado recebe destaque visual sem alterar a seleção ou o fluxo de persistência;
- drag-and-drop para o mapa continua usando os mesmos MIME internos e dados reais.


### Navegação e prévia destacada da Biblioteca

- abas Grupos/Favoritos/Recentes/Mais usados/Coleção usam ícones semânticos;
- a prévia 3D real selecionada ganhou maior área visual e hierarquia de informação;
- detalhes do asset passam a ser organizados em pequenos blocos, sem esconder os dados técnicos;
- a miniatura e a prévia continuam vindo do conteúdo real carregado do OMSI.


### Inspetor visual v2

- cabeçalhos de mapa, objeto, spline e assets em colocação passam a usar ícones semânticos do Map Studio;
- abas de objeto usam ícones para Geral, Transformar, Geometria e Materiais;
- abas de spline usam ícones para Geral, Traçado e Perfil;
- propriedades densas passam a usar cartões/linhas compactas com maior contraste;
- transformação de objetos usa grade de 3 colunas para X/Y/Z e orientação em telas adequadas;
- transformação de splines permanece em grade de 2 colunas para acomodar comprimento, raio e gradientes;
- botões de prévia, copiar, excluir, salvar, nivelar, vínculos e descartar passam a usar ações SVG consistentes;
- todos os handlers, validações preservativas e fluxos de backup permanecem inalterados.


### Viewport visual v2

- seleção ativa usa outline ciano mais forte enquanto hover permanece mais discreto;
- marcador de objeto e eixo de spline selecionados seguem a mesma cor de seleção;
- grade normal foi suavizada, tiles ausentes continuam destacados em vermelho e o tile ativo permanece verde;
- splines não selecionadas ficam visualmente mais leves e a spline ativa recebe maior contraste;
- o viewport exibe um cartão compacto com tipo/ID do item selecionado, nome do asset, ferramenta atual e estado do Snap;
- o gizmo Babylon usa `scaleRatio` levemente maior para facilitar a captura dos eixos sem alterar os valores de transformação;
- o diagnóstico técnico da seleção passou a ser recolhível para não cobrir o mapa durante edição normal;
- picking, fallback geométrico, Snap, transformação e persistência continuam usando a mesma lógica existente.


### Controles compactos de câmera e camadas

- barra desktop de câmera passa a usar botões compactos com ícones para Perspectiva, Topo e Snap;
- atalhos 1/2/N permanecem visíveis como dicas secundárias;
- o estado da ferramenta atual usa o mesmo ícone de Selecionar/Mover/Rotacionar do restante do editor;
- o painel Camadas ganhou cabeçalho próprio, ícones semânticos e linhas de visibilidade mais compactas;
- Grade, Objetos, Splines, Perfis, Nightmap, Terreno e Pintura de terreno continuam ligados aos mesmos estados reais;
- Limpar cache mantém a mesma lógica e passa a usar ação visual própria;
- no modo tela cheia esses painéis continuam ocultos em favor das docks unificadas já existentes.


## Seleção no estilo do OMSI Editor

- o clique simples é confirmado ao soltar o botão esquerdo, evitando reconstruir a cena no meio do mesmo clique;
- objetos e splines visíveis continuam usando picking real da malha;
- assets sem malha clicável utilizam uma tolerância de seleção em espaço de tela, mantendo o alvo previsível em diferentes níveis de zoom;
- selecionar um item não troca automaticamente o filtro de seleção nem desarma **Mover**/**Rotacionar**;
- clique rápido repetido no mesmo item mantém o comportamento de foco;
- clicar em área vazia limpa a seleção sem alterar a ferramenta explicitamente escolhida.

O `Engine` do Babylon permanece associado ao canvas durante atualizações normais de estado; apenas a cena necessária é atualizada/recriada. Isso evita o flash preto causado por destruir e recriar o contexto gráfico durante streaming, seleção e abertura do inspetor.


### Overlay de seleção desacoplado

A seleção visual foi separada da reconstrução estrutural da cena. Em modo **Selecionar**, trocar de objeto ou spline atualiza apenas o contorno/guia de seleção; o mapa, terreno, objetos e perfis já renderizados permanecem na cena. A reconstrução estrutural continua reservada para mudanças que realmente alteram conteúdo, visibilidade ou edição com gizmos. Isso aproxima o comportamento do editor clássico do OMSI e reduz interrupções ao clicar repetidamente no cenário.
