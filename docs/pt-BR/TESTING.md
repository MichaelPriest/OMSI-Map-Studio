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

## Teste da malha de terreno

1. abra Grundorf e aguarde a leitura dos tiles;
2. confirme que **Terreno** está habilitado e que o fundo plano é substituído pela malha de altura onde houver `.map.terrain` válido;
3. no inspetor, confirme **60×60 células**, **3.721 alturas** e um intervalo de altitude coerente para o tile ativo;
4. altere o tile ativo no modo 3×3 e confirme que as novas malhas acompanham a região carregada;
5. desligue **Terreno** e confirme que a geometria de altura desaparece sem afetar objetos/splines;
6. reative a camada e confirme que a câmera enquadra a altitude do terreno, sem voltar para Y=0 em mapas elevados;
7. valide um sidecar inválido/truncado e confirme que o mapa continua abrindo, o diagnóstico mantém o arquivo visível e nenhuma malha falsa é criada;
8. confirme que nenhum arquivo `.terrain` é modificado.

## Teste da textura base do terreno

1. abra um mapa cujo `global.cfg` tenha uma ou mais entradas `[groundtex]`;
2. confirme no inspetor a quantidade de camadas declaradas;
3. valide que a primeira textura principal real é carregada e substitui o material neutro da malha;
4. confira que a repetição visual acompanha o valor `Repeating` da camada 0;
5. confirme que a textura de detalhe mostra estado de carregamento no inspetor, mas não é misturada visualmente nesta etapa;
6. renomeie temporariamente uma textura base de um mapa de teste e confirme fallback para material neutro sem placeholder fake;
7. teste um caminho com tentativa de saída da instalação e confirme que o host o rejeita;
8. confirme que alternar **Terreno** continua ocultando/exibindo a malha sem alterar arquivos do mapa.

## Teste das máscaras de pintura do terreno

1. abra um mapa que possua arquivos `texture/map/tile_*.map.N.dds`;
2. selecione um tile com máscara e confirme no inspetor a lista **Máscaras de terreno** com os índices encontrados;
3. confirme que a camada 0 permanece como base e que cada índice N pinta somente a região indicada por sua máscara DDS;
4. compare o índice N com a entrada `[groundtex]` correspondente e confirme a textura principal correta;
5. valide que o valor de repetição de cada camada é respeitado;
6. troque de tile no modo 3×3 e confirme que apenas as máscaras da nova área são carregadas/renderizadas;
7. confirme que áreas transparentes da máscara deixam a camada inferior visível;
8. valide um índice sem `[groundtex]` correspondente e confirme que nenhuma textura inventada é exibida;
9. confirme no inspetor os dados do `.terrain_0.rdy` (validade, vértices, triângulos e transform) sem alterar nenhum arquivo;
10. confirme que objetos, splines, relevo e navegação continuam funcionais sobre as camadas pintadas.

## Teste dos controles e validação de camadas

1. abra um mapa com várias entradas `[groundtex]` e selecione um tile com máscaras numeradas;
2. no inspetor, desligue uma camada por vez e confirme que somente a pintura correspondente desaparece;
3. desligue a camada 0 e confirme retorno ao material neutro do relevo sem ocultar a malha;
4. desligue **Pintura terreno** e confirme que todas as camadas numeradas desaparecem, mantendo o relevo/base;
5. reative **Pintura terreno** e confirme restauração das camadas habilitadas;
6. confirme que máscaras A8 mostram dimensão/formato e estado **validada**;
7. use um DDS de formato diferente em mapa de teste e confirme estado **não renderizada** sem aplicação visual;
8. navegue por tiles suficientes para exceder o cache de máscaras e confirme que o editor continua estável, recarregando assets removidos quando necessário;
9. clique **Limpar cache** e confirme repovoamento progressivo sem descarregar o mapa;
10. confirme que nenhum `.dds`, `.terrain`, `.rdy` ou `global.cfg` é modificado.

