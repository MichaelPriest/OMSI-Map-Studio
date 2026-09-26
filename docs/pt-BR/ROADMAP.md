# Roadmap técnico e funcional

[English](../en/ROADMAP.md) · **Português (Brasil)**

> Este documento é a fonte de verdade para a direção técnica e funcional do OMSI Map Studio. Mudanças de arquitetura, novas famílias de ferramentas e diferenças importantes de compatibilidade com o editor original do OMSI devem ser registradas aqui antes ou junto da implementação.

## Visão

O objetivo do OMSI Map Studio não é copiar visualmente o editor antigo do OMSI. O objetivo é **substituí-lo funcionalmente**, preservando compatibilidade com os formatos reais do OMSI 2, enquanto oferece uma experiência moderna de construção próxima de editores city-builder.

A meta de longo prazo é permitir que um mapa possa ser criado, carregado, editado, validado e preparado para operação no OMSI sem depender do editor original para tarefas essenciais.

Princípios permanentes:

- nenhum dado fake/mock em produção;
- o C# continua sendo a autoridade sobre arquivos, formatos, paths e persistência OMSI;
- React nunca inventa estado que deveria vir do Core;
- comandos desconhecidos devem ser preservados;
- alterações persistentes devem possuir backup/undo seguro sempre que tecnicamente possível;
- leitura parcial nunca deve provocar perda silenciosa;
- o projeto permanece totalmente separado do OMSI NavBR Multiplayer;
- documentação oficial deve existir em pt-BR e en;
- compatibilidade e segurança de gravação têm prioridade sobre conveniência;
- UX moderna não pode alterar a semântica dos arquivos OMSI.

---

## 1. Direção tecnológica oficial

### Stack mantida

A arquitetura alvo continua sendo:

- **.NET 10 / C#** para domínio, parsing, indexação, cache, validação e escrita;
- **WPF** como host Windows e integração nativa;
- **WebView2** para hospedar a interface moderna;
- **React + TypeScript + Vite** para a interface;
- **Babylon.js** para viewport 3D, picking, materiais, instancing, LOD e ferramentas de edição;
- armazenamento persistente local para índices/caches; a primeira opção arquitetural é **SQLite**, podendo ser acompanhado por arquivos binários de cache para geometria pesada.

Não está planejada uma migração geral para Unity, Unreal ou uma engine C++ própria.

### Por que manter esta arquitetura

Ela permite separar responsabilidades:

    OMSI files / installation
              ↓
        MapStudio.Core
              ↓
      Asset Index / Cache
              ↓
       Desktop Bridge
              ↓
     React + Babylon.js

O Core conhece OMSI. O renderer conhece geometria. A UI conhece interação. Nenhuma dessas camadas deve assumir a responsabilidade da outra.

### WebGL / WebGPU

Babylon continua sendo a camada de renderização. WebGPU pode ser adotado futuramente quando estiver suficientemente confiável no WebView2 alvo, com fallback compatível. A evolução do backend gráfico não deve exigir reescrita dos parsers OMSI.

---

## 2. Como usar o editor original do OMSI como referência

O editor original é o próprio OMSI iniciado em modo editor. Por isso ele compartilha diretamente a infraestrutura de carregamento do simulador.

O Map Studio é independente e precisa reproduzir explicitamente esse conhecimento.

Características que devemos adotar como referência arquitetural:

- mapa dividido em tiles;
- carregamento orientado pela região visível/ativa;
- objetos, splines, terreno e demais dados associados aos tiles;
- assets acessados conforme necessários;
- catálogo de tipos cresce conforme o conteúdo usado é encontrado;
- o mapa não deve precisar virar uma única cena monolítica para poder ser editado.

Para mapas cartesianos tradicionais, o tile OMSI continua sendo tratado como unidade espacial de 300 × 300 m quando o formato realmente usa essa convenção.

