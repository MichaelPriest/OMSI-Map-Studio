# Exportação Proton Bus — especificação técnica inicial

Este documento registra a pesquisa e a arquitetura inicial para adicionar Proton Bus como alvo de exportação do OMSI Map Studio sem misturar o formato Proton Bus com os parsers/writers do OMSI.

## Estado desta branch

Branch: `feature/protonbus-map-export`

Base inicial confirmada em 2026-09-23:

`5bf80d739d7cdb57a19f0e106e4d2b85c82032f5`

A `main` não é alterada por este trabalho.

O suporte Proton Bus ainda não deve ser anunciado como completo. Nesta primeira etapa foram adicionados apenas:

- modelo do manifesto do mapa;
- writer do arquivo `.map.txt`;
- layout de pacote;
- validação de nomes/caminhos portáveis;
- constantes para nomes especiais de meshes;
- testes unitários;
- este plano de implementação.

## Fontes pesquisadas

Fontes principais:

- tutorial oficial experimental de mapas de 2019:
  https://blog.protonbus.com.br/2019/09/experimental-mods-de-mapas-no-pbs.html
- página oficial da Fase 4:
  https://www.protonbus.com.br/arvores/
- tutorial Fase 3, de Marcos Elias, atualizado em setembro de 2021, encontrado em cópias espelhadas:
  https://pt.scribd.com/document/618606511/Tutorial-Mods-de-Mapas-Fase-3

A documentação Fase 3 encontrada em espelhos deve ser tratada como referência técnica histórica até ser confrontada com os arquivos de ajuda da build-alvo atual do Proton Bus.

## Estrutura básica confirmada

O Proton Bus usa um arquivo `.map.txt` de texto e uma pasta base dentro de `maps` nas Fases 2/3 documentadas.

Exemplo:

```text
maps/
  Cidade.map.txt
  Cidade/
    dest/
    skins/
    textures/
    tiles/
      Rota 1/
        *.3ds
        aipeople/
        aitrains/
        aivehicles/
        busstops/
        trafficlights/
        streetlights/
```

Manifesto básico:

```ini
[map]
baseDir=Cidade
modelsDir=Rota 1
textures=textures
mapModVersion=3
preview=preview
```

Na Fase 3, `mapModVersion=3` habilita os recursos correspondentes daquela geração do sistema de mapas. Versões futuras precisam ser validadas contra a build Proton Bus de destino antes de serem emitidas automaticamente.

## Regras de compatibilidade importantes

Os materiais e objetos do Proton usam nomes especiais no 3D. Entre os comandos documentados estão:

- `_transparent_` — transparência;
- `_gencol_` — gera colisor;
- `_invisible_` — objeto invisível, normalmente combinado com colisor;
- `_emissive_` — emissivo;
- `_additive_` — shader aditivo, útil em semáforos;
- `_low_speed_zone_` — área de velocidade baixa;
- `_force_exit_` — força desembarque.

A documentação histórica também recomenda nomes de arquivos sem acentos, cedilha ou caracteres especiais para evitar diferenças entre sistemas operacionais.

Texturas PNG são a opção mais segura nas instruções antigas. Para destino mobile, a documentação histórica recomenda evitar dimensões acima de 2048 px.

## Diferença estrutural para OMSI

Não devemos converter Proton Bus como uma simples troca de extensão.

OMSI trabalha fortemente com:

- tiles;
- splines;
- sceneryobjects;
- referências externas;
- paths;
- arquivos de timetable;
- terreno por tile.

O Proton Bus historicamente recebe cenário como modelos `.3ds` e configurações TXT associadas. O mapa também é carregado de forma muito mais agregada.

Portanto o pipeline correto é:

```text
Map Studio Domain
       |
       +-- geometria/terreno
       +-- ruas e calçadas
       +-- objetos
       +-- paths
       +-- paradas
       +-- semáforos
       +-- luzes
       +-- tráfego/pedestres/trens
       |
       v
ProtonBus Export Scene
       |
       +-- tesselação de splines
       +-- transformação de coordenadas
       +-- bake/instancing conforme capacidade
       +-- materiais e tags Proton
       +-- marker meshes
       |
       v
3DS + TXT + PNG + .map.txt
```

## Mapeamento inicial Map Studio -> Proton Bus

### Terreno

O terreno do Map Studio deve ser triangulado em meshes exportáveis. Tiles OMSI não devem permanecer como requisito do pacote final Proton.

### Ruas e splines

Splines precisam ser tesselladas em malha real:

- pista;
- calçada;
- meio-fio;
- faixas;
- acostamentos quando existirem;
- elevação;
- curvas;
- pontes/túneis.

Partes dirigíveis ou com contato físico devem receber colisores adequados sem transformar todo o mapa em um único colisor gigante.

### Objetos de cenário

Objetos podem ser:

1. incorporados/baked aos modelos `.3ds`; ou
2. convertidos para o sistema de prefabs reutilizáveis da Fase 4 quando a build-alvo suportar isso.

A escolha deve ser feita pelo perfil de exportação.

### Paradas e passageiros

As posições editadas visualmente no Map Studio deverão gerar:

- marker objects necessários no 3D;
- arquivos em `busstops/`;
- configuração de embarque/desembarque;
- destino/linha quando aplicável.

