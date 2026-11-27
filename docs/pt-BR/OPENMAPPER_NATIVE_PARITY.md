# Plano de paridade funcional do Map Studio Native

## Objetivo

Evoluir o editor nativo usando o openMapper como referência de experiência e cobertura funcional, mantendo o Core .NET como autoridade para ler, validar e gravar os formatos reais do OMSI 2.

Referência: https://github.com/openOMSI-org/openMapper

## Decisões de arquitetura e licença

- Manter o Native em .NET 10, WinUI 3 e Direct3D 11/Vortice.Windows.
- Usar o openMapper como referência de comportamento e fluxo de trabalho; não portar o renderer Rust/wgpu/egui para dentro do Native.
- A referência openMapper é AGPL-3.0-only. Não copiar código, shaders ou assets sem análise de licença explícita.
- Não escrever na instalação original do OMSI 2; alterações devem ocorrer no workspace selecionado.

## Estado e prioridades de paridade

Cada recurso deve ser classificado como **equivalente**, **parcial**, **ausente** ou **não aplicável**. A presença de um botão ou classe não comprova paridade: ações de persistência exigem salvar, fechar e reabrir um mapa real.

| Prioridade | Área | Critério de aceite |
| --- | --- | --- |
| P0 | Criar/abrir mapas e segurança do workspace | Nomes inválidos, traversal, colisões e nomes excessivos são rejeitados; mapas existentes nunca são sobrescritos; mapa criado é legível pelo Core |
| P1 | Viewport, câmera e seleção | Orbit/pan/zoom/fly previsíveis; seleção legível sem ocultar materiais; hit-test e handles utilizáveis em diferentes escalas |
| P2 | Estradas, splines e junções | Perfis .sli reais, geometria e vínculos corretos; estradas em níveis diferentes não são conectadas indevidamente; dados sobrevivem ao round-trip |
| P3 | Objetos, catálogo e terreno | Preview corresponde ao asset real; escala, rotação, textura, altura e tile corretos; terreno e máscaras preservados após reload |
| P4 | Tráfego, importação e verificação | Recursos só são declarados suportados quando leitores, escritores e testes reais existem; relatórios não alteram o mapa silenciosamente |
| P5 | Robustez e desempenho | Salvamento recuperável, logs acionáveis e medições em mapas pequenos e grandes |

## Regras de validação

- Não remover nem enfraquecer testes para obter CI verde.
- Não usar mapas fictícios ou geometria OMSI inventada como substituto de validação de formatos reais.
- Não declarar suporte a um formato sem reader, writer e teste de round-trip.
- Mudanças visuais precisam de inspeção com malhas e texturas reais, incluindo transparência, faces duplas e materiais dia/noite quando aplicável.
- Falhas de renderização ou persistência devem gerar diagnóstico útil, sem incluir segredos.
- Integração na `main` depende de CI concluído; deploy é uma etapa separada e requer autorização explícita.

## Referências internas

- Arquitetura nativa: `docs/pt-BR/NATIVE_MIGRATION.md`
- Core e contratos OMSI: `src/MapStudio.Core`
- Renderer e viewport: `src/MapStudio.Renderer`


### Seleção de camadas do mapa real

Na etapa Reconstrução, o modo automático permite escolher vias/cruzamentos, edificações, vegetação, infraestrutura e mobiliário urbano separadamente. A seleção padrão continua completa. A elevação e a referência visual mantêm controles independentes. As camadas desmarcadas não executam seus geradores; a sessão conserva backup e rollback. A seleção de classes de vias/SLI, água, linhas OSM e DEM Terrarium continuam pendentes de paridade.


### Referência gratuita sem chave

OpenStreetMap é o mapa-base padrão. A referência sobre o terreno é gerada localmente em PNG a partir da geometria vetorial OSM, usando a mesma projeção geográfica do gerador de vias, com contornos de edificações e vias por largura. A região é limitada ao tile ativo e vizinhos carregados (até 3 × 3); o cache de dados OSM é reaproveitado. Não são baixados mosaicos de tiles raster públicos para uso offline. Atribuição ODbL visível; Google e CARTO seguem opcionais. Esri World Imagery é uma opção separada para visualização online quando o usuário possui licença/autorização; não é incluída no PNG local nem exportada. Esta referência ainda não desenha rótulos, água ou vegetação.

### Keyless reference (EN)

OpenStreetMap is the default map picker. The local ground overlay renders OSM vector road and building geometry into georeferenced PNG, without Google or CARTO credentials, within the active tile and loaded neighbors. It uses the existing OSM data cache, not bulk public raster tile downloads. Esri is a separate authorized online imagery preview, not an export/offline source. Labels, water and vegetation in the reference remain pending.

## Auditoria de paridade — 27/11/2026

Base externa: openMapper main, docs/FEATURES.md (licença AGPL-3.0-only). Comparação documental e estática, não homologação executável.

| Prioridade | Diferença | Estado Map Studio |
|---|---|---|
| P0 | Desenho em dois cliques, tangência entre trechos, edição numérica durante desenho | Parcial; motor de splines existente |
| P0 | Importação GPS GPX/KML/GeoJSON | Importador GPX/KML integrado ao fluxo existente de prévia de grafo; testes adicionados; altitude ignorada, sem download externo |
| P0 | Importação por classe OSM, água, trilhos, linhas de ônibus | Parcial; validar cada gerador |
| P0 | DEM Terrarium e provedores ortofoto autorizados | Pendente; Open-Meteo e referência vetorial OSM existentes |
| P1 | Perfil longitudinal editável, régua, área, snap angular | Não localizada equivalência completa |
| P1 | Rotatórias, baias, cruzamentos complexos reutilizáveis | Parcial/ausente |
| P1 | HOF, calendários, reroteamento TTData ao mudar ruas | Não confirmada paridade |
| P2 | Scripts, colaboração, edição de malha, plano SVG, merge de tiles | Avaliar separadamente; ausência não comprovada por execução |

Não copiar código AGPL; implementação GPS independente baseada nos formatos XML e no grafo próprio. Não declarar paridade até testes de mapa real e reload.

### Desenho direto — paridade de interação

Estrada fácil confirma por padrão tanto no segundo clique quanto ao soltar um arrasto. O botão Revisão alterna para a confirmação manual original, inclusive nos trechos contínuos. O modo usa a solução geométrica existente e a mesma inserção com backup; não muda os formatos OMSI. Homologar primeiro trecho, continuação, cancelamento, revisão, encaixe em via existente e falha de geometria. A confirmação de curvas manuais continua independente.

### Desenho numérico de vias

Road Studio > Precisão: definir início no mapa e informar comprimento de arco, raio assinado e direção inicial. Com conexão anterior, a tangente real substitui a direção digitada. Cota final segue modo de elevação; raio zero representa reta. Limites conservadores: 0,5–3000 m, raio mínimo 6 m, arco até 180°. Não encaixa automaticamente o fim para não alterar a geometria exata; conexão final permanece manual. Reutiliza inserção, backup e continuidade existentes. Testes de geometria adicionados; homologação de UI pendente.