O Map Studio não precisa reproduzir limitações do editor antigo. A referência é a **semântica OMSI**, não a interface antiga.

---

## 3. Nova arquitetura de carregamento

### 3.1 Asset Index persistente

Criar um índice local persistente da instalação OMSI.

O índice deve cobrir progressivamente:

- `Sceneryobjects`;
- `Splines`;
- `Texture` e texturas referenciadas por pacotes;
- `.sco`;
- `.sli`;
- `.o3d`;
- árvores;
- collision meshes;
- scripts/metadados necessários;
- dependências entre assets;
- caminhos resolvidos;
- fingerprints de arquivo;
- estado disponível/ausente/protegido/inválido.

O índice não substitui os arquivos OMSI. Ele é derivado deles e pode ser reconstruído.

### Implementação Fase A — estado atual

A base persistente usa um banco SQLite local por instalação OMSI. A atualização é executada em segundo plano e não bloqueia a edição. Em uma instalação já indexada, arquivos sem alteração são reaproveitados; uma falha do índice não impede a leitura direta dos arquivos OMSI.

O streaming automático por tiles agora é o modo padrão ao abrir mapas: os anéis 0–1 recebem conteúdo completo, o anel 2 recebe summary/metadata leve, terreno pesado fora da região ativa é descartado da UI e respostas regionais antigas são invalidadas por geração para não sobrescrever a posição atual da câmera. **Mapa completo** continua disponível como modo explícito de diagnóstico. O primeiro estágio de cache derivado em memória reutiliza parsing de SCO, SLI, O3D e X enquanto o fingerprint do arquivo permanece válido. O renderer também mantém um warm cache GPU limitado a 64 texturas antigas ou 128 MiB. O conteúdo completo dos tiles usados no streaming agora possui cache LRU de 32 entradas com fingerprint dos arquivos dependentes, permitindo reaproveitar os tiles sobrepostos ao mover a janela 3×3 sem reler tudo do disco. A navegação 3×3 também ganhou transição visível primeiro: se o tile alvo já está no snapshot atual, ele é focado imediatamente e o novo entorno é completado depois; em saltos fora da área carregada, o tile central é renderizado antes dos vizinhos. Ainda faltam cache persistente/mais amplo, fila completa por prioridade e gerenciamento/LOD GPU por orçamento global para concluir a Fase A.

### 3.2 Cache derivado

O cache pode armazenar:

- metadata já parseada;
- bounds;
- geometria pronta para o renderer;
- materiais resolvidos;
- informação de textura;
- thumbnails;
- preview 3D;
- dependências;
- resultado de validações caras;
- perfil de spline já normalizado para preview;
- hashes/fingerprints para invalidação.

Arquivos modificados externamente devem invalidar apenas as entradas afetadas.

### 3.3 Índice incremental

A primeira indexação pode percorrer a instalação inteira, mas as próximas execuções devem:

1. comparar path + tamanho + data/hashes conforme necessário;
2. reutilizar entradas válidas;
3. atualizar somente assets novos/alterados/removidos;
4. atualizar dependentes somente quando a mudança realmente os afetar.

### 3.4 Streaming de mapa por tiles

Substituir gradualmente a dicotomia manual “Mapa completo / 3×3” por streaming automático.

Modelo alvo:

- **anel 0 — tile da câmera/seleção:** dados completos e prioridade máxima;
- **anel 1 — vizinhos imediatos:** terreno, splines, objetos e picking completos;
- **anel 2 — região próxima:** visual simplificado/LOD e metadata;
- **restante do mapa:** topologia, bounds, minimapa e metadata leve;
- assets fora de uso podem ser descarregados da GPU mantendo cache de CPU/disco.

O modo “Mapa completo” pode continuar existindo como opção de diagnóstico, mas não deve ser necessário para trabalhar normalmente.

### 3.5 Prioridade de carregamento

A ordem preferida para abrir um mapa será:

1. `global.cfg` e topologia;
2. tiles e bounds;
3. terreno da região ativa;
4. splines/ruas da região ativa;
5. objetos visíveis;
6. texturas e detalhes progressivos;
7. conteúdo distante;
8. tarefas de baixa prioridade como thumbnails.

A interface deve se tornar utilizável antes de toda a instalação terminar de ser analisada.

### 3.6 Renderer

Usar sempre que seguro:

- frustum culling;
- instancing/thin instances para objetos repetidos;
- LOD;
- descarte de recursos GPU fora da região relevante;
- texture reuse;
- batching onde não comprometer seleção individual;
- meshes simplificados para distância;
- picking associado à identidade OMSI real, inclusive em instâncias.

---

## 4. Estado funcional já alcançado

O Map Studio já possui bases que são superiores ou mais modernas que o editor original em vários fluxos:

- catálogo de mapas reais;
- leitura preservativa;
- objetos `.sco` reais;
- geometria `.o3d` real quando suportada;
- materiais/texturas em evolução;
- splines `.sli` reais;
- terreno real;
- seleção por mesh e fallback geométrico;
- transformação de objetos/splines;
- preview antes de gravar;
- backups;
- undo/redo;
- biblioteca visual;
- grupos/subgrupos;
- favoritos/recentes/frequentes/coleções;
- previews e thumbnails;
- construção Single/Repeat/Line/Brush/Matrix/Circle/Lots;
- Construction Sets;
- road builder com início/fim/curva;
- snapping;
- pontes/elevados;
- nivelamento usando terreno;
- coordenadas e referência geográfica;
- imagem de mapa real;
- grade de elevação;
- Inspector contextual;
- interface desktop/fullscreen unificada;
- Map Health;
- auditoria de dependências.

Essas funções devem ser preservadas durante todas as fases seguintes.

---

## 5. Compatibilidade a alcançar com o editor OMSI

Legenda:

- ✅ já existe de forma útil;
- 🟡 existe parcialmente ou somente leitura/preview;
- ⬜ ainda precisa ser implementado como ferramenta real.

| Área | Estado alvo atual |
|---|---|
| Objetos `.sco` | ✅ |
| Splines `.sli` | ✅ |
| Selecionar / mover / rotacionar | 🟡 — seleção ainda em estabilização |
| Curva, comprimento e gradiente de spline | ✅ |
| Biblioteca moderna / preview | ✅ |
| Ferramentas de construção em massa | ✅ |
| Coordenadas / referência real / elevação | ✅/🟡 |
| Criar e excluir tiles | 🟡 — criação e exclusão segura já integradas; remoção de tile intermediário segue bloqueada até reindexar referências dependentes |
| Propriedades completas do tile | ⬜ |
| Água nativa do tile/mapa | ✅ |
| Lightmap / iluminação de tile | ⬜ |
| Attach object → spline | 🟡 |
| Attach object → object | 🟡 |
| Parent / hierarchy editável | ⬜ |
| Labels/opções específicas do objeto | ⬜ |
| Mirror de spline | ⬜ |
| Cant start/end | ✅ |
| Complete to… | ✅ — conecta fim→início compatíveis com solver reto/arco, raio máximo, vínculos transacionais e backup |
| Spline export | ✅ — seleção múltipla das splines carregadas e exportação geométrica DirectX `.x` com UVs para Blender |
| Paths editáveis | 🟡 |
| Traffic Rules | ✅ |
| Speed limits | ✅ — preset `speedlimit` com valor customizado por path e grupo de veículo |
| Traffic density | ✅ — presets `trafficdensity` incluindo bloqueio do tráfego não agendado e densidades graduais |
| Vehicle restrictions | ✅ — presets OMSI `no_cars`, `truck`, `bus` e `overtaking_prohib`, com suporte a `[rule]`/`[kill_rule]` |
| Prioridades viárias | ✅ — presets `priority` alto/baixo por path, persistidos com backup |
| AI paths / crossing behavior | ⬜ |
| Traffic lights / signal phases | ✅ |
| Tracks | ✅ |
| Trips | ✅ |
| Stops/stations | ✅ |
| StationLinks | ✅ |
| Time profiles | ✅ — editor visual nativo cria/exclui perfis, ajusta duração total e tempos acumulados por parada, preservando os dados OMSI brutos |
| Timetable editor | 🟡 — Route Studio e a janela Timetable destacável já editam Tracks/Trips/Stops/StationLinks/Lines/Tours e perfis; Lines/Tours possuem tabela de saídas com adicionar/remover/reordenar, e Trips podem ser editados sem sair da janela Timetable. Chrono, calendários/serviços e fluxos avançados ainda evoluem |
| Signal Routes | ⬜ |
| Railway priorities/switches | ⬜ |
| Environment settings | ⬜ |
| Chronology support | ⬜ |
| Debug operacional do mapa | ⬜ |

