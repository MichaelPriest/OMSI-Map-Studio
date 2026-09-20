# Sistema de ícones

[English](../en/ICON_SYSTEM.md) · **Português (Brasil)**

## Objetivo

O OMSI Map Studio terá um pacote próprio de ícones para criar uma identidade visual coerente com a linguagem técnica do OMSI sem copiar diretamente assets proprietários do jogo.

A direção visual deve lembrar:

- editor técnico de transporte;
- sinalização viária;
- mapas e infraestrutura;
- botões compactos do OMSI clássico;
- instrumentos de construção;
- leitura rápida mesmo em tamanhos pequenos.

O resultado deve ser mais moderno, limpo e consistente que o editor original.

## Formato oficial

Os ícones principais devem ser **SVG vetorial**.

Motivos:

- permanecem nítidos em 16, 20, 24, 32 e 48 px;
- funcionam em telas HiDPI;
- podem receber cor pelo CSS;
- não exigem várias versões rasterizadas;
- podem ser usados diretamente em React;
- facilitam estados hover, active, disabled e warning.

PNG poderá existir apenas para exportações especiais ou previews.

## Regras visuais

- desenho simples;
- leitura clara em 16–20 px;
- espessura de traço consistente;
- cantos levemente técnicos, não excessivamente arredondados;
- evitar aparência infantil;
- evitar aparência genérica de aplicativo mobile;
- silhuetas inspiradas em trânsito, mapas, engenharia e transporte;
- uma cor principal por estado, controlada por CSS;
- fundo transparente;
- nenhum texto embutido no SVG;
- nenhum logotipo ou asset copiado de outro produto.

## Estados obrigatórios

Todo ícone precisa funcionar nos estados:

- normal;
- hover;
- active/selected;
- disabled;
- warning;
- error quando aplicável.

A cor não deve ser a única forma de indicar estado: o botão/controle também deve mudar borda, fundo ou contraste.

## Tamanhos

Tamanhos-base:

- 16 px — menus e listas densas;
- 18/20 px — toolbar principal;
- 24 px — construção e Inspector;
- 32 px — cards/biblioteca;
- 48 px — categorias e telas vazias.

## Pacote inicial

### Navegação/editor

- select;
- move;
- rotate;
- scale (reservado);
- focus;
- fit view;
- fullscreen;
- exit fullscreen;
- camera perspective;
- camera top;
- grid;
- snap;
- undo;
- redo;
- save;
- open;
- search;
- settings;
- explorer;
- inspector;
- layers;
- visibility on/off.

### Construção

- road;
- curved road;
- intersection;
- bridge;
- tunnel;
- building;
- house;
- tree;
- vegetation;
- grass;
- water;
- street furniture;
- utility;
- transit;
- bus stop;
- rail;
- terrain;
- bulldoze/delete;
- replace;
- duplicate;
- line placement;
- brush placement;
- matrix placement;
- circle placement;
- lots.

### OMSI específico

- SCO object;
- SLI spline;
- O3D model;
- texture;
- tile;
- tile create;
- tile delete;
- terrain file;
- attachment;
- parent;
- spline mirror;
- spline cant;
- spline complete-to;
- spline export;
- path;
- traffic rule;
- speed limit;
- traffic density;
- vehicle restriction;
- priority;
- AI traffic;
- pedestrian;
- traffic light;
- signal phase;
- stop;
- station;
- StationLink;
- track;
- trip;
- timetable;
- signal route;
- railway switch;
- chronology;
- environment.

### Diagnóstico

- map health;
- dependency;
- missing asset;
- protected asset;
- warning;
- error;
- success;
- reload;
- cache;
- asset index;
- streaming;
- GPU/memory;
- performance.

## Organização no código

Estrutura planejada:

    src/MapStudio.UI/src/icons/
      types.ts
      MapStudioIcon.tsx
      icons/
        select.svg
        move.svg
        road.svg
        ...
      index.ts

A interface deve usar um componente único:

    <MapStudioIcon name="road" size={20} />

Não espalhar SVGs inline pelo App.tsx.

## Semântica

Os nomes dos ícones devem representar ações ou conceitos, não posição visual.

Correto:

- `road`
- `save`
- `traffic-rule`
- `asset-index`

Evitar:

- `button-left`
- `icon-blue`
- `tool3`

## Acessibilidade

Ícones puramente decorativos usam `aria-hidden`.

Botões que contêm somente ícone devem possuir `aria-label` e `title` apropriados.

## Migração

A troca dos símbolos atuais deve acontecer por grupos, sem quebrar funcionalidade:

1. barra rápida;
2. HUD de construção;
3. Explorer/Inspector;
4. menus;
5. biblioteca;
6. diagnóstico;
7. ferramentas OMSI futuras.

