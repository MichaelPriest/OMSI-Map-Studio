# Remodelação Professional — etapa 2

Referência: mockups aprovados do editor principal, Road Studio e Geo Studio.

- Barra contextual horizontal acima do viewport, com os comandos nativos existentes.
- Biblioteca de assets acoplada abaixo do viewport ao abrir ferramentas de construção. O mesmo painel pode ser desacoplado, minimizado ou devolvido ao explorador.
- Miniaturas e filtros originais, com ações em coluna lateral rolável no modo acoplado.
- Painéis laterais mantidos, sem substituir renderizador ou comandos de edição.

## Homologação pendente

Executar no Windows: abrir mapa, selecionar Road Studio, selecionar uma spline, desenhar e conectar trechos; testar filtro, favoritos, arrastar/soltar, alternar acoplado/flutuante e minimizar/restaurar; verificar mudança de ferramenta, tela cheia, resolução pequena e temas claro/escuro. Verificar que as janelas de geração real, transporte e carregamento continuam sobre o workspace.

O Geo Studio completo, minimapa permanente e visualizações simultâneas ainda não foram implementados nesta etapa.

## Etapa 3 — Road Studio e navegador de assets

- Acesso antecipado a reta, curva, pontos de controle, cruzamentos, snap, continuidade e conexão automática.
- Filtros rápidos por tipo de asset, favoritos e pesquisa vinculada ao filtro existente.
- Explorador de cena simultâneo à biblioteca hospedada, com buscas independentes.
- Selecionar um asset com mapa aberto não substitui automaticamente o viewport por uma prévia; a prévia 3D permanece acessível por comando explícito.
- Campos de comprimento e raio identificados em metros. Validação visual e funcional ainda necessária.

## Etapa 4 — Geo Studio, camadas e minimapa

- Geo Studio expandido no workspace, com seleção geográfica central, controles laterais, camadas de reconstrução e atalhos para fontes e geração. Mantém o assistente existente e opção flutuante.
- Camadas de cena sincronizadas com os comandos de visibilidade existentes.
- Minimapa navegável de tiles reais, com região 9 × 6 ao redor do tile ativo e distinção entre tiles carregados e não carregados.
- O minimapa representa a grade de tiles, não imagem aérea. Visualizações 3D/2D simultâneas e perfil longitudinal ainda não fazem parte deste lote.
- Homologar navegação, sincronização de camadas, geração OSM, janelas e resoluções menores no Windows.

## Etapa 5 — resolução compacta

Faixa de módulos compacta, cabeçalhos com quebra de linha, contexto do Inspector em linha separada, comandos da biblioteca roláveis, limite de largura lateral abaixo de 1200 DIPs e prioridade do Geo Studio acoplado sobre a biblioteca. Resultados da busca ocupam linha própria. Sem mudanças de formatos OMSI. Homologar 1024 × 720, 1366 × 768 e 1920 × 1080, temas claro/escuro e DPI 100/125/150%. Capturas da revisão pendentes.

## Etapa 6 — hierarquia visual

Duas faixas de navegação no desktop, ações frequentes junto ao menu e ações secundárias agrupadas em Mais. Barra contextual plana e rolável, câmera em faixa reservada, trilho vertical menor com rolagem, ações de projeto em duas linhas, minimapa inicialmente recolhido, biblioteca limitada à altura do workspace e Geo Studio com fundo opaco. Todos os nomes de controles e eventos existentes preservados. Validar abertura do menu Mais, alternância de módulos, tela cheia, biblioteca e Geo Studio com as capturas de CI.

## Etapa 7 — Road Studio por grupos

Desenho, reta, curva, snap e continuidade ficam no início; edição de curva/endpoints, conexões/paralelas e elevação passam para grupos expansíveis. Precisão, rotatória, revisão, perfil e ferramentas avançadas permanecem acessíveis. Biblioteca com miniaturas e comandos compactos; limite proporcional somente no modo acoplado. Todos os controles e eventos foram preservados. Homologação visual e de edição pendente.

## Etapa 8 — Inspector funcional

Cabeçalho e seleção fixos, propriedades com rolagem independente, ações Aplicar/Duplicar/Excluir no rodapé quando disponíveis. Geometria principal sempre acessível; ajustes terreno/via e vínculos em seções recolhíveis. Ferramentas de terreno separam pincel, texturas e polígonos. Bordas mais discretas e status com reticências. Preservados nomes e eventos dos controles. Validar seleção, edição e gravação no Windows, inclusive altura compacta.

## Etapa 9 — viewport e projetos

Abaixo de 1250 DIPs, seleção de ferramentas usa biblioteca lateral em vez da bandeja inferior automática. Pesquisa sempre visível, abas do Explorer disponíveis, ações com altura limitada e miniaturas com espaço próprio. A barra de ferramentas vertical não cobre o viewport. Ícones de módulos reaproveitam as geometrias vetoriais nativas. Tela inicial contextual com ações reais de novo mapa, abrir, importar e Geo Studio; desaparece quando há mapa ou outra ferramenta. Validação da etapa 8 aprovada; etapa 9 ainda requer CI e capturas.
