# Testes — v0.1.0-alpha.3

[English](../en/TESTING.md) · **Português (Brasil)**

A **v0.1.0-alpha.3** já permite gravação preservativa experimental de transformações de objetos e splines existentes, além de inserção/cópia/exclusão segura de objetos dentro das limitações documentadas.

## Instalação

1. Baixe e extraia o pacote da Alpha.3.
2. Execute `OMSI Map Studio.exe`.
3. Clique em **Abrir OMSI** e selecione a pasta raiz do OMSI 2.
4. Clique em **Abrir mapa** e escolha manualmente uma pasta dentro de `maps`.

## Roteiro principal

- abra Grundorf ou outro mapa de teste;
- confirme que **Mapa completo** é o modo padrão;
- valide carregamento de tiles, objetos O3D e splines;
- selecione um objeto;
- pressione **W**, mova o gizmo e confirme **Prévia não salva**;
- pressione **E**, rotacione o objeto;
- use `Ctrl+Z` e `Ctrl+Y` para validar desfazer/refazer;
- na aba **Transformação**, digite um valor exato de X/Y/Z ou rotação e pressione Enter;
- pressione **F** para focar a seleção;
- use **G**, **O** e **L** para alternar grade, objetos e splines;
- clique em ↶ e confirme que a prévia volta ao valor original;
- repita uma transformação e clique **Salvar** ou use `Ctrl+S`;
- aguarde o recarregamento do mapa;
- confirme que a nova posição/rotação permanece após recarregar;
- confira a criação da pasta `.mapstudio-backups/<timestamp>/` no mapa;
- abra o tile salvo em editor de texto e confirme que seções desconhecidas/comentários não foram removidos.

## Teste de conflito

Com uma prévia pendente, altere externamente a identidade do mesmo bloco `[object]` (por exemplo ID ou caminho `.sco`) antes de clicar Salvar. O Map Studio deve cancelar o lote e mostrar um erro de conflito, sem sobrescrever silenciosamente o tile.

## Limitações conhecidas

- criação e cópia só gravam quando existe template seguro do mesmo `.sco`;
- criação instalada de `[spline]`/`[spline_h]` exige template neutro real do mesmo tipo no mapa;
- terreno binário `.terrain` ainda não é interpretado/editado;
- texturas de imagem de splines e O3D ainda não são aplicadas;
- mapas `[worldcoordinates]` continuam limitados;
- O3D criptografado e arquivos `.x` continuam sem preview geométrico.

## Segurança

Nunca use o único exemplar de um mapa importante durante esta alpha. Apesar do backup automático e da escrita preservativa, a funcionalidade de Save ainda é experimental.


## Teste de inserção pela Biblioteca

Use primeiro um `.sco` que já apareça no mapa de teste:

1. abra **Biblioteca** e busque pelo objeto;
2. clique **Colocar**;
3. clique em um tile;
4. confirme que a prévia aparece no ponto clicado;
5. altere Z, rotação, pitch e bank;
6. confirme **Confirmar e salvar**;
7. aguarde o recarregamento;
8. confira que o novo objeto recebeu um ID diferente de todos os objetos/splines existentes;
9. confira o backup do tile;
10. reabra o mapa no OMSI e valide a colocação.

Também escolha um `.sco` instalado que nunca foi usado no mapa. No modo Mapa completo, a prévia deve funcionar, mas a confirmação persistente deve permanecer bloqueada com a explicação de template indisponível.


## Teste de cópia do objeto selecionado

1. selecione um objeto existente com Z/rotação/pitch/bank fáceis de reconhecer;
2. opcionalmente crie uma prévia numérica sem salvar;
3. na aba **Geral**, clique **Colocar cópia**;
4. clique em outro ponto de um tile existente;
5. confirme que X/Y vieram do novo clique e que Z/rotação/pitch/bank começaram com os valores da seleção;
6. confirme **Confirmar e salvar**;
7. aguarde o recarregamento;
8. confirme que a cópia recebeu um ID global novo e que o objeto original permaneceu intacto;
9. verifique o backup em `.mapstudio-backups/<timestamp>/`.


