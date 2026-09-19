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


## Streaming de tiles em mapas grandes

O editor trabalha com um **tile ativo** e uma janela padrão de 3×3 tiles ao redor dele. O `global.cfg` continua fornecendo a topologia completa do mapa, mas objetos, splines e outros dados pesados são carregados somente nessa área.

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