## Teste de validação/cobertura das máscaras DDS

1. abra um mapa com `tile.map.N.dds` e selecione um tile que possua pintura;
2. confirme no inspetor resolução, cobertura e faixa alpha de cada máscara válida;
3. confirme que uma máscara A8 vazia aparece como vazia e não gera camada visual;
4. confirme que máscara 100% opaca pinta toda a camada sem depender de textura de opacidade adicional;
5. teste um DDS truncado ou de formato não suportado em um mapa de teste e confirme que é marcado como inválido sem impedir a abertura do mapa;
6. desligue a camada base 0 no controle de visibilidade e confirme que a textura base some, mantendo o relevo;
7. reative a camada 0 e confirme restauração imediata;
8. alterne entre tiles no modo 3×3 e confirme que máscaras vazias não entram no cache e somente as necessárias são carregadas.

## Teste da resolução declarada em [groundtex]

1. abra um mapa com camadas de resolução conhecida;
2. confirme que código 8 espera máscara 256×256 e código 9 espera 512×512;
3. selecione um tile cuja máscara corresponde à dimensão esperada e confirme renderização normal;
4. em um mapa de teste, substitua temporariamente uma máscara por outra A8 válida com dimensão diferente;
5. confirme que o inspetor mostra a dimensão real e a dimensão esperada;
6. confirme que a camada incompatível fica desabilitada e não é renderizada;
7. restaure a máscara correta e confirme que a camada volta a aparecer sem alterar o `global.cfg`.


## Teste do bloqueio durante carregamento

1. abra uma instalação do OMSI e confirme que a sobreposição animada aparece durante a leitura;
2. abra um mapa em modo completo e confirme barra/percentual conforme os tiles avançam;
3. enquanto a sobreposição estiver visível, tente clicar no Explorer, Inspetor, viewport, menus e atalhos de edição e confirme que nenhuma alteração é aceita;
4. confirme que a preparação progressiva de O3D também mantém a edição bloqueada até a estrutura visual necessária ficar pronta;
5. alterne para modo desempenho 3×3, mude o tile ativo e confirme bloqueio temporário enquanto a nova região é carregada;
6. abra Biblioteca/Splines vazias pela primeira vez e confirme a animação até o host devolver a lista;
7. provoque um erro de leitura em um mapa de teste e confirme que o bloqueio desaparece junto com a mensagem de erro;
8. confirme que F11 e a saída de tela cheia continuam utilizáveis sem permitir edição durante o bloqueio.


## Teste de tela cheia, terreno e splines

1. entre em F11 e confirme que sidebar, Explorador e Inspetor fixos desaparecem;
2. confirme que o dock flutuante permite selecionar/mover/rotacionar, criar objeto, criar spline, abrir Explorer/Inspetor, salvar e controlar camadas;
3. confirme que Explorer/Inspetor abrem como drawers sobre o viewport e fecham sem sair da tela cheia;
4. em Grundorf, aguarde o carregamento bloqueante terminar e confirme que a textura base de terreno deixa de aparecer preta quando o arquivo de origem é BMP;
5. no Inspetor, confirme que uma textura BMP convertida aparece como PNG com indicação de origem BMP;
6. em modo mapa completo, aguarde a fila de perfis SLI e confirme que as superfícies de spline aparecem pelo mapa inteiro, não apenas no entorno 3×3;
7. alterne para modo desempenho e confirme que a limitação espacial de perfis continua ativa;
8. confirme que nenhum arquivo BMP/SLI do OMSI é modificado pela visualização.


## Validação de upload GPU de textura

