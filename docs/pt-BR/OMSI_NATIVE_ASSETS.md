# Assets reais do OMSI na criação de mapas

[English](../en/OMSI_NATIVE_ASSETS.md) · **Português (Brasil)**

## Decisão de produto

Ao criar um mapa destinado ao OMSI 2, o **conteúdo instalado do próprio OMSI deve ser a primeira opção**. O Map Studio deve facilitar a reutilização de ruas/splines, objetos, pontes, túneis, sinalização, mobiliário urbano, vegetação e demais assets existentes na instalação selecionada.

O Road Kit e os geradores procedurais do Map Studio continuam disponíveis como ferramentas complementares, não como substitutos silenciosos dos assets reais quando existe conteúdo OMSI compatível.

## Comportamento esperado

1. **Selecionar a instalação real do OMSI 2** e indexar as pastas de assets disponíveis, sem presumir que todas as instalações tenham os mesmos pacotes.
2. **Biblioteca baseada nos arquivos reais**:
   - `Splines/**/*.sli` para ruas, pistas, calçadas, trilhos e demais perfis instalados;
   - `Sceneryobjects/**/*.sco` para objetos, edifícios, vegetação, sinalização, estruturas, pontes e peças de túnel;
   - dependências referenciadas pelos assets, incluindo O3D, texturas e arquivos auxiliares suportados.
3. **Busca e filtros úteis** por tipo, nome, pasta/pacote e disponibilidade de dependências. A categoria é apenas uma classificação da biblioteca; não deve alterar o arquivo original.
4. **Pré-visualização real** da geometria e das texturas do asset selecionado, identificando claramente arquivos ausentes ou formatos ainda não suportados.
5. **Colocação no mapa real** usando os formatos e templates OMSI apropriados, com prévia, validação, backup e gravação transacional. Não inventar registros ou campos desconhecidos para forçar a inserção.
6. **Pontes e túneis**: permitir localizar e combinar assets OMSI instalados — por exemplo, splines de pista, peças estruturais, portais, paredes, teto e objetos auxiliares. A geometria procedural é uma alternativa explícita quando o usuário preferir ou quando não houver asset compatível.
7. **Dependências e portabilidade**: antes de exportar/compartilhar, gerar relatório dos assets referenciados e ausentes. Não copiar automaticamente conteúdo de terceiros para o pacote nem alterar os arquivos originais da instalação.
8. **Sem substituição falsa**: se nenhum asset real compatível for encontrado, informar isso e oferecer o Road Kit procedural como alternativa identificada. Não mostrar objetos de demonstração ou mocks como se fossem assets instalados.

## Segurança e compatibilidade

- Nunca gravar alterações diretamente nas bibliotecas globais do OMSI; editar os arquivos do mapa e manter os assets-fonte intactos.
- Preservar comandos desconhecidos e referências OMSI existentes.
- Respeitar pontes, túneis e estruturas com níveis separados ao conectar vias e ajustar terreno.
- Mostrar a origem do asset e o caminho relativo, para que o usuário saiba qual pacote está utilizando.
- Usar testes com fixtures reais/minimamente representativos e testes de regressão; mocks podem existir somente em testes isolados, nunca como conteúdo apresentado como real na aplicação.

## Critérios de aceite

- Uma instalação OMSI selecionada fornece assets reais à Biblioteca sem exigir importação manual arquivo por arquivo.
- O usuário consegue encontrar e pré-visualizar uma spline `.sli` e um objeto `.sco` instalados e inserir cópias no mapa com persistência segura.
- Pontes e túneis instalados são pesquisáveis e utilizáveis por meio de seus assets reais; a ferramenta não afirma que uma estrutura procedural é um asset oficial do OMSI.
- Dependências ausentes/incompatíveis são reportadas antes da gravação ou exportação.
- O conteúdo original da instalação não é modificado.
- A validação inclui testes Core e build/testes nativos Windows; nenhuma funcionalidade é declarada pronta somente por compilar.

## Sequência de implementação

1. Verificar e consolidar o índice da instalação e as dependências dos assets.
2. Garantir que a Biblioteca priorize assets reais da instalação ativa e mostre sua origem.
3. Integrar a inserção real de SCO/SLI ao fluxo de criação com prévia e backup.
4. Adicionar filtros e fluxos orientados a pontes/túneis com combinações de assets reais.
5. Validar em instalação Windows com conteúdo OMSI real e documentar limitações de formato.
