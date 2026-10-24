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