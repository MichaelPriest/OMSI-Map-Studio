# OMSI Map Studio — Manual do Usuário

> Manual inicial da arquitetura nativa WinUI 3 + Direct3D 11.  
> Atualizado para a série **0.2.0-alpha.5-test.10.7-native**.

O OMSI Map Studio ainda está em desenvolvimento Alpha. Antes de editar mapas importantes, mantenha cópias de segurança. Diversas operações do editor já criam backup automaticamente, mas mapas e assets compartilhados podem afetar mais de um projeto.

---

## 1. Visão geral da interface

A janela principal é dividida em quatro áreas:

- **Viewport 3D**: área central onde o mapa é visualizado e editado.
- **Projeto / Explorer**: navegação pelo mapa, tiles, objetos, splines e recursos do projeto.
- **Biblioteca de Assets**: busca e seleção de objetos, splines e outros itens disponíveis.
- **Inspector**: ajustes avançados do item selecionado. Para criação comum de ruas, ele não é obrigatório.

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

**Projeto** e **Biblioteca de Assets** são áreas separadas.

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

# 20. Referência de mapa e georreferência

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