Durante a migração, símbolo antigo e SVG novo não devem aparecer juntos no mesmo botão.

## Regra de identidade

O pacote pode ser **inspirado na atmosfera técnica do OMSI**, porém todos os desenhos finais do Map Studio serão próprios.

Não reutilizar arquivos gráficos extraídos do OMSI 2 ou de outros editores.

## Próximo passo

Criar o primeiro lote visual com:

- select;
- move;
- rotate;
- road;
- intersection;
- bridge;
- building;
- tree;
- terrain;
- explorer;
- inspector;
- save;
- undo;
- redo;
- fullscreen;
- map health;
- asset index;
- streaming.

Depois validar o conjunto dentro da toolbar e da barra de construção antes de desenhar todos os demais.


## Estado da implementação

Primeiro lote implementado na interface:

- componente compartilhado `MapStudioIcon`;
- SVGs próprios com `currentColor`, fundo transparente e traço técnico consistente;
- barra rápida desktop/tela cheia migrada para select, move, rotate, fit view, focus, Explorer, Inspector, undo, redo, save e fullscreen;
- HUD de construção migrado para ruas, cruzamentos, pontes, prédios, vegetação, transporte, mobiliário, infraestrutura e terreno;
- atalhos de teclado continuam visíveis onde ajudam a operação;
- símbolos Unicode antigos deixam de ser usados nos botões já migrados.

O pacote também já inclui os ícones de diagnóstico `map-health`, `asset-index` e `streaming`, preparados para a próxima migração visual.

Os SVGs são originais do Map Studio e não reutilizam arquivos gráficos do OMSI 2.


### Segundo lote migrado

- toolbar técnico de seleção/mover/rotacionar/escala reservada;
- enquadrar, focar, tela cheia, desfazer/refazer e descartar;
- histórico de construção;
- botão de conjuntos de construção;
- auditoria de dependências;
- saúde do mapa no toolbar e no HUD;
- ícone da categoria ativa na prateleira de assets.

Novos SVGs deste lote: `scale`, `construction-set`, `dependency`, `discard`, `warning` e `success`.

### Terceiro lote migrado

- navegação principal: Início, Abrir, Explorador, Ferramentas e Configurações;
- recolher/expandir a barra lateral;
- indicadores de etapa concluída usam o ícone `success`;
- grupos das bibliotecas de objetos e splines usam nomes semânticos do pacote SVG em vez de símbolos Unicode;
- fallbacks dos cartões/prateleiras de assets usam `MapStudioIcon`;
- conjuntos de construção no HUD usam o mesmo ícone compartilhado;
- status do menu combina `success`, `warning`, `streaming`, `asset-index` e `map-health`.

Novos SVGs deste lote: `home`, `open`, `tools`, `settings`, `collapse` e `expand`.

O modo tela cheia também reutiliza este mesmo sistema na navegação compacta, evitando uma identidade visual separada entre desktop e fullscreen.


### Quarto lote migrado

O dock rápido de tela cheia deixou de usar letras como representação principal para **Snap, Grade, Terreno, Objetos, Splines e Perfis de spline**.

- `snap`, `grid` e `profile` foram adicionados como SVGs originais do Map Studio;
- Terreno reutiliza `terrain`;
- Objetos reutilizam `sco-object`;
- Splines reutilizam `sli-spline`;
- as letras N/T/G/O/L/P permanecem somente como dicas secundárias dos atalhos de teclado;
- estados ativos continuam usando a mesma semântica visual do restante do editor.

Isso aproxima o modo F11 da referência visual aprovada sem criar uma segunda implementação de ferramentas.


### Quinto lote migrado

- novo ícone `drag` para todas as alças de painéis/ferramentas móveis;
- fechar Explorer/Inspector, Saúde do mapa e Conjuntos reutiliza `discard`;
- estados da Saúde do mapa reutilizam `success` e `warning`;
- aviso de templates ausentes reutiliza `warning`.


### Sexto lote migrado

- `favorite` para favoritos da biblioteca;
- `collection` para coleções de assets;
- `preview` para abrir a prévia real do asset;
- ações de colocação continuam reutilizando `sco-object`, `sli-spline` e `profile`.


### Sétimo lote migrado

- `recent` para itens recentes;
- `usage` para itens mais usados;
- navegação da Biblioteca combina estes ícones com `construction-set`, `favorite` e `collection`.


### Oitavo lote migrado

- `properties`, `geometry` e `materials` para abas do Inspetor;
- `duplicate` para cópias preservativas;
- `delete` para exclusão de objetos/splines;
- `link` para edição de vínculos previous/next;
- ações continuam reutilizando `preview`, `save`, `discard`, `terrain`, `sco-object`, `sli-spline` e `profile`.