---

## 6. Ordem oficial de implementação

### Fase A — fundação de desempenho

Prioridade máxima antes de aumentar muito a quantidade de sistemas.

Estado atual da Fase A:

- 🟡 **Asset Index:** SQLite v1 já indexa `.sco`, `.sli`, `.o3d`, `.x` e texturas em `Sceneryobjects`, `Splines` e `Texture`;
- 🟡 **cache persistente:** o índice fica em `LocalApplicationData/OMSI Map Studio/Cache/<instalação>/assets-v1.sqlite` e pode ser reconstruído;
- 🟡 **invalidação incremental:** path, tipo, tamanho e data de modificação distinguem arquivos novos, alterados, iguais e removidos;
- 🟡 **bibliotecas indexadas:** os catálogos de objetos e splines podem usar o índice persistente e mantêm a varredura direta como fallback seguro;
- 🟡 **streaming automático por tiles:** é o modo padrão ao abrir mapas; mantém anéis 0–1 completos, lê o anel 2 como metadata leve, descarta terreno pesado fora da região ativa e ignora respostas regionais obsoletas;
- ⬜ fila de carregamento por prioridade completa;
- ⬜ gerenciamento de memória/GPU completo;
- ⬜ instancing e LOD onde seguro;
- ⬜ métricas internas completas de tempo de abertura;
- 🟡 diagnóstico de cache: progresso e contagens do Asset Index já aparecem na tela da instalação OMSI.

Já existem cache derivado inicial, cache LRU de conteúdo de tiles e retenção LRU GPU limitada. Ainda faltam cache persistente/mais amplo, fila completa por prioridade e descarte/LOD GPU por orçamento global para marcar a Fase A como ✅.

Critério de conclusão:

- um mapa deve ficar editável sem exigir que todos os assets da instalação sejam processados novamente;
- mover a câmera deve carregar/descarregar região sem perder identidade dos itens;
- seleção e edição devem continuar funcionando durante streaming.

### Fase B — completar edição física do mapa

Implementar:

- criar tile;
- excluir tile com confirmação e backup;
- propriedades completas de tile;
- edição de água real;
- lightmap/iluminação onde o formato estiver validado;
- attachments object/spline;
- parent/hierarchy;
- opções/labels de objetos;
- Mirror;
- Cant start/end;
- Complete to…;
- Spline Export;
- criação/edição segura de seções ainda preservadas como extras.

Critério de conclusão:

- operações físicas comuns do editor original não exigem abrir o editor OMSI;
- round-trip preservativo permanece válido.

### Fase C — paths e tráfego

Implementar uma camada visual de paths, independente da malha renderizada da rua.

Ferramentas:

- visualizar paths;
- selecionar path;
- criar/remover/editar path quando o formato estiver validado;
- direção;
- faixa/tipo;
- speed limit;
- traffic density;
- prioridades;
- proibições/restrições por veículo;
- carros;
- caminhões;
- ônibus;
- pedestres quando aplicável;
- ligação entre paths;
- cruzamentos;
- comportamento AI;
- semáforos/fases;
- validação de path quebrado/desconectado.