## Teste de exclusão segura

1. selecione um objeto existente;
2. confirme que **Excluir objeto** está disponível sem prévias pendentes;
3. crie uma prévia e confirme que a exclusão fica bloqueada;
4. descarte a prévia;
5. clique **Excluir objeto** e confirme;
6. aguarde o recarregamento;
7. confirme que somente o objeto escolhido desapareceu;
8. confira o backup em `.mapstudio-backups/<timestamp>/`;
9. confirme no tile que comentários, linhas em branco e a seção seguinte foram preservados.

Para testar conflito, altere externamente o ID ou caminho `.sco` do objeto antes da confirmação. A exclusão deve ser cancelada sem sobrescrever o tile.


## Teste de edição de spline

1. selecione uma spline existente;
2. pressione **W**, mova o gizmo e confirme que o eixo/perfil acompanham o arraste;
3. pressione **E** e confirme que apenas a rotação horizontal da spline pode ser alterada;
4. confirme que o Snap configurado no viewport é respeitado;
5. abra **Traçado** e altere X/Y/Z, rotação, comprimento, raio ou um gradiente;
6. clique ✕ ou **Descartar prévia** e confirme o retorno aos valores originais;
7. repita uma alteração e use **Salvar spline**, o botão global **Salvar** ou `Ctrl+S`;
8. aguarde o recarregamento e confirme que a alteração persistiu;
9. confira o backup em `.mapstudio-backups/<timestamp>/`;
10. confira no tile que ID, previous/next, extras, comentários e seções desconhecidas não foram alterados.

Altere externamente ID, caminho `.sli`, tipo `[spline]`/ `[spline_h]` ou vínculos previous/next antes do Save para validar que o host cancela a gravação como conflito.


## Teste de cópia de spline

1. selecione uma spline existente;
2. na aba **Geral**, clique **Colocar cópia desconectada**;
3. clique em outro ponto do mapa;
4. confirme que a prévia mantém tipo, comprimento, raio, rotação e gradientes da fonte;
5. ajuste os valores desejados;
6. confirme **Confirmar e salvar**;
7. aguarde o recarregamento;
8. confirme que a nova spline recebeu um ID global novo;
9. confira no tile que `previous` e `next` da nova spline são `-1`;
10. confirme que a spline original e seus vínculos não foram alterados;
11. confira o backup em `.mapstudio-backups/<timestamp>/`.

Para testar conflito, altere externamente o ID, caminho, tipo ou vínculos da spline-fonte depois de iniciar a colocação. A criação deve ser cancelada.


## Teste de exclusão de spline

1. crie uma cópia desconectada de uma spline e valide que **Excluir spline** a remove;
2. escolha uma spline conectada cuja anterior/próxima seja fácil de identificar;
3. clique **Excluir spline** e confirme;
4. após o recarregamento, confirme que a fonte desapareceu;
5. confirme que os vizinhos que apontavam para ela agora possuem a ponta correspondente em `-1`;
6. confira que todos os tiles alterados possuem backup sob o mesmo timestamp;
7. verifique preservação de comentários/seções desconhecidas.

Para testar conflito, altere externamente um vínculo do vizinho antes de confirmar a exclusão. Nenhum tile deve ser parcialmente modificado.


## Teste de busca de splines

1. use a busca do Explorer pelo nome de um arquivo `.sli`;
2. repita usando o ID da spline e a coordenada do tile;
3. clique no resultado e confirme que a spline é selecionada e focada;
4. crie uma prévia de edição e confirme a marca **alterada** na lista;
5. pesquise um termo que corresponda simultaneamente a objetos e splines e confirme as duas seções.


## Teste de vínculos transacionais

1. selecione uma spline desconectada e anote seu ID;
2. selecione outra spline com ponta livre;
3. em **Vínculos da cadeia**, informe o ID apropriado em Anterior ou Próxima;
4. clique **Salvar vínculos**;
5. após o recarregamento, confirme que a spline vizinha recebeu o vínculo recíproco;
6. troque o vizinho por outra spline livre e confirme que a ponta do vizinho antigo voltou para `-1`;
7. teste **Desconectar rascunho** + **Salvar vínculos** e confirme que os dois lados da conexão são liberados;
8. confira backups de todos os tiles alterados no mesmo timestamp.