1. abra Grundorf e aguarde o bloqueio de carregamento terminar;
2. confirme no Inspetor que `gras.bmp` aparece como `PNG (origem BMP)`;
3. confirme visualmente que a textura base do terreno aparece sobre os tiles e não apenas o fundo escuro do viewport;
4. ligue/desligue Terreno e confirme que a superfície texturizada desaparece e retorna;
5. ligue Perfis spline e confirme que as superfícies reais das splines usam suas texturas carregadas;
6. se uma textura continuar ausente, abra o console WebView2 e procure por `OMSI Map Studio: texture upload failed`, registrando extensão/MIME e erro;
7. confirme que nenhum arquivo do OMSI foi modificado durante o teste.


## Validação de carregamento contínuo, RGBA e ícone

1. abra Grundorf em mapa completo e confirme que existe apenas uma tela bloqueante para o carregamento estrutural, sem um novo modal para cada O3D/SLI;
2. confirme que O3D, SLI e texturas continuam avançando na barra de status em segundo plano;
3. confirme que `gras.bmp` continua indicado como origem BMP e que o terreno aparece usando o upload RGBA;
4. confirme que superfícies de ruas/splines BMP aparecem sem depender do decoder PNG do WebView;
5. confira o contador `O3D reais`: respostas com geometria inválida devem aparecer como falhas, não como modelo renderizável;
6. instale pelo EXE, abra pelo Menu Iniciar e confirme o ícone do OMSI Map Studio na janela, barra de tarefas e atalho;
7. confirme que nenhum arquivo original do OMSI foi modificado.


## Regressão O3D long-header + bones

O teste `GeometryReader_LongHeaderBoneSection_UsesShortBoneCount` cria um O3D mínimo com cabeçalho estendido, índices longos e uma seção de bone cuja contagem é UInt16. A leitura deve terminar com geometria renderizável e sem `invalidBoneSection`.

Na validação manual do Grundorf, o Inspetor do mapa deve mostrar separadamente O3D renderizáveis, falhas, pendentes e os principais códigos de erro por malha. Perfis SLI também devem mostrar quantidade de arquivos lidos e total de superfícies efetivamente disponíveis.


## Validação da animação única, terreno e ícone

1. abra Grundorf e confirme que, após a leitura dos tiles, permanece uma única animação “Preparando recursos do mapa” até O3D/SLI/texturas estabilizarem;
2. confirme que a animação não fecha e reabre para cada item;
3. durante a animação, a interface de edição deve permanecer bloqueada;
4. no Inspetor, confirme `Upload textura base: RGBA direto` e observe o campo `RGB médio`;
5. confirme visualmente que `gras.bmp` aparece no terreno, agora pelo canal emissivo não iluminado;
6. confirme que superfícies de spline texturizadas também deixam de ficar escuras apenas por iluminação desativada;
7. instale a nova build pelo EXE e abra pelo Menu Iniciar e também diretamente pelo executável; o ícone do OMSI Map Studio deve aparecer na janela e na barra de tarefas.


## Validação de árvores [tree]

1. abra Grundorf em modo 3×3 e em mapa completo;
2. confirme que `tree_medium_*.sco`, `Tree_Small_*.sco` e outros objetos com `[tree]` deixam de aparecer apenas como marcadores amarelos quando a textura existe;
3. compare duas árvores da mesma espécie com alturas/proporções diferentes e confirme que o viewport preserva os valores gravados no `[object]`;
4. selecione uma árvore e confirme no Inspetor a textura e a faixa declarada pelo `[tree]`;
5. confirme que o helper `treehelper.x` não é contabilizado como falha O3D da árvore;
6. confirme que os marcadores amarelos continuam visíveis apenas onde o asset real não pôde ser renderizado.


## Validação de O3D, SLI e pan lateral

1. abra Grundorf em modo 3×3 e confirme que os objetos O3D deixam de desaparecer por alfa difuso ou por ausência de correspondência de LOD;
2. compare objetos que possuem múltiplos blocos `[LOD]` e confirme que pelo menos um LOD real permanece visível em qualquer distância do editor;
3. abra uma spline com mais de dois `[profilepnt]` e confirme que todas as faixas da seção transversal aparecem, incluindo rua/calçada quando declaradas;
4. clique no viewport e teste `A/D` para esquerda/direita e `W/S` para frente/trás;
5. repita com `Shift` para movimento acelerado;
6. confirme que botão do meio e `Shift + botão direito` continuam fazendo pan sem selecionar objetos.


