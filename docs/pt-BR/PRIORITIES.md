# Prioridades de desenvolvimento — OMSI Map Studio

Atualizado para a branch `feature/native-winui-d3d11`.

Esta fila é a referência prática para continuar o desenvolvimento. O PR #3 permanece DRAFT e não deve ser mesclado enquanto os bloqueadores de Alpha não forem encerrados.

## P0 — bloquear uma Alpha somente por estabilidade

Objetivo: produzir uma nova build nativa que possa ser validada sem risco conhecido de corrupção, desalinhamento de tiles ou regressão básica de edição.

- ✅ Grade OMSI canônica de 300 m, incluindo coordenadas negativas.
- ✅ Costura de bordas de `.terrain` ao criar tiles.
- ✅ Persistência 3×3 no `global.cfg`.
- ✅ Centralização correta de áreas pares e ímpares no Criar mapa real por área.
- ✅ Preservar a câmera ao criar/excluir tiles.
- ✅ Teste automático de criação sequencial 3×3.
- ✅ Round-trip em disco: criar catálogo 3×3, gravar terrenos, reabrir pelo Core e validar todas as bordas compartilhadas.
- ⏭ Gerar a próxima Native Feature Preview a partir de um HEAD verde.
- ⏭ Smoke test no executável: criar tiles ±X/±Y, 3×3, 2×2/4×4 por área, salvar, fechar e reabrir.
- ⏭ Smoke test de seleção, Mover/Girar, Undo/Redo, exclusão em lote e salvamento sem Inspector.
- ⏭ Validar inicialização/instalação/portable e logs de crash antes de promover a build.

Critério para sair do P0: a build candidata abre, edita, salva e reabre mapas de teste sem desalinhamento de tile, corrupção ou crash reproduzível nos fluxos principais.

## P1 — desempenho e mapas grandes

Objetivo: tornar o editor previsível em mapas reais grandes.

- Cache derivado persistente de geometria, material e thumbnail.
- Fila completa de carregamento por prioridade.
- Descarte controlado de recursos CPU/GPU fora da região ativa.
- Instancing onde for seguro.
- LOD para terreno/objetos/splines onde não prejudique picking ou edição.
- Métricas de abertura, streaming, uso de memória e tempo de rebuild.
- Garantir seleção/edição contínua durante streaming.

Critério para sair do P1: mover pelo mapa não exige reprocessar a instalação inteira e o uso de memória/GPU permanece controlado.

## P2 — paridade operacional com o editor OMSI

Objetivo: não depender do editor antigo para as tarefas principais de construção/operação.

- Propriedades completas de tile.
- Lightmap/iluminação de tile validada.
- Attachments completos, incluindo campos ainda somente leitura.
- Parent/hierarchy e opções/labels específicas de objetos.
- Mirror de spline.
- Remoção segura de tile intermediário com reindexação de referências.
- Paths totalmente editáveis e diagnóstico de continuidade.
- AI paths, crossings e comportamento de tráfego avançado.
- Timetable avançado: calendários/serviços e casos operacionais restantes.
- Signal Routes.
- Ferrovia: switches/agulhas, prioridades e level crossings.
- Environment.
- Chronology.
- Suporte formal de leitura/escrita para mapas `[worldcoordinates]`.
- Map Health completo: paths, splines, attachments, IDs, timetable, sinais, terreno e referências.

Critério para sair do P2: um mapa OMSI pode ser construído, operado, validado e salvo sem retornar ao editor original nos fluxos essenciais.

## P3 — recursos modernos e expansão

Objetivo: ampliar o Map Studio além da paridade do editor original.

- OSM multipolygon com `inner rings`/courtyards seguros.
- DEM e validação de datum/offset mais avançados.
- Auto-junction e ferramentas procedurais adicionais.
- Lotes, bairros, distribuição e alinhamento avançados.
- Evolução dos Building/Road/Bridge/Tunnel Studios.
- Assistência por IA adicional sem tornar funções principais dependentes de IA.
- Adapters para outros simuladores somente quando houver implementação real.

## Fora desta branch

Licenciamento, assinatura, Stripe, serial, site comercial e enforcement de entitlement permanecem em fluxo/branch separado e não entram em `feature/native-winui-d3d11`.