Teste de conflito: tente ligar a uma ponta já ocupada ou altere externamente um vínculo antes do Save. Nenhum dos tiles deve ficar parcialmente alterado.


## Teste da Biblioteca de Splines

1. abra a guia **Splines** no Explorer;
2. confirme a leitura sob demanda da pasta `OMSI 2/Splines`;
3. busque um `.sli` que não esteja usado no mapa;
4. clique **Normal**, escolha um tile e confirme a prévia do perfil real;
5. ajuste Z, rotação, comprimento, raio e gradientes;
6. confirme **Confirmar e salvar**;
7. em mapa com template `[spline]` neutro explícito (5 extras zero), confirme novo ID global, `previous=-1`, `next=-1` e backup;
8. repita com **Altura** em mapa que possua template `[spline_h]` neutro explícito (6 extras zero);
9. em mapa sem template neutro compatível do tipo escolhido, confirme que a prévia funciona mas a gravação é bloqueada.


## Teste de texturas reais

1. selecione um objeto O3D conhecido por usar BMP/PNG/JPG, DDS ou TGA;
2. confirme que a geometria continua aparecendo imediatamente com as cores-base e recebe a textura quando o asset chega;
3. selecione uma spline cujo `.sli` declare `[texture]` e confirme a textura no perfil extrudado;
4. inicie uma colocação de objeto e uma colocação de spline e confirme o reaproveitamento das texturas em cache;
5. use uma referência de textura ausente e confirme fallback para a cor do material, sem placeholder fake;
6. valide que caminhos que escapam de `Sceneryobjects`/`Splines` não são carregados.


## Teste do estado de material

1. selecione um objeto com múltiplos materiais;
2. abra **Materiais** e confirme que cada textura declarada mostra estado individual;
3. valide uma textura real encontrada e confirme **Carregada · EXT**;
4. teste uma referência ausente e confirme **Arquivo ausente** sem quebrar a geometria;
5. selecione uma spline texturizada e confirme o mesmo estado no painel **Perfil**.


## Teste de prefetch visual

1. abra um mapa completo com vários tipos de objetos texturizados;
2. sem selecionar objetos, confirme que alguns materiais próximos ao tile ativo recebem textura progressivamente;
3. confirme `Texturas auto: X/24` na barra de status e que X nunca passa de 24;
4. altere o tile ativo e confirme que novos recursos próximos podem ocupar o orçamento ainda disponível;
5. selecione um objeto cuja textura não entrou no orçamento e confirme que a seleção ainda carrega a textura normalmente;
6. confirme que perfis de spline próximos são carregados um por vez e que não há pedidos duplicados do mesmo asset.


## Teste de `[matl_alpha]`

1. abra um objeto com `[matl]` + `[matl_alpha] 1` e confirme recorte/cutout sem transparência parcial;
2. abra um objeto com `[matl_alpha] 2` e confirme transparência parcial;
3. confira no inspetor **Materiais** a linha `SCO: alpha ...`;
4. teste `[matl_noZwrite]` e `[matl_noZcheck]` em objeto conhecido;
5. use um `.sco` com `[matl_change]` e confirme que o estado dinâmico não é aplicado como override estático;
6. confirme que material cujo índice/nome de textura não correspondem ao O3D não recebe override.


## Teste de bumpmap

1. selecione um `.sco` com `[matl]` e `[matl_bumpmap]` estáticos;
2. confirme no inspetor o nome e fator do bump;
3. valide que o asset passa de Aguardando/Carregando para Carregada;
4. compare o relevo visual com um material equivalente sem bump;
5. remova temporariamente a imagem do bump e confirme fallback sem placeholder/fake.


## Teste de nightmap