## Validação de desempenho de carregamento

1. abra Grundorf em modo mapa completo e cronometre da seleção do mapa até o fim de “Preparando recursos do mapa”;
2. repita a abertura sem trocar a raiz OMSI e confirme reaproveitamento dos caches do host durante a sessão;
3. no modo desempenho 3×3, confirme que o contador O3D pode ultrapassar 64 quando a região realmente usa mais de 64 paths únicos;
4. confirme que todos os perfis SLI usados na região entram na fila, sem corte em 48;
5. confirme que BMPs mostram upload RGBA direto e que não existe segunda carga PNG do mesmo arquivo;
6. compare objetos O3D que possuem seção de transformação 0x79 e confirme posição/orientação interna correta após aplicação da transformação inversa;
7. confirme que a interface não congela durante parse pesado de O3D, pois a leitura ocorre fora da thread de UI.


## Validação de carregamento estrutural prioritário

1. abra Grundorf em **Mapa completo** e cronometre separadamente o fim da leitura dos tiles e o momento em que todas as texturas terminam;
2. confirme que, depois que os tiles estão consistentes, o viewport fica utilizável enquanto O3D/SLI/texturas restantes continuam progredindo na barra de status;
3. confirme que o prefetch amplo de texturas não começa antes de os caminhos O3D e SLI estruturais terem recebido resposta;
4. reabra o mapa sem trocar a raiz do OMSI e confirme reutilização dos caches de `.sco` e de meshes físicos `.o3d`;
5. use dois `.sco` que referenciem o mesmo arquivo `.o3d` e confirme que a geometria continua idêntica, sem leitura/parsing duplicado perceptível;
6. altere para o modo 3×3 e confirme que a troca de região ainda bloqueia edição somente durante a leitura consistente dos novos tiles;
7. confirme que erros reais como `encrypted` e `unsupportedFormat` continuam aparecendo no diagnóstico e não são convertidos em geometria fictícia.


## Validação de diagnóstico O3D/DirectX

1. abra um objeto com O3D de cabeçalho longo e seção de bones `0x54`; confirme que o resumo estrutural é válido e mostra a contagem correta;
2. confirme que O3D com chave diferente de `0xFFFFFFFF` continua aparecendo como `encrypted`, sem tentativa de geometria falsa;
3. abra um `.sco` cujo `[mesh]` aponte para `.x` e confirme `legacyDirectXMesh` no diagnóstico;
4. use uma extensão de mesh desconhecida e confirme que ela continua como `unsupportedFormat`;
5. confirme que objetos `[tree]` com `treehelper.x` continuam fora da contagem de falhas, pois são renderizados pela definição real de árvore.


## Validação de mesh DirectX `.x`

1. abra um `.sco` com `[mesh]` apontando para um `.x` iniciado por `xof 0303txt 0032`;
2. confirme que o objeto entra em **Malhas renderizáveis** e deixa de aparecer como `legacyDirectXMesh`;
3. valide um quad/polígono e confirme triangulação sem buracos;
4. valide UV + `MeshMaterialList` + `TextureFilename` e confirme a textura real no viewport;
5. valide um arquivo com `FrameTransformMatrix` e confirme deslocamento/orientação local coerente;
6. valide múltiplos `Mesh` no mesmo arquivo;
7. use variantes `bin`, `tzip` e `bzip` e confirme `legacyDirectXUnsupportedEncoding`, sem travamento e sem fallback fictício;
8. confirme que o cache por arquivo físico também é reutilizado para `.x`.


## Validação de transformações locais SCO

