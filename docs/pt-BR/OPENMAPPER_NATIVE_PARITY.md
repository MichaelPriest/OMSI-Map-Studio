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


### Elevação real sem chave: AWS Terrarium

O assistente de mapa real oferece AWS Terrarium / Mapzen como fonte adicional, sem API key, tanto para terreno isolado como para reconstrução automática. Os PNGs Terrarium são decodificados em um único componente compartilhado entre Core e Renderer, com cache local, interpolação entre pixels e tiles e resolução adaptada à extensão. O pipeline aplica uma superfície e uma cota de referência comum à região. O endpoint público Open-Meteo também pode ser usado sem chave no modo por tile. A resolução nativa varia por região e a grade de amostragem limita o detalhe final; não representa levantamento topográfico de engenharia. Homologação visual e testes com dados reais pendentes.

**EN:** AWS Terrarium / Mapzen is available as a keyless terrain provider for both terrain-only and full reconstruction. Shared PNG decoding, cached tiles, bilinear sampling and a common regional baseline are used. The public Open-Meteo per-tile key guard was removed. Actual source resolution varies; live visual testing remains required.
