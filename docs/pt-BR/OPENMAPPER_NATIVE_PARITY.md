# Plano de paridade nativa inspirado no openMapper

## Objetivo

Usar o openMapper como referência funcional e técnica para evoluir o viewport e as ferramentas do Map Studio Native, preservando o Core .NET como autoridade para ler, validar e gravar os formatos OMSI 2.

Referência analisada: https://github.com/openOMSI-org/openMapper (README e release notes disponíveis em 24/10/2026).

## Decisão de arquitetura

Não portar o renderer Rust/wgpu/egui para dentro do Native. O Map Studio Native continua em .NET 10, WinUI 3 e Direct3D 11/Vortice.Windows. O openMapper serve como referência para comportamento do editor, fluxos de trabalho, verificações e critérios de qualidade; a implementação deve usar as APIs e os modelos existentes no Map Studio.

A licença atual do openMapper é AGPL-3.0-only. Portanto, este plano não copia código, shaders, assets ou trechos de implementação. Qualquer reutilização literal futura exige uma análise de licença explícita antes de começar.

## Mapeamento de recursos

| Referência do openMapper | Direção para Map Studio Native | Critério verificável |
| --- | --- | --- |
| Câmera de editor com fly, orbit e trackpad | Unificar navegação do viewport: orbit, pan, zoom e fly; respeitar foco do painel e evitar scroll acidental nos painéis laterais | Testes de mapeamento de entrada e teste manual de navegação em mapa pequeno e grande |
| Seleção que não oculta o objeto selecionado | Contorno de seleção nas bordas e grips dimensionados por tela; não pintar toda a spline/objeto por cima da geometria | Seleção mantém materiais e silhueta legíveis em diferentes distâncias |
| Grips para remodelar splines | Usar handles com hit-test tolerante em pixels, independente da distância da câmera | Seleção e arrasto funcionam em splines estreitas e largas |
| Estradas e junções a partir de pontos | Melhorar edição de splines e junções em cima dos perfis reais .sli, sem inventar formatos OMSI | Geometria, orientação e vínculos persistem após salvar e reabrir |
| Escultura, rampas e pintura de terreno | Usar o modelo de terreno/Core atual e expor feedback visual consistente no viewport nativo | Alturas, bordas entre tiles e máscaras de textura não se perdem após reload |
| Catálogo visual de objetos e splines | Manter biblioteca existente e melhorar preview, posicionamento e feedback de encaixe | Preview corresponde ao asset selecionado e placement grava no tile correto |
| Outliner, camadas, histórico Undo/Redo | Integrar seleção, visibilidade por camada e histórico transacional ao modelo de edição existente | Undo/Redo reverte modelo e arquivos sem deixar estado visual divergente |
| Verificações do mapa | Consolidar validações para arquivos ausentes, junções de estradas, vínculos de transporte e IDs duplicados | Relatório reproduzível; validações não corrigem nem alteram o mapa silenciosamente |
| Salvamento seguro e versões anteriores | Preservar transações seguras já existentes e ampliar testes de falha/reabertura | Interrupção durante gravação não destrói a última versão válida |
| Log de diagnóstico claro | Garantir log de inicialização e de renderização com versão, adapter gráfico, operação e exceção | Falha não fica silenciosa; log não inclui segredos ou dados desnecessários |

## Ordem de implementação

### P0 — Contratos e segurança de criação

1. Verificar o fluxo de criação de mapa do Native ponta a ponta: XAML/command -> sessão nativa -> Core workspace bootstrapper -> arquivos gravados -> reload.
2. Garantir que os campos enviados pelo formulário sejam os mesmos esperados pelo Core; mensagens de validação devem diferenciar nome inválido, mapa existente e falha de I/O, sem esconder a exceção original nos logs.
3. Adicionar testes de contrato para nomes vazios, espaços, caracteres proibidos, caminhos/traversal, colisão de nome e criação válida.
4. Confirmar que mapas existentes nunca são sobrescritos pela operação Novo mapa.

### P1 — Viewport e seleção

