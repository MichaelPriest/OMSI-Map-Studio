# OMSI Map Studio — Manual do Usuário

> Manual inicial da arquitetura nativa WinUI 3 + Direct3D 11.  
> Atualizado para a série **0.2.0-alpha.5-test.10.10-native**.

O OMSI Map Studio ainda está em desenvolvimento Alpha. Antes de editar mapas importantes, mantenha cópias de segurança. Diversas operações do editor já criam backup automaticamente, mas mapas e assets compartilhados podem afetar mais de um projeto.

---

## 1. Visão geral da interface

A janela principal é dividida em quatro áreas:

- **Viewport 3D**: área central onde o mapa é visualizado e editado.
- **Projeto / Explorer**: navegação pelo mapa, tiles, objetos, splines e recursos do projeto.
- **Biblioteca de Assets**: busca e seleção de objetos, splines e outros itens disponíveis.
- **Inspector**: ajustes avançados e numéricos do item selecionado.

### Regra de uso do editor

**Nenhuma função principal depende do Inspector.** O Inspector é opcional e serve para ajuste fino.

Criação, seleção, movimentação, rotação, duplicação, divisão, curvas, elevação, nivelamento, substituição e demais operações principais devem ser executáveis diretamente no viewport, pelas barras rápidas ou pela roda contextual.

O modo **Creator Focus / F11** amplia o espaço do viewport. Os painéis podem ser minimizados e o painel Projeto pode ser redimensionado.

---

## 2. Abrindo ou criando um projeto

No menu **Arquivo**:

- **Workspace Map Studio**: abre o workspace independente do editor.
- **Novo mapa...**: cria um novo mapa.
- **Importar mapa existente...**: adiciona um mapa existente ao workspace.
- **Adicionar pasta de itens...**: adiciona bibliotecas externas de assets.
- **Exportar mapa como pacote OMSI...**: prepara um pacote compatível com OMSI.
- **Abrir OMSI...**: seleciona/abre uma instalação do OMSI quando necessário.
- **Abrir mapa do catálogo...**: abre um mapa detectado pelo catálogo.
- **Abrir pasta de mapa...**: abre diretamente uma pasta de mapa.

O editor pode trabalhar com conteúdo próprio do Map Studio e também com pastas de assets adicionadas pelo usuário.

---

## 3. Navegação no mapa

### Pan

Use o **botão do meio do mouse** para arrastar a câmera lateralmente.

Durante o pan o cursor muda para uma mão.

### Órbita

Segure e arraste o **botão direito** do mouse.

Um clique curto com o botão direito sobre um item selecionável abre a roda de ações; um arrasto continua sendo interpretado como órbita.

### Zoom

Use a roda do mouse sobre o viewport.

### Focar seleção

Selecione um objeto ou spline e use **Focar** na roda contextual ou no comando correspondente.

### Vista

No menu **Visualizar** estão disponíveis:

- Perspectiva;
- Vista superior;
- visibilidade de terreno;
- pintura do terreno;
- objetos;
- splines;
- grade;
- perfis reais das splines;
- alternância de Explorer e Inspector.

---

## 4. Seleção

Clique sobre um objeto ou spline no viewport.

A seleção nativa usa picking por ID com uma área de clique ampliada para facilitar objetos estreitos, pequenos e splines.

Ao passar o mouse, o hover é atualizado sem precisar selecionar.

Quando existem vários itens muito próximos ou sobrepostos, **clique novamente praticamente no mesmo ponto**. O editor alterna entre os candidatos e informa algo como `1/3 sobrepostos`, `2/3 sobrepostos`.

Na faixa **Seleção fácil** você pode filtrar por:

- Todos;
- Objetos;
- Ruas / splines;
- Terreno / tile.

Se a seleção ainda estiver difícil:

1. aproxime a câmera;
2. use vista superior quando for uma spline;
3. filtre temporariamente para **Objetos** ou **Ruas / splines**;
4. confira se o tipo de item está visível;
5. clique novamente no mesmo ponto para alternar candidatos.

### 4.1 Mover e girar com ghost visual

Ao usar **Mover** ou **Girar**, o item selecionado mostra um **ghost preenchido da própria geometria** acompanhando o mouse em tempo real. O item original permanece no ponto inicial até você soltar o botão.

- **Mover (W)**: arraste o próprio objeto ou spline no viewport.
- **Girar (E)**: arraste o próprio item ou use o anel do gizmo.
- Soltar confirma a transformação.
- Ctrl+Z / Ctrl+Y continuam disponíveis.
- O Inspector não é necessário.

---

## 5. Roda contextual

Um **clique curto com o botão direito** sobre um item abre a roda contextual.

### Para objetos

As ações principais incluem:

- Mover;
- Girar;
- Duplicar;
- Excluir;
- Inspector;
- Focar.

**Girar sem Inspector:** selecione o item, ative **Girar** (atalho **E**) e arraste o próprio item no viewport. O anel de rotação continua disponível para controle visual mais preciso. Solte o mouse para aplicar. Com Snap ativo, a rotação usa os incrementos configurados.

**Mover sem Inspector:** selecione o item, ative **Mover** (atalho **W**) e arraste o próprio objeto ou spline no viewport para mover no plano do mapa. Os eixos do gizmo continuam disponíveis para ajuste preciso e movimento vertical.

### Para splines/ruas

A roda possui até 12 ações:

- **Mover**
- **Curvar**
- **Duplicar**
- **Dividir**
- **Paralela**
- **Elevar**
- **Nivelar**
- **Baixar**
- **Substituir**
- **Espelhar**
- **Fluxo**
- **Excluir**

As ações específicas de rua não aparecem para objetos comuns.

---

## 6. Projeto e Biblioteca de Assets

**Projeto** e **Biblioteca de Assets** são áreas separadas. As áreas mais densas usam painéis recolhíveis; **Filtros de Assets** e **Paths / Visualização** podem permanecer fechados para liberar espaço no editor.

### Projeto

Use para navegar no conteúdo já existente do mapa.

O modo **Mapa / Tiles** mostra os tiles dentro do próprio painel Projeto.

### Biblioteca

Use para escolher assets que serão inseridos ou usados para substituir outros.

A Biblioteca possui busca própria.

A classificação pode receber:

- categoria;
- subcategoria;
- override manual;
- classificação assistida por IA para objects e splines.

A classificação por IA não altera a geometria do asset; ela organiza sua apresentação na Biblioteca.

---

# 7. Criador de ruas / splines

O fluxo principal foi desenhado para evitar digitar coordenadas manualmente.

## 7.1 Fluxo básico

1. Entre em **Ruas**.
2. Escolha uma **SLI** na Biblioteca.
3. Clique em **Construir spline**.
4. Clique e mantenha pressionado no ponto inicial.
5. Arraste o mouse observando a prévia fantasma.
6. Solte no ponto final.

Ao soltar o botão do mouse, o trecho é finalizado e enviado para inserção. O Inspector fica reservado para ajustes avançados.

Se **Construir spline** não puder iniciar, o editor mostra na barra de status o motivo: mapa não aberto, SLI não selecionada, arquivo SLI inválido ou outro pré-requisito ausente.

---

## 7.2 Arrastar

O modo **Arrastar** é o fluxo mais rápido.

- pressione no início;
- arraste vendo a prévia;
- solte no final.

Com modo contínuo ativado, o próximo trecho pode continuar do endpoint anterior.

---

## 7.3 Reta

Cria um trecho reto entre início e fim.

---

## 7.4 Curva

O modo **Curva** permite definir visualmente a curvatura.

Após escolher início e fim, mova o cursor lateralmente para definir a curva.

O editor calcula:

- raio;
- comprimento;
- rotação;
- gradiente.

---

## 7.5 Editar curva de uma spline existente

1. selecione a spline;
2. escolha **Editar curva** ou **Curvar** na roda;
3. mova a alça visual;
4. clique para confirmar.

A spline pode ser curvada ou endireitada.

---

# 8. Elevação estilo city-builder

A barra rápida de Ruas possui três modos principais:

**[ ↓ Baixar ] [ Nivelar ] [ ↑ Elevar ]**

Também existe o comando **Terreno**.