UX desejada:

- overlay de paths com cores/ícones próprios;
- Inspector de regras;
- filtros;
- erros destacados no cenário;
- edição visual sem exigir conhecimento de números internos.

### Fase D — transporte público e operação

Implementar:

- stops;
- stations;
- station links;
- tracks;
- trips;
- linhas;
- direção da linha;
- sequência de paradas;
- perfis de tempo;
- viagens;
- horários;
- calendário/serviço conforme o formato OMSI suportar;
- validação de HOF/linhas quando aplicável;
- visualização da rota sobre o mapa.

Criar um editor de linha visual:

    path → track → stops → trip → profile → timetable

Estado atual do Route Studio: preview visual de Track, Trip, StationLink e Line/Tours, lista navegável de segmentos reais `ID:pathIndex`, foco do trecho no viewport, overlay opcional de Paths OMSI e edição preservativa dos arquivos TTData existentes. A janela Timetable destacável edita Trip e perfis diretamente, além de Lines/Tours em tabela com inclusão, remoção e reordenação de saídas.

Critério de conclusão:

- uma linha de ônibus pode ser construída e validada no Map Studio sem retornar ao editor antigo para a parte operacional principal.

### Fase E — sinais, ferrovia e regras avançadas

Implementar:

- Signal Routes;
- sinais;
- switches/agulhas;
- prioridades ferroviárias;
- crossing/level crossing;
- dependências entre sinal e rota;
- diagnóstico de conflito;
- ferramentas ferroviárias dedicadas.

### Fase F — ambiente e cronologia

Implementar, após validação dos formatos:

- Environment;
- clima/ambiente quando editável por mapa;
- Chronology;
- objetos/alterações condicionais por período;
- ferramentas de inspeção de estados cronológicos.

### Fase G — criação moderna acima do editor original

Continuar evoluindo ferramentas que não precisam existir no editor antigo:

- criação de rua tipo city-builder;
- curvas por handles;
- curvas Bézier quando puderem ser convertidas de forma segura para o modelo OMSI;
- snapping inteligente;
- auto-intersection;
- auto-junction assistido por assets reais;
- avenidas paralelas;
- grid roads;
- ruas paralelas;
- replace tool;
- elevação/depressão visual;
- taludes/cortes;
- pontes;
- ✅ túneis paramétricos por SLI real, com geração de perfil e paths;
- lotes;
- vegetação procedural;
- regras de distribuição;
- presets de bairros;
- Construction Sets;
- duplicação procedural;
- ferramentas de alinhamento;
- distribuição uniforme;
- seleção múltipla.

Toda ferramenta procedural deve resultar em dados OMSI reais e inspecionáveis.

### Fase H — mapas reais e geodados

Completar:

- conversão oficial de `[worldcoordinates]` somente após validar o formato;
- âncoras geográficas;
- importação de elevação;
- referência aérea/satélite;
- alinhamento da imagem ao terreno;
- importação opcional de dados vetoriais permitidos/licenciados;
- geração assistida de traçado de ruas;
- conversão para splines reais escolhidas pelo usuário;
- reconstrução de terreno por DEM;
- validação de datum/offset.

Nunca gravar um formato “parecido” com worldcoordinates. Ou é compatível com o OMSI real ou permanece metadata exclusiva do Map Studio.

### Fase I — validação e debug

Criar ferramentas equivalentes a uma verificação técnica de mapa:

- missing assets;
- paths quebrados;
- splines desconectadas;
- attachments inválidos;
- IDs duplicados;
- referências fora do mapa;
- problemas de textura;
- O3D incompatível/protegido;
- regras de tráfego inconsistentes;
- linhas sem continuidade;
- paradas sem conexão;
- timetable incompleto;
- signal routes inválidas;
- tiles ausentes;
- terreno inválido;
- dependências circulares quando aplicável.