### Pedestres, tráfego e trens

Os paths internos do Map Studio devem ser convertidos para os objetos de posição e TXT esperados em:

- `aipeople/`;
- `aivehicles/`;
- `aitrains/`.

A conversão não deve depender do Inspector; o Inspector pode editar propriedades avançadas, mas o fluxo principal deve funcionar pelas ferramentas visuais do editor.

### Semáforos

O modelo do Map Studio precisa ser traduzido para:

- arquivo de máquina em `trafficlights/`;
- prefixo exclusivo;
- quantidade de paths;
- ticks/estados;
- tempos;
- luzes vermelho/amarelo/verde;
- triggers de bloqueio;
- marker meshes correspondentes no 3D.

### Iluminação

Tipos de poste/ambiente deverão gerar arquivos em `streetlights/` e marker meshes para luz real/fake quando o alvo oferecer suporte.

### GPS/rotas

Será tratado como etapa própria depois que o formato atual da build-alvo for validado em um mapa real.

## Exportador 3DS

A direção preferencial é um writer 3DS próprio no Map Studio.

Motivos:

- não obrigar o usuário a instalar Blender 2.79;
- preservar nomes longos usados pelos comandos Proton;
- exportar diretamente da geometria já carregada no editor;
- permitir testes determinísticos;
- evitar automação externa frágil.

O Blender poderá existir futuramente como opção de interoperabilidade, não como dependência obrigatória.

O writer deverá cobrir pelo menos:

- vertices;
- faces triangulares;
- UV;
- materiais;
- referência de textura;
- transformação;
- nomes de objetos;
- divisão automática para limites do formato 3DS;
- preservação das tags Proton.

## Fase 4

A página oficial da Fase 4 descreve:

- vegetação automática/aleatória;
- prefabs automáticos/aleatórios;
- prefabs posicionados manualmente;
- tráfego personalizado;
- divisão do mundo em grids internos para otimização.

Esses recursos serão adicionados somente após analisarmos os arquivos de ajuda/exemplo da build alvo. Não será inventado formato com base apenas na descrição pública.

## Etapas de implementação

### P0 — concluído nesta branch

- modelo `ProtonBusMapDefinition`;
- writer do manifesto;
- layout do pacote;
- validação portável;
- tags básicas;
- testes.

### P1 — concluído no Core

- modelo intermediário `ProtonBusExportScene`;
- transformação explícita do espaço Y-up do Map Studio para o espaço 3DS usado pelo pipeline Proton;
- correção da ordem dos triângulos após a troca Y/Z;
- tesselação de splines OMSI com reta, curva, perfil, UV e gradiente;
- triangulação de terreno OMSI por tile;
- amostragem bilinear do terreno para posicionamento de objetos;
- conversão de objetos OMSI carregados pelo Core (`.o3d` e `.x`) com posição, rotação, escala e UV;
- preservação de altura relativa e `[absheight]`;
- materiais com textura, cor difusa, opacidade e emissive básico;
- nomes de materiais globalmente únicos por tile/objeto;
- planejamento e divisão automática de chunks 3DS;
- remapeamento local de índices para os limites do 3DS;
- montagem de `ProtonBusExportScene` por tile, com relatório de assets ausentes.

### P2 — iniciada

Concluído:

- writer binário `.3ds` nativo para meshes estáticas;
- chunks de material, objeto, vértices, faces, associação de material e UV;
- nomes completos de objetos/texturas, sem o truncamento legado de 12 caracteres;
- textura difusa;
- transparência e self-illumination básicas;
- divisão automática de meshes grandes.

Pendente:

- validar o arquivo gerado diretamente na build alvo do Proton Bus;
- conversão real das texturas de origem para PNG (hoje o pacote exige PNG, mas não recodifica DDS/BMP/TGA);
- regras finais para colisores de scenery conforme metadata/uso do objeto;
- regras finais para emissive/additive;
- fixture real para validar visualmente eixos, winding, UV e materiais.

### P3

- entry points;
- paradas/passageiros;
- pedestres;
- veículos de tráfego;
- trens.

### P4

- semáforos;
- street lights;
- GPS/rotas;
- validação completa do pacote.

### P5

- recursos Fase 4;
- prefabs;
- vegetação automática;
- otimizações específicas;
- perfil PC/mobile.

### P6

- UI `Exportar > Proton Bus`;
- relatório pré-exportação;
- seleção da versão alvo;
- exportação ZIP;
- teste contra mapa exemplo real.

## Critério para marcar Proton Bus como suportado

O adapter Proton Bus só deve ser registrado como funcional no `MapStudioSimulatorRegistry` quando:

1. um mapa simples puder ser exportado sem Blender;
2. o pacote abrir na build Proton Bus alvo;
3. terreno, ruas e cenário aparecerem corretamente;
4. colisão funcionar;
5. pelo menos uma rota com parada funcionar;
6. tráfego/pedestres essenciais forem validados;
7. os testes automatizados passarem;
8. houver um mapa de fixture pequeno e redistribuível ou gerado pelo próprio teste.

Até lá, `MapStudioSimulatorIds.ProtonBus` permanece apenas como ID conhecido.