## 8.1 Passo de elevação

Escolha:

- 0,5 m;
- 1 m;
- 2 m;
- 5 m.

O passo define quanto a ferramenta sobe ou desce por acionamento.

---

## 8.2 Elevar

Use para criar:

- rampas;
- viadutos;
- pontes;
- acessos elevados.

Exemplo:

- início: 0 m;
- Elevar: +6 m;
- final: +6 m.

A spline recebe gradiente real.

---

## 8.3 Nivelar

Mantém a cota do ponto inicial.

Exemplo:

- início da ponte: +6 m;
- final: +6 m;
- gradiente: 0%.

Isso permite atravessar terreno irregular mantendo a ponte nivelada.

---

## 8.4 Baixar

Use para:

- descidas;
- rampas subterrâneas;
- entrada de túnel;
- trechos abaixo do terreno.

Exemplo:

- início: 0 m;
- Baixar: -5 m;
- final: -5 m.

A spline realmente fica abaixo da cota inicial.

---

## 8.5 Terreno

Retorna ao modo em que o endpoint segue a altura real do terreno.

---

## 8.6 Preview da rampa

Durante o arrasto, o editor mostra informações como:

`52,4 m · 0 → +5 m · 9,5%`

Podem aparecer:

- comprimento;
- elevação inicial;
- elevação final;
- inclinação;
- raio;
- modo atual.

---

## 8.7 Limite de inclinação

O campo de inclinação máxima recomendada permite configurar um limite.

Quando o trecho ultrapassa o valor, o editor mostra um aviso.

O aviso não bloqueia automaticamente a criação; ele serve como alerta visual para o usuário decidir.

---

# 9. Editando elevação de uma rua existente

Selecione a spline e use a roda contextual.

### Elevar

Move verticalmente a spline pelo passo atual.

### Baixar

Move verticalmente a spline para baixo pelo passo atual.

### Nivelar

Zera os gradientes inicial e final e mantém a spline na cota atual.

### Ajustar ao terreno

No menu **Mais**, use **Ajustar spline selecionada ao terreno**.

O editor lê o terreno no início e no fim e recalcula o gradiente.

---

# 10. Snap, conexão e modo contínuo

## Snap

Quando ativado, endpoints próximos podem ser encaixados automaticamente.

A distância de snap pode ser configurada.

## Conexão automática

Quando o snap encontra um endpoint compatível, o editor pode configurar automaticamente os vínculos `previous/next`.

## Contínuo

Depois de criar um trecho, o próximo começa a partir do endpoint anterior.

É recomendado para desenhar uma sequência longa de ruas.

---

## 10.1 Auto conectar e reparar vínculos

Na barra de **Ruas**, o botão **Auto conectar** trabalha sobre a spline selecionada.

Ele procura endpoints compatíveis dentro da distância de snap e:

- preenche Previous/Next ausentes;
- corrige links que apontam para IDs que não existem mais;
- atualiza o vínculo recíproco da spline vizinha;
- preserva vínculos válidos;
- cria backup antes da gravação.

Isso funciona sem abrir o Inspector.

# 11. Dividir spline

1. selecione a spline;
2. escolha **Dividir**;
3. clique diretamente no ponto onde deseja cortar.

Resultado:

- primeiro segmento mantém o ID original;
- segundo segmento recebe novo ID;
- vínculos previous/next são atualizados;
- a operação usa backup/transação.

---

# 12. Rua paralela

Defina o afastamento em metros e escolha:

- **← Paralela**
- **Paralela →**

Para retas, o trecho é deslocado lateralmente.

Para curvas, o editor recalcula raio e comprimento.

Elevação e gradiente são preservados.

---

# 13. Duplicar

Selecione uma spline e use **Duplicar**.

A cópia entra no fluxo de reposicionamento.

---

# 14. Substituir tipo de rua

1. selecione no mapa a spline que será alterada;
2. escolha outra SLI na Biblioteca;
3. use **Substituir**.

O editor preserva:

- ID;
- posição;
- rotação;
- raio;
- comprimento;
- previous/next;
- geometria principal.

Apenas o tipo/asset da spline é substituído.

---

# 15. Espelhar