O Map Health deve evoluir para concentrar essas verificações.

---

## 7. Modelo de ferramentas da interface

A UI deve continuar aproximando a interação de um city-builder moderno, sem copiar assets proprietários.

Estrutura desejada:

- menu superior para funções menos frequentes;
- viewport como área dominante;
- barra rápida única desktop/fullscreen;
- barra de construção inferior;
- shelf contextual de assets;
- Explorer;
- Inspector;
- painéis movíveis;
- ferramentas contextuais apenas quando necessárias;
- previews visuais;
- estados claros de hover/seleção/erro;
- ferramentas avançadas acessíveis sem poluir o modo básico.

### Modos principais futuros

- Seleção;
- Objetos;
- Ruas/Splines;
- Cruzamentos;
- Terreno;
- Água;
- Paths/Tráfego;
- Transporte/Rotas;
- Ferrovia/Sinais;
- Ambiente;
- Validação.

---

## 8. Regras de persistência

Antes de liberar qualquer nova gravação:

1. entender o bloco/formato real;
2. ter fixture/teste representativo;
3. preservar encoding;
4. preservar linhas/seções desconhecidas;
5. criar backup;
6. conseguir reabrir o arquivo;
7. validar que o OMSI ainda aceita o resultado;
8. não normalizar/reformatar conteúdo alheio à alteração sem necessidade.

Quando não houver segurança, a ferramenta deve ficar em preview/read-only em vez de gravar um formato inventado.

---

## 9. Testes obrigatórios por família de recurso

Cada recurso novo deve considerar:

- mapa padrão do OMSI;
- mapa pequeno;
- mapa grande;
- asset ausente;
- asset protegido;
- caminhos relativos;
- encoding não UTF-8;
- objetos repetidos;
- splines curvas;
- spline_h quando aplicável;
- tiles vizinhos;
- edição na borda de tile;
- fullscreen e desktop;
- undo/redo;
- save + reopen;
- backup;
- mapa criado por terceiros.

Para sistemas operacionais do mapa, acrescentar fixtures específicas de traffic rules, tracks/trips, timetable e sinais conforme cada fase entrar.

---

## 10. Critério para “substituto do editor OMSI”

Não declararemos o Map Studio substituto completo apenas porque ele renderiza mapas.

Para atingir esse marco, ele deve conseguir, de forma segura:

- abrir mapas reais;
- navegar por todos os tiles relevantes;
- editar terreno;
- criar/excluir tiles;
- colocar/editar/remover objetos;
- colocar/editar/remover splines;
- editar attachments;
- editar propriedades completas essenciais;
- editar paths e regras de tráfego;
- configurar cruzamentos/sinais necessários;
- criar stops/tracks/trips;
- criar e editar horários;
- validar o mapa;
- salvar sem corromper conteúdo desconhecido;
- reabrir o resultado;
- produzir mapa aceito pelo OMSI 2.

Depois desse marco, recursos modernos continuam sendo diferenciais e não requisitos de paridade.

---

## 11. O que não faremos

- não reescrever tudo em outra engine apenas por estética;
- não usar mocks para mascarar parser ausente;
- não converter silenciosamente assets;
- não substituir conteúdo protegido por geometria falsa;
- não gravar parâmetros OMSI cujo significado não foi validado;
- não carregar o mapa inteiro e toda a instalação na GPU por padrão;
- não acoplar o projeto ao OMSI NavBR Multiplayer;
- não sacrificar compatibilidade para copiar exatamente a UX de outro jogo.

---

## 12. Próxima sequência prática

A sequência recomendada a partir do estado atual é:

