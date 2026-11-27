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

## Etapa 10 — linguagem visual Clean

Paleta neutra grafite/claro, azul para seleção, contornos discretos e módulos compactos. No desktop, o trilho vertical contém apenas seleção, vegetação e cruzamentos, sem repetir os módulos superiores; em tela cheia, todos os atalhos voltam. Controles e eventos preservados, tema de alto contraste intacto. Validar tema claro/escuro, troca de ferramenta e F11. Capturas pendentes.

## Etapa 11 — layout Clean (estrutura)

Restaurada a paleta anterior à etapa 10: foco na disposição, não na cor. Controles de câmera e grade em Vista, dentro da faixa contextual; removida a faixa exclusiva sobre o viewport. Projeto e Propriedades acessíveis na mesma faixa. Inspector automático recolhe quando vazio e abre quando há seleção editável ou ferramenta de terreno; alternância manual desativa o automático, reativável em Visualizar. Abas do Explorer em uma linha, ações de projeto recolhidas por padrão. Restauração em tela cheia preservada. Correção do bloqueio estático do trilho de módulos mantendo todos os atalhos no XAML e acesso pela faixa superior. CI e homologação visual pendentes.

## Etapa 12 — separação de início e edição

Tela inicial ocupa o workspace sem expor painéis vazios; quatro ações reais de projeto, acesso explícito ao editor e retorno pelo menu Visualizar quando nenhum mapa estiver aberto. Em tela cheia a tela inicial é ocultada. Explorer usa lista compacta sem cards por objeto, preserva tipo/detalhe e caminho em tooltip. Marca superior simplificada. Paleta e motor inalterados. Capturas e CI da etapa 12 pendentes.

## Etapa 13 — menos barras e biblioteca sob demanda

Faixa horizontal de módulos recolhida por padrão, reativável em Visualizar. Trilho compacto concentra as ferramentas, incluindo Geo Studio e validação. Biblioteca mostra primeiro pesquisa, filtros e resultados; ações do asset aparecem após seleção, abaixo da lista no painel lateral ou ao lado no modo acoplado. Captura automática sai da tela inicial e seleciona os controles visíveis por UI Automation. Cores e motor preservados; compilação e capturas da etapa 13 pendentes.

## Etapa 14 — comandos contextuais compactos

Road Studio mantém desenho, reta e curva acessíveis e reúne opções de desenho, construção numérica/rotatória, geometria/conexões/elevação em três grupos. Controles de terreno e tráfego secundários passam para menus. Acesso aos painéis por ícones com nomes de automação e tooltips; instrução de seleção transferida para tooltip. Nenhum nome de controle ou evento existente removido. Validar seleção de SLI, continuidade/snap, abertura dos grupos e retorno ao viewport; validar menus de terreno e tráfego em mapa real. CI e captura pendentes.