Use **Espelhar** para alternar o mirror nativo OMSI da spline.

---

# 16. Fluxo veicular

Use **Fluxo** para alternar o sentido dos paths de veículos da spline quando a operação for suportada.

O comando trabalha sobre os paths do asset e informa quantos paths foram alterados.

---

# 17. [spline_h]

O modo **[spline_h]** continua disponível no menu avançado de Ruas para compatibilidade com o formato OMSI.

Use quando estiver trabalhando especificamente com splines de altura do OMSI.

---

# 18. Objetos

Ao selecionar um asset de objeto na Biblioteca, use o viewport para posicioná-lo.

A câmera é preservada ao inserir/recarregar itens no mesmo mapa.

Objetos selecionados podem ser:

- movidos;
- girados;
- duplicados;
- excluídos;
- focados;
- ajustados pelo Inspector.

---

# 19. Terrain / terreno

O editor possui ferramentas para:

- selecionar ponto de terreno;
- nivelar;
- elevar;
- baixar;
- suavizar;
- pintar ground textures;
- aplicar elevação Google;
- importar grade local de elevação.

As operações de terreno são diferentes da elevação de uma spline: alterar a spline não modifica automaticamente a malha do terreno.

---

# 20. Mapa real por área

Use **Mapa > Criar mapa real por área...** para criar um mapa sem precisar digitar manualmente latitude/longitude.

1. Pesquise uma **cidade, endereço ou local**. A busca usa OpenStreetMap/Nominatim sob ação explícita do usuário e não exige chave.
2. Escolha **OpenStreetMap** ou **Google Maps** para visualizar a área.
3. Mova e dê zoom no mapa.
4. Ajuste **Largura área %** e **Altura área %** para aumentar ou reduzir o retângulo central. O cálculo dos limites geográficos acompanha o retângulo em tempo real.
5. Confira o tamanho físico estimado e a quantidade de tiles OMSI de 300 m.
6. Informe pasta/nome.
7. Em **Vias OpenStreetMap**, escolha:
   - **Criar apenas terreno / referência**;
   - **Importar vias como guias** — mostra o grafo no viewport sem gravar splines;
   - **Gerar ruas automaticamente após preview** — classifica as vias, prepara splines/junctions e exige confirmação depois da prévia antes de gravar.
8. Opcionalmente ative **Google Elevation** e/ou **Referência visual no terreno**.
9. Clique **Criar área**.

A janela pode ser movida, redimensionada e minimizada dentro do editor.

**OpenStreetMap:** não exige chave e é usado para navegação interativa, busca de locais e dados vetoriais. O Map Studio não faz download em massa/offline dos tiles do servidor público. Consultas de busca são limitadas e identificam o aplicativo.

**Google Maps:** usa a API key salva pelo próprio usuário no Windows Credential Manager. Para o seletor interativo, a chave precisa ter Maps JavaScript API habilitada. Google Elevation pode gerar cobrança e por isso fica desativado por padrão.

As vias OSM são projetadas para a georreferência do mapa e classificadas por tipo/largura/faixas quando os dados estiverem disponíveis. Mesmo no modo automático, o Map Studio mostra **Preview antes de gravar** e só persiste as splines/junctions após confirmação.

Para elevação sem Google, use **Mapa → Importar grade local de elevação**. CSV/TXT/ASC são aplicados ao tile ativo pelo mesmo pipeline seguro de terreno, com backup; essa importação local é por tile e não é tratada como DEM global da área inteira.

## Tile X/Y

Tile X/Y não ocupa mais o card Projeto. Use o botão **Tile XY** na barra superior. A janela pode ser movida, redimensionada e minimizada dentro do editor. Ao minimizar, somente a barra de título permanece visível; o mesmo botão restaura a janela sem perder sua posição.

A janela lista os tiles reais do mapa e mostra quais estão **ativos** e **carregados no viewport**, além da quantidade de objetos/splines e do caminho do arquivo. As ações principais ficam disponíveis diretamente nela:

- **Focar** — enquadra um tile que já está carregado sem recarregar a região;
- **Carregar** — torna o tile ativo e, no modo desempenho, carrega a região 3×3;
- **Criar** — cria um tile usando as coordenadas X/Y informadas;
- **Excluir** — ativa o tile escolhido e reutiliza a exclusão segura com validação e backup;
- duplo clique na lista também carrega/foca o tile.

A janela **Criar mapa real por área** segue o mesmo padrão: pode ser movida, redimensionada e minimizada sem fechar o seletor de área.

## Painéis recolhíveis

**Ações do projeto**, **Assets · Filtros** e **Paths · Visualização** podem ser recolhidos para liberar espaço. O Map Studio salva o estado aberto/fechado dessas seções no perfil local e o restaura na próxima execução.

## Janelas internas destacáveis

A barra superior também permite abrir ferramentas como janelas internas independentes:

- **Paths** — filtros dos paths OMSI em tempo real;
- **Transporte** — Route Studio completo;
- **Biblioteca** — o mesmo painel real de assets, favoritos, coleções e classificação por IA;
- **IA painel** — perfil ativo, provedor, modelo, credencial e teste de conexão.

Essas janelas podem ser movidas, redimensionadas, minimizadas e fechadas. **Transporte** e **Biblioteca** não criam uma segunda cópia dos controles: o painel real é movido para a janela flutuante e volta para Projeto ao fechar.

## Multi-seleção

Use **Ctrl+clique** ou **Shift+clique** para adicionar/remover objetos e splines da seleção. Para selecionar vários itens visualmente, comece o arrasto em uma área vazia do viewport e desenhe uma caixa sobre os itens desejados. Com **Ctrl** ou **Shift** pressionado, a caixa adiciona itens ao grupo atual; sem modificador, ela substitui a seleção.

O grupo selecionado pode ser **movido**, **girado**, **duplicado** e **focado** diretamente no viewport. O preview/ghost acompanha o conjunto durante a transformação e o histórico trata a transformação como uma única ação visual. Nenhuma dessas funções depende do Inspector.

Ao usar **Duplicar** com vários itens, o Map Studio cria novos IDs em lote com backup, desloca inicialmente as cópias em 2 m e deixa o novo conjunto selecionado em modo Mover. Splines copiadas são criadas desconectadas (previous/next = -1) por segurança; reposicione e use **Auto conectar** quando desejar reconstruir as ligações.

Pressione **Delete** para excluir o conjunto selecionado com uma única confirmação e backup seguro. Depois da exclusão, **Ctrl+Z** restaura o lote a partir dos backups reais dos arquivos do mapa.

## Movimento natural

No modo **Mover**, arraste o próprio item: o ghost acompanha a posição do cursor no plano do objeto. Para navegar, o botão do meio agora funciona como agarrar/puxar o mapa.

# 21. Referência de mapa e georreferência

No menu **Mapa** estão disponíveis:

- criar mapa real por coordenadas;
- editar georreferência;
- aplicar elevação Google;
- importar grade local;
- carregar referência Google sobre o terreno;
- remover referência Google.

Essas ferramentas auxiliam mapas baseados em locais reais.

---

# 21. Paths de tráfego

O editor suporta visualização e edição de paths OMSI.

Na visão geral podem aparecer:

- **CAR** — veículos;
- **HUM** — pedestres;
- **RAIL** — trilhos;
- **AIR** — aéreo.

É possível trabalhar com paths de SCO e SLI, incluindo criação, duplicação e exclusão segura.

> Atenção: paths pertencem ao asset compartilhado. Alterar um path de uma SLI/SCO pode afetar todas as instâncias que usam o mesmo arquivo.

---

# 22. Transporte, Tracks, Trips e Station Links

As áreas de Transporte/Timetable concentram recursos como:

- Tracks;
- Trips;
- Station Links;
- Lines/Tours;
- Profiles;
- tempos por parada;
- Route Studio;
- janela Timetable destacável.

Ao editar dados de timetable, salve e reabra o conteúdo para validar o round-trip.

---

# 23. Semáforos e tráfego

O projeto inclui ferramentas para regras de tráfego e programas de semáforo.

Como esta área continua evoluindo, valide as mudanças em uma cópia do mapa antes de aplicá-las ao mapa principal.