1. selecione um objeto com `[matl_nightmap]` estático;
2. confirme **preview desligado** com Nightmap desmarcado;
3. marque **Nightmap** e confirme carregamento do asset real;
4. confirme emissão sem substituir a textura difusa;
5. desmarque e confirme retorno ao preview diurno;
6. confirme que `[matl_change]` continua sem ser executado.


## Teste de envmap

1. selecione um objeto com `[matl_envmap]` estático;
2. confirme arquivo e força no inspetor;
3. confirme carregamento da textura real;
4. compare a reflexão visual com o mesmo material sem envmap;
5. valide forças 0, 0.4 e 1 e confirme que valores externos são limitados no preview.


## Teste de comandos dinâmicos de material

1. abra um objeto com `[matl_transmap] \S:...`;
2. confirme a referência no inspetor com **runtime não simulado**;
3. abra um material com `[matl_lightmap]` e confirme o mesmo aviso;
4. valide que nenhum desses comandos dispara carregamento/aplicação fake no viewport.


## Teste de diagnóstico de material

1. abra um `.sco` com `[matl_envmap_mask]`, `[alphascale]` ou `[matl_allcolor]`;
2. confirme que o material correspondente mostra `Não simulado:` no inspetor;
3. valide que comandos repetidos não aparecem duplicados;
4. confirme que esses comandos não alteram o material visualmente nesta etapa.


## Teste do cache LRU

1. navegue/seleciona recursos suficientes para ultrapassar 64 texturas únicas;
2. confirme que `Cache: X/64` nunca mostra mais de 64;
3. volte a um objeto cuja textura antiga foi removida e confirme que ela pode ser solicitada novamente;
4. clique **Limpar cache** e confirme `Cache: 0/64` e `Texturas auto: 0/24` antes do repovoamento progressivo;
5. confirme que o mapa continua carregado e somente assets de textura são descartados.


## Teste de LOD de objetos

1. selecione um objeto com `[LOD] 0.6`, `[LOD] 0.2` e `[LOD] 0`;
2. confirme os rótulos LOD na aba **Geometria**;
3. aproxime a câmera e confirme o grupo de maior limiar;
4. afaste a câmera e confirme as trocas para 0.2 e 0;
5. confirme que apenas um grupo LOD fica ativo por vez;
6. confirme que meshes **Global** permanecem visíveis em todos os níveis.


## Teste de superfícies de spline

1. abra um tile com ruas/splines e aguarde o prefetch dos perfis `.sli`;
2. com **Perfis spline** ativo, confirme que eixos próximos passam a receber superfície extrudada;
3. confira curva, largura, elevação e gradiente contra o eixo;
4. confirme texturas reais quando disponíveis;
5. mude o tile ativo e confirme que a janela 3×3 acompanha a nova área;
6. desligue **Perfis spline** e confirme retorno imediato ao modo de eixos.


## Teste de diagnóstico de terreno

1. abra um mapa com tile que possua `[terrain]` e `.map.terrain`;
2. confirme **Presente** + **Encontrado** e tamanho no inspetor;
3. teste tile com `[terrain]` mas sem sidecar e confirme **Ausente**;
4. teste sidecar existente sem marcador e confirme que a inconsistência fica visível;
5. confirme que nenhum arquivo `.terrain` é alterado.

## Teste de tela cheia e navegação

1. abra um mapa e pressione **F11**; confirme que a moldura/barra de título do Windows desaparece;
2. pressione **Esc** e confirme retorno ao estado anterior da janela;
3. repita usando o botão de tela cheia da barra do editor;
4. arraste com o botão direito e confirme órbita sem selecionar objetos acidentalmente;
5. arraste com o botão do meio e confirme deslocamento do alvo da câmera;
6. use Shift + botão do meio e confirme deslocamento acelerado;
7. valide zoom suave pela roda e por `+` / `-`;
8. use as setas e Shift + setas para deslocar pelo mapa;
9. inicie o carregamento progressivo de vários O3D/texturas, mova a câmera durante o processo e confirme que posição/alvo/zoom não retornam ao enquadramento inicial;
10. confirme que clique esquerdo, seleção e gizmos continuam funcionando;
11. valide **F**, **Home**, **1** e **2** após navegar manualmente.
