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