1. Inventariar o comportamento atual da câmera e o hit-test antes de alterar APIs.
2. Melhorar feedback de seleção sem ocultar materiais ou a geometria selecionada.
3. Dimensionar handles em pixels de tela, não apenas em unidades do mundo.
4. Garantir que atualizações da cena sejam aplicadas atomicamente, mantendo a última cena válida durante reload.

### P2 — Estradas, splines e terreno

1. Comparar geometria renderizada com os perfis reais .sli e os dados de spline lidos pelo Core.
2. Corrigir orientação, largura, curvas, elevação e junções usando dados reais.
3. Verificar continuidade visual e dados de borda entre tiles.
4. Manter o writer OMSI atual e comprovar save -> reload -> comparação de dados.

### P3 — Ferramentas e validação

1. Melhorar catálogo/preview e posicionamento de assets.
2. Expor verificações de mapa com mensagens acionáveis.
3. Adicionar cenários de regressão com mapas de teste reais e fixtures mínimas legítimas.
4. Medir desempenho e memória em mapas pequenos e grandes.

## Regras de aceitação

- Não alterar main e não fazer merge automático da PR #3; ela permanece DRAFT.
- Não remover nem enfraquecer testes para obter CI verde.
- Não usar mapas falsos, usuários simulados ou geometria OMSI inventada como substituto de validação.
- Não escrever na instalação original do OMSI 2; trabalhar no workspace selecionado e respeitar o fluxo de importação/exportação.
- Não declarar suporte de um formato ou simulador sem leitor, writer e teste de round-trip reais.
- Toda correção de persistência deve provar leitura após gravação, não apenas sucesso visual no viewport.

## Referências

- openMapper: https://github.com/openOMSI-org/openMapper
- Notas de release e detalhes de UX: https://github.com/openOMSI-org/openMapper/releases
- Arquitetura atual do Map Studio Native: docs/pt-BR/NATIVE_MIGRATION.md
- Contratos e ferramentas de OMSI: src/MapStudio.Core

## Inventário ampliado de paridade funcional

A comparação não termina em estradas e viewport. O openMapper deve servir como referência de experiência e cobertura para todo o fluxo de autoria de mapas OMSI. Cada linha precisa ser auditada contra implementação real no Native; a existência de uma classe ou botão, isoladamente, não conta como recurso concluído.

| Área funcional | Comportamento de referência a comparar | Verificação no Native |
| --- | --- | --- |
| Criar, abrir e trocar mapas | Fluxo curto, mapas recentes e mensagens claras | Criar/abrir/salvar/reabrir um mapa sem sobrescrever outro nem perder alterações |
| Navegação e câmera | Orbit, pan, zoom, fly e visão geral | Atalhos consistentes, foco correto do viewport e navegação em mapas grandes |
| Seleção e edição | Seleção única/múltipla, caixa de seleção, mover, duplicar, copiar/colar e propriedades numéricas | Hit-test previsível, transformações persistidas e undo/redo coerente |
| Ruas e tipos de estrada | Desenho rápido por pontos, edição posterior, larguras/faixas/calçadas/texturas e perfis reais | Gerar e editar arquivos .sli válidos, com caminhos/faixas, altura e textura corretos |
| Junções e continuidade | Junções automáticas, curvas conectadas, pontas e cruzamentos | Testar geometrias e vínculos reais; nenhuma conexão pode ser apenas visual |
| Trilhos e splines especiais | Tipos de spline além de ruas, quando suportados pelo formato | Seleção do tipo correto, orientação e round-trip dos dados |
| Objetos e vegetação | Catálogo pesquisável, favoritos/recentes, preview fiel, posicionamento e áreas/pincel quando aplicável | Texturas, transparência, escala, rotação, origem e tile corretos; sem substituir assets reais por cubos fictícios |
| Terreno | Escultura, nivelamento, rampas, elevação importada, pintura por pincel/regra/estrada e bordas entre tiles | Preservar alturas, máscaras e texturas depois de salvar e reabrir |
| Água, luzes e cruzamentos | Criação e edição dos elementos de cena suportados pelo mapa | Persistência nos formatos OMSI corretos e representação visual identificável |
| Lógica de tráfego | Paradas, pontos iniciais, estacionamentos, caminhos AI, linhas/viagens/rotas e sinais, conforme formatos existentes | Vínculos válidos e verificáveis; não marcar como suportado sem reader/writer e teste |
| Importação e referência geográfica | OpenStreetMap, imagens aéreas, alturas, trilhas GPS e tiles de outros mapas | Importação reproduzível, projeção/escala conferidas e gravação apenas no destino escolhido |
| Verificação do mapa | Encontrar arquivos ausentes, splines desconectadas, paradas fora de caminhos e IDs conflitantes | Relatório acionável e não destrutivo; nenhuma correção silenciosa |
| Salvamento, recuperação e compartilhamento | Autosave, recuperação após falha, versões anteriores, cópia/exportação | Gravação segura, backup recuperável e teste de interrupção/round-trip |
| Desempenho e diagnóstico | Cenas grandes, medição de renderização e erros visíveis | Testes de mapas pequenos e grandes, logs úteis e ausência de travamentos silenciosos |
| Interface e acessibilidade de uso | Busca de comandos, atalhos consistentes, painéis claros e escala de interface | Ações principais fáceis de encontrar, sem depender de sequência obscura de cliques |

### Regra para declarar paridade

Cada recurso será marcado internamente como **equivalente**, **parcial**, **ausente** ou **não aplicável**, com evidência de código e teste. "Equivalente" exige que a ação funcione em mapa real e que os dados sobrevivam a salvar, fechar e reabrir. Recursos do openMapper que ainda não existem no Native serão tratados como trabalho de implementação, não como suporte presumido. A prioridade será: fluxo de edição básico e viewport, estradas/junções, objetos e terreno, depois lógica/importação e ferramentas de qualidade.

## Auditoria inicial do renderer (revisão de código, não teste visual)

A leitura inicial do renderer Native e do shader de malha do openMapper identificou estas diferenças e cuidados. Os itens abaixo são hipóteses de trabalho para confirmar com assets reais; não são declarações de defeito confirmado.

| Tema | Evidência no código atual | Próxima verificação |
| --- | --- | --- |
| Materiais OMSI | O Native já transporta alpha mode, no-Z-write/no-Z-check, double-sided, transmap, bump, environment, night/light maps e detail texture nos lotes de material | Montar casos de regressão com objetos reais que usem cada combinação; conferir transparência e ordenação com profundidade |
| Iluminação de objetos | O shader Native aplica iluminação adicional quando há bump map e reflexo quando há environment map; o openMapper tem um caminho de iluminação geral com normais frente/verso, fog e tonemapping | Comparar objetos sem bump/environment: garantir leitura visual adequada sem alterar a aparência dos materiais OMSI |
| Normais e faces duplas | O openMapper inverte a normal para faces traseiras no shader; Native já marca lotes double-sided, mas é necessário confirmar a combinação entre rasterizer, winding e normal usada na iluminação | Usar malhas reais de folhas, cercas e superfícies finas, incluindo objetos com faces invertidas |
| Transparência e transmap | Native possui shaders separados para alpha cutout/blend e transmap | Validar folhas, grades, vidros e materiais noturnos contra o jogo; não trocar o limiar de alpha sem evidência |
| LOD de objetos | O construtor de geometria Native seleciona atualmente o maior limiar de LOD encontrado no asset durante a montagem da geometria | Verificar como o snapshot e a câmera atualizam a cena; medir custo em mapas grandes antes de alterar estratégia de LOD |
| Seleção | O shader openMapper oferece contorno/rim para seleção; o Native usa caminhos de renderização e picking próprios | Inspecionar o passe de seleção do Native e preservar cor/textura da malha selecionada, com handles legíveis em escala de tela |

### Critério antes de mexer no shader

Nenhuma alteração visual será considerada correta apenas por compilar. A validação precisa incluir malhas e texturas reais do OMSI, seleção, faces duplas, materiais opacos e transparentes, dia/noite quando suportado, além de captura antes/depois. Se não houver asset de teste legítimo para uma característica, ela fica pendente em vez de ser declarada validada.