---

# 24. Undo / Redo

Alterações de transformação suportadas entram no histórico de edição.

Use os comandos em **Editar** ou os atalhos configurados no aplicativo para desfazer/refazer.

Operações que alteram arquivos completos podem usar um histórico de construção/backup separado.

---

# 25. Salvamento e backups

Algumas alterações são mantidas como transformações pendentes até serem salvas.

Antes de operações estruturais, o editor pode salvar alterações pendentes para preservar consistência.

Várias ferramentas de edição de assets e mapa geram backup automaticamente.

Recomendação:

1. mantenha uma cópia externa do mapa;
2. faça alterações em pequenos grupos;
3. salve;
4. reabra o mapa;
5. valide visualmente.

---

# 26. IA no Map Studio

A integração OpenAI nativa usa a Responses API. O Map Studio também mantém a estrutura de perfis para outros adapters suportados pelo projeto.

## Como conectar a IA ao mapa

1. Abra um mapa.
2. Clique em **IA: conectar** na barra superior ou use **IA > Conectar / testar IA no mapa...**.
3. Se ainda não houver perfil ativo, a janela de provedores será aberta.
4. Escolha o provedor/preset, informe o modelo e a API key/token.
5. Marque o perfil como **ativo**.
6. Clique em **Testar conexão**.
7. Salve.
8. Clique novamente em **IA: <nome do perfil>** para testar a conexão ativa no contexto do mapa.

Quando a conexão estiver correta, a barra de status informa que aquele perfil será usado pelas ferramentas de IA do mapa.

Recursos atuais incluem classificação assistida de objects e splines, análise de referências e funções de criação assistida que forem habilitadas na ferramenta correspondente.

A API key é armazenada no **Windows Credential Manager** e não é gravada no mapa nem nos assets.

O usuário continua responsável por escolher o provedor e configurar sua própria credencial.

---

## 26.1 Selecionando o perfil ativo

A barra superior possui um seletor **Perfil de IA**. Escolher um perfil nesse ComboBox o torna ativo imediatamente e salva a escolha.

Depois use **IA: conectar/testar** para validar a conexão.

## 26.2 Referência de mapa

Em **Mapa → Referência de mapa sobre o terreno...**:

- **OpenStreetMap** é a opção padrão e não exige API key;
- **Google Maps** usa a Maps Static API e exige uma chave do usuário;
- a chave Google pode ser salva no Windows Credential Manager;
- a mesma chave salva pode ser reutilizada pelo Google Elevation;
- a referência aparece sobre o terreno com opacidade configurável.

# 27. Testando uma build Alpha

Ao validar uma build:

1. use um mapa de teste ou cópia;
2. abra o mesmo mapa novamente depois de salvar;
3. confirme que objetos e splines continuam no lugar;
4. teste seleção;
5. teste pan e órbita;
6. teste undo/redo;
7. confira os arquivos gerados e backups;
8. registre qualquer crash junto com `startup.log`.

---

# 28. Problemas comuns

## O item não seleciona

- aproxime a câmera;
- tente a vista superior;
- confirme a camada visível;
- clique no centro do item.

## A roda não abre

Faça um clique curto com botão direito. Se arrastar, o editor interpreta como órbita.

## Uma rua ficou muito inclinada

Use o indicador de inclinação e reduza a diferença de altura ou aumente o comprimento do trecho.

## Quero fazer uma ponte

Crie a rampa com **Elevar**, depois use **Nivelar** na cota da ponte.

## Quero fazer um túnel

Use **Baixar** para criar a rampa de entrada e depois **Nivelar** na cota subterrânea.

## Quero voltar ao terreno

Use **Terreno** na barra de Ruas.

---

# 29. Funcionalidades em evolução

Ainda estão sendo expandidas:

- mover endpoints visualmente;
- edição avançada do raio por alças;
- unir trechos;
- criação automática de interseções;
- upgrade visual de rua;
- criação de cruzamentos;
- reconhecimento contextual de solo/rampa/ponte/túnel;
- assets automáticos de pilares, guard rails, portais e paredes de túnel.

O manual será atualizado conforme cada recurso entrar em uma build validada.