1. estabilizar a UI/picking atual e concluir a migração visual para o pacote SVG próprio;
2. ampliar o cache persistente para geometria/material/thumbnail derivados;
3. completar fila de carregamento por prioridade, descarte de GPU, instancing e LOD seguro;
4. completar operações físicas que faltam;
5. implementar paths + Traffic Rules;
6. implementar transporte: stops/tracks/trips/timetables;
7. implementar sinais/ferrovia;
8. ambiente/cronologia;
9. ampliar ferramentas procedurais e geográficas;
10. consolidar Map Health/validation até atingir paridade operacional.

Esta ordem pode ser ajustada quando uma dependência técnica exigir, mas nenhuma família listada acima deve ser esquecida.


---

## 13. Regra de manutenção do roadmap

Este arquivo não é apenas uma lista de ideias.

Ao implementar uma família de recursos descrita aqui:

- atualizar o estado correspondente de ⬜ para 🟡 ou ✅;
- registrar limitações que permanecerem;
- acrescentar novas dependências descobertas;
- mover tarefas entre fases somente quando existir motivo técnico;
- nunca remover uma lacuna apenas porque ficou difícil;
- manter pt-BR e en sincronizados;
- quando uma decisão arquitetural mudar, registrar a nova decisão e o motivo.

Novas ideias relevantes para substituir o editor original ou ampliar o editor moderno devem entrar neste roadmap antes de serem consideradas “lembradas pelo projeto”.


### Identidade visual e pacote de ícones

O Map Studio usará um pacote SVG próprio, inspirado na atmosfera técnica do OMSI sem copiar assets proprietários. A especificação completa está em [ICON_SYSTEM.md](ICON_SYSTEM.md).

A migração acontecerá por grupos: barra rápida, HUD de construção, Explorer/Inspector, menus, biblioteca, diagnósticos e futuras ferramentas OMSI. O primeiro lote já migrou a barra rápida e o HUD principal de construção; os demais grupos continuam em progresso.


---

## Fase de estabilização do viewport — prioridade imediata

Antes de ampliar novas ferramentas de construção, o viewport precisa ser simplificado e estabilizado.

### Auditoria do estado atual

A UI cresceu além do ponto seguro para continuar corrigindo seleção por adição de novos fallbacks:

- `Viewport.tsx` possui cerca de **9,5 mil linhas**;
- `App.tsx` possui cerca de **25 mil linhas**;
- o viewport registra múltiplos listeners de ponteiro/teclado e contém mais de uma estratégia de picking;
- seleção, hover, câmera, placement, gizmos, splines, terreno e lifecycle da cena ainda dividem o mesmo componente;
- existem atualmente raycast de malha, seleção por caixa projetada, volume visual e fallback geométrico.

Isso torna difícil provar qual rota decidiu o clique e aumenta o risco de um ajuste corrigir um cenário e quebrar outro.

### Decisão

**Não trocar a stack inteira neste momento.** O Core .NET, parsing, persistência, React e a ponte WebView2 permanecem válidos.

A próxima etapa será reescrever o viewport como um runtime imperativo isolado do React:

1. `ViewportRuntime` — cria Engine/Scene/Camera uma única vez;
2. `ViewportInputController` — única autoridade sobre mouse/teclado;
3. `ViewportSelectionController` — uma única rota de seleção;
4. `ViewportObjectRegistry` — associa IDs OMSI aos meshes/proxies;
5. `ViewportSplineRegistry` — associa IDs OMSI aos perfis/eixos;
6. `ViewportGizmoController` — mover/rotacionar sem recriar a cena;
7. React recebe somente eventos de alto nível, como `objectSelected` e `splineSelected`.

O seletor de produção deverá usar **proxies de picking simples e determinísticos**, independentes de material/textura do O3D. Raycast contra geometria visual será apenas otimização, não requisito para conseguir selecionar.

### Critério para trocar tecnologia

Babylon/WebView2 só será substituído se um protótipo isolado, sem React controlando o lifecycle, falhar em algum destes critérios:

