# Interface do usuário

[English](../en/UI.md) · **Português (Brasil)**

A interface principal da Alpha.3 segue a direção visual do conceito aprovado pelo projeto: navegação lateral permanente, telas dedicadas para configuração da instalação e abertura manual de mapas, e um editor de três áreas.

## Navegação

A barra lateral contém:

- Início;
- Abrir OMSI;
- Abrir mapa;
- Explorador;
- Ferramentas;
- Configurações.

Itens ainda não implementados podem aparecer como estrutura visual, mas não devem apresentar dados ou ações fictícias.

## Fluxo

1. **Abrir OMSI** registra a pasta raiz da instalação.
2. **Abrir mapa** permite escolher manualmente uma pasta dentro de `maps`.
3. Após abrir o mapa, a interface entra no **Editor**.

Nenhum mapa é carregado automaticamente ao selecionar a instalação.

## Editor

O editor é dividido em:

- **Explorador**, à esquerda, com contagens reais de objetos, splines e tiles;
- **Viewport**, ao centro, renderizado com Babylon.js;
- **Inspetor**, à direita, com propriedades do mapa ou do objeto selecionado;
- **Barra de status**, com contagens reais do mapa aberto.

As categorias ainda não suportadas, como terreno e rotas, aparecem claramente como “em desenvolvimento”.

## Inspetor de objeto

Quando um objeto é selecionado, o inspetor expõe abas:

- Geral;
- Transformação;
- Geometria;
- Materiais.

Todas as informações exibidas vêm dos arquivos reais do OMSI. Materiais usam dados O3D reais já interpretados pelo Core.

## Regra visual

A interface pode seguir o conceito visual aprovado, mas nunca deve apresentar miniaturas, mapas, contagens ou estados inventados para parecer completa. Estados sem suporte devem ser vazios, desabilitados ou identificados como em desenvolvimento.


## Superfície-base do viewport

Enquanto o formato binário `.terrain` ainda não é interpretado, os tiles existentes recebem uma superfície-base neutra do editor. Ela serve apenas para leitura espacial e não representa o relevo nem a textura real do terreno.

Tiles ausentes continuam sem preenchimento e destacados separadamente. Os eixos de splines e marcadores de objetos são desenhados acima dessa superfície.
