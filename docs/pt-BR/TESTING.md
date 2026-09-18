# Testes — v0.1.0-alpha.3

[English](../en/TESTING.md) · **Português (Brasil)**

A **v0.1.0-alpha.3** continua deliberadamente **somente leitura**.

## Instalação

1. Baixe `OMSI-Map-Studio-v0.1.0-alpha.3-win-x64.zip`.
2. Extraia o ZIP.
3. Execute `OMSI Map Studio.exe`.
4. Clique em **Abrir OMSI** e selecione a pasta raiz do OMSI 2.
5. Clique em **Abrir mapa** e escolha manualmente uma pasta dentro de `maps`.

O pacote é self-contained para .NET 10. O Microsoft Edge WebView2 Runtime precisa estar disponível no Windows.

## O que mudou nesta alpha

- nova interface baseada no conceito visual aprovado;
- nenhum mapa é listado ou aberto automaticamente;
- streaming de mapa por **tile ativo + área 3×3**;
- cache de tiles já lidos;
- superfície-base neutra para tiles existentes;
- leitura e desenho dos eixos reais de `[spline]` e `[spline_h]`;
- seleção direta de splines no viewport;
- inspetor de spline com IDs, posição, rotação, comprimento, raio e gradientes;
- leitura sob demanda de arquivos `.sli`;
- leitura de `[texture]`, `[profile]` e `[profilepnt]`;
- extrusão do perfil real da spline selecionada;
- materiais O3D embutidos aplicados no preview do objeto selecionado.

## Roteiro principal

- abra um mapa grande e confirme que apenas a região 3×3 é carregada;
- clique em outro tile visível e confirme que ele vira o tile ativo;
- volte para uma área visitada e observe se a troca é rápida por causa do cache;
- clique em um eixo azul de spline;
- confira no inspetor o caminho `.sli`, ID, comprimento, raio e gradientes;
- abra a aba **Perfil**;
- em splines com `[profile]` válido, confira se a faixa/superfície aparece sobre o eixo;
- selecione um objeto e confirme que a seleção de spline é limpa, e vice-versa;
- selecione um objeto O3D não criptografado e valide geometria/materiais.

## Limitações conhecidas

- salvar, criar e editar mapas continuam desabilitados;
- a geometria detalhada de spline é carregada somente para a spline selecionada;
- texturas de imagem da spline ainda não são aplicadas;
- `[patchwork_chain]` e extensões avançadas de material de spline ainda não são renderizadas;
- terreno binário `.terrain` ainda não é interpretado;
- mapas `[worldcoordinates]` continuam esquemáticos;
- O3D criptografado e arquivos `.x` continuam sem preview geométrico;
- texturas O3D ainda não são aplicadas.

## Segurança

Esta alpha não possui operação de escrita de mapas. O botão **Salvar** continua desabilitado.
