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
- criação livre de splines e edição automática dos vínculos previous/next ainda não existem; a criação atual é por cópia desconectada de uma spline real;
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