1. abra um `.sco` com dois `[mesh]` apontando para geometria real e use `[new_pos]` diferente em cada um; confirme a posição relativa;
2. valide os aliases `[rot_x]/[rotx]`, `[rot_y]/[roty]` e `[rot_z]/[rotz]`;
3. teste `[scale]` com um valor e confirme escala uniforme;
4. teste `[scale]` com três valores e confirme escala independente X/Y/Z;
5. confirme que um mesh sem blocos de transformação mantém identidade;
6. reutilize o mesmo arquivo físico `.o3d` ou `.x` em dois `.sco` com transforms diferentes e confirme que a geometria continua compartilhada pelo cache sem misturar as transformações.


## Validação de máscaras DDS sob demanda

1. abra Grundorf em **Mapa completo** e confirme que a leitura dos tiles termina sem varrer todos os pixels das máscaras DDS;
2. confirme que cada máscara válida já chega com largura/altura e `hasPixelStatistics=false` após a abertura;
3. habilite pintura/camada que use a máscara e confirme que o asset é solicitado normalmente;
4. depois do carregamento do asset, confirme que cobertura, alpha mínimo e alpha máximo aparecem no diagnóstico;
5. valide máscara vazia e máscara totalmente opaca; ambas devem manter a classificação correta depois que o asset for carregado;
6. confirme que DDS inválido, truncado ou em formato não-A8 continua recusado sem fallback fictício.


## Validação de paralelismo e responsividade

1. abra Grundorf em **Mapa completo** e confirme que o WPF/WebView continua responsivo durante a leitura;
2. confirme no progresso que vários tiles avançam sem esperar serialmente um pelo outro;
3. repita em uma máquina/runtime com poucos processadores lógicos e confirme que a abertura continua usando até 8 leituras de tile;
4. abra a mesma instalação/mapa novamente e confirme reaproveitamento do cache de tiles;
5. confirme que perfis SLI e metadata SCO continuam retornando os mesmos dados reais após a mudança para workers;
6. valide terrain, RDY e máscaras em um tile com todos os sidecars presentes e confirme resultado idêntico ao anterior.


## Validação de O3D com bones e animação de carregamento

1. abra Grundorf e confirme que a animação continua visível enquanto malhas O3D/.x, perfis SLI e texturas ainda estão aquecendo;
2. confirme que, depois da leitura estrutural, a animação de aquecimento não bloqueia mouse/teclado do editor;
3. em modo desempenho 3×3, confirme que o Inspetor mostra `renderizáveis / total`, falhas e pendentes da área ativa;
4. valide um O3D de cabeçalho estendido com seção `0x54` e contagem de bones em `UInt32`;
5. valide um O3D legado com contagem de bones em `UInt16`;
6. confirme que um modelo com bones não vira marcador amarelo apenas por desalinhamento da seção `0x54`.


## Validação de O3D protegido

1. em Grundorf, confirme que o contador do Explorador mostra **Malhas reais** e não conta payload protegido como renderizado;
2. confirme que `encrypted` aparece como **O3D protegido** no Inspetor;
3. confirme que o status final informa a quantidade de tipos de objeto e malhas protegidas;
4. confirme que um O3D protegido não recebe geometria fictícia nem substituição automática por outro formato;
5. confirme que marcadores de O3D protegido têm cor diferente dos marcadores de arquivo ausente/inválido.


## Validação de O3D protegido no Core

1. valide um O3D v7 com cabeçalho estendido, chave de proteção e seed alternativo;
2. confirme que a leitura produz posições, normais e UVs decodificados sem alterar o arquivo fonte;
3. confirme que triângulos, materiais e transform continuam sendo processados pelo pipeline O3D normal;
4. em Grundorf, compare o total de **Malhas reais** com a test.21: os objetos anteriormente marcados como `encrypted` devem migrar para renderizáveis quando estiverem dentro do domínio suportado;
5. se uma malha protegida exceder o domínio validado, confirme o erro explícito `protectedVertexCountUnsupported`.
