# Organização dos testes

[English](README.en.md) · **Português (Brasil)**

## Projetos de teste

- `MapStudio.Core.Tests`: regras de domínio, formatos OMSI, importação/exportação, edição, geração e persistência.
- `MapStudio.Renderer.Tests`: geometria, viewport, picking, renderização e cálculos usados pelo renderer nativo.

## Convenções

1. Mantenha cada teste no projeto que referencia a camada sob teste; evite referências cruzadas entre projetos de teste.
2. Agrupe novos testes em subpastas por domínio quando isso melhorar a navegação (por exemplo, `IO/`, `Omsi/`, `Generation/`); não crie uma pasta por classe.
3. Use nomes de teste que expressem cenário e resultado esperado. Preserve testes de regressão para defeitos já corrigidos.
4. Antes de mover testes existentes, procure referências a caminhos nos workflows, scripts, documentação e arquivos de solução. Confirme que o SDK inclui os arquivos de forma automática e execute os testes no Windows.
5. Não reduza cobertura nem substitua validações reais por mocks apenas para facilitar a reorganização.

## Próxima reorganização

A migração incremental agora organiza nove arquivos em `MapStudio.Core.Tests/Omsi/`. Além dos seis arquivos já migrados, `OmsiTerrainBorderStitcherTests.cs`, `OmsiTerrainEditingTests.cs` e `OmsiTerrainTextureMaskTests.cs` foram movidos para a pasta OMSI e tiveram seus namespaces atualizados; os testes existentes foram preservados. Os workflows `ci.yml`, `native-ci.yml` e `core-editing-tests.yml` filtram `tests/**` ou `tests/MapStudio.Core.Tests/**`, portanto as novas localizações permanecem cobertas por esses gatilhos. A validação da nova movimentação depende da conclusão do CI.

O projeto Core.Tests ainda tem muitos arquivos diretamente na raiz. Continue por domínio, conferindo todos os caminhos explícitos em workflows, scripts e documentação; não mova todo o conjunto de uma vez.
