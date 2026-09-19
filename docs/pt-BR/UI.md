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
