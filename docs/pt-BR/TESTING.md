# Testes — v0.1.0-alpha.3

[English](../en/TESTING.md) · **Português (Brasil)**

A **v0.1.0-alpha.3** agora permite salvar apenas **transformações de objetos posicionados**. As demais operações de edição continuam bloqueadas.

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

- Salvar atua apenas em posição/rotação/pitch/bank de objetos `[object]` já existentes;
- criar, duplicar ou excluir objetos ainda não grava;
- splines ainda não possuem edição persistente;
- terreno binário `.terrain` ainda não é interpretado/editado;
- texturas de imagem de splines e O3D ainda não são aplicadas;
- mapas `[worldcoordinates]` continuam limitados;
- O3D criptografado e arquivos `.x` continuam sem preview geométrico.

## Segurança

Nunca use o único exemplar de um mapa importante durante esta alpha. Apesar do backup automático e da escrita preservativa, a funcionalidade de Save ainda é experimental.
