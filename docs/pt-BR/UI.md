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