- clique 1:1 em DPI 100%, 125%, 150% e 200%;
- seleção consistente de objetos, árvores e splines;
- mover/rotacionar sem recriar Scene;
- mapa Gundorf sem flicker;
- navegação e seleção simultâneas sem conflito;
- centenas de objetos selecionáveis sem degradação perceptível.

Se esses critérios falharem no runtime mínimo, a substituição deve atingir **somente o renderer/viewport**, preservando MapStudio.Core, formatos, cache, persistência e o restante do produto.


## Novos diferenciais nativos — geração procedural e IA

Além da paridade com o editor antigo, o roadmap passa a incluir oficialmente:

- **geração automática de ruas** por traçado manual sobre o terreno;
- **geração assistida por referência georreferenciada**, convertendo um grafo vetorial em splines OMSI reais;
- **cruzamentos automáticos** derivados do mesmo grafo, com snap e validação de conectividade;
- **Road Kit próprio do Map Studio**, sem dependência obrigatória de conteúdo de terceiros;
- **Building Studio** para casas, prédios e outros volumes, gerando SCO/O3D editáveis;
- uso opcional de fotos como referência/textura de fachada;
- **IA conectável por adaptadores**, sem fornecedor obrigatório;
- análise por IA apenas como sugestão estruturada e revisável;
- suporte futuro a múltiplas fotos, extração de contorno, estimativa de escala, fachada, telhado, materiais, vegetação e mobiliário urbano;
- preview obrigatório antes de persistir geração automática em mapa real.

A prioridade de implementação é manter um único motor geométrico: traçado manual, dados vetoriais e IA devem alimentar o mesmo pipeline de geração, evitando formatos paralelos.


---

## Atualização arquitetural — host nativo é a direção atual

A seção histórica **“Fase de estabilização do viewport”** acima registra a análise que levou à mudança de arquitetura. A decisão posterior já foi executada: o viewport de produção passou para **WinUI 3 + Direct3D 11**. React/WebView2 não é mais a direção do renderer principal.

Estado atual dos diferenciais novos:

- ✅ editor standalone com Workspace próprio, template/terreno inicial e criação/edição sem instalação do OMSI;
- ✅ importação opcional de pastas de itens SCO/SLI para a biblioteca do Workspace;
- ✅ atalho **Abrir OMSI** preservado como fonte opcional de mapas/assets;
- ✅ chrome nativo remodelado com menu + ribbon desktop e barra de editor específica no fullscreen;
- ✅ Road Kit próprio;
- ✅ criador paramétrico de túneis SLI com pista, marcações, paredes, teto em arco, paths de tráfego e uso da Estrada fácil em curvas/gradientes;
- ✅ Cant/Mirror avançado exposto no Inspector nativo com persistência preservativa e backup;
- ✅ Complete to nativo com solver tangencial conservador, limite de raio e conexão Previous/Next transacional;
- ✅ Spline Export nativo `.x` com seleção múltipla, malha real SLI e UVs para Blender;
- ✅ grafo procedural de vias;
- ✅ traçado manual;
- ✅ GeoJSON georreferenciado;
- ✅ OSM XML georreferenciado;
- ✅ análise de vias da referência Google por IA;
- ✅ preview D3D11 antes de persistir;
- ✅ auto-link em continuidade linear segura;
- ✅ junctions procedurais próprios;
- ✅ persistência em batch com backup/rollback;
- ✅ Building Studio procedural com O3D/SCO;
- ✅ múltiplos tipos de telhado e aberturas de fachada;
- ✅ contratos de IA independentes de fornecedor;
- ✅ camada comercial/entitlements preparada, ainda sem cobrança aplicada no Alpha.

A remoção de tiles intermediários continua conscientemente bloqueada até existir um reindexador que prove e atualize todas as referências dependentes do índice do tile. Essa limitação não deve ser contornada apenas removendo uma seção `[map]`.
