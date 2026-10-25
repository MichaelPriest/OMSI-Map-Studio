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

A migração incremental organiza nove testes de terreno/leitura em `MapStudio.Core.Tests/Omsi/` e dez testes de horários/viagens em `MapStudio.Core.Tests/Omsi/Timetables/`. Os arquivos de horários foram movidos sem alterar os casos de teste e seus namespaces passaram a `MapStudio.Core.Tests.Omsi.Timetables`. Os workflows `ci.yml`, `native-ci.yml` e `core-editing-tests.yml` filtram `tests/**` ou `tests/MapStudio.Core.Tests/**`; o projeto usa inclusão automática de arquivos C#, sem lista manual no `.csproj`. A execução dos workflows do novo commit ainda precisa confirmar a validação.

Sete testes de splines e caminhos de splines agora estão em `MapStudio.Core.Tests/Omsi/Splines/`, com namespace `MapStudio.Core.Tests.Omsi.Splines`. O caminho explícito de `OmsiSplineTests.cs` em `native-preview-release.yml` foi atualizado. Três testes de inserção/edição segura de objetos foram organizados em `MapStudio.Core.Tests/Omsi/Objects/`, com namespace `MapStudio.Core.Tests.Omsi.Objects`; a auditoria não encontrou referências explícitas a esses caminhos nos cinco workflows verificados. Três testes de patch de cenário foram organizados em `MapStudio.Core.Tests/Omsi/Scenery/`, com namespace `MapStudio.Core.Tests.Omsi.Scenery`. O caminho explícito do teste de semáforos/cenário no `native-preview-release.yml` também foi atualizado. Onze testes de tiles, seleção, segurança, água, tráfego e identidade de splines foram organizados em `MapStudio.Core.Tests/Omsi/Tiles/`, com namespace `MapStudio.Core.Tests.Omsi.Tiles`. Os caminhos explícitos de `OmsiTileGridTests.cs`, `OmsiTileContentCacheTests.cs` e `OmsiTileCreationRoundTripTests.cs` foram atualizados no `native-preview-release.yml`, inclusive a leitura do arquivo no script PowerShell. Oito testes de catálogo, caminhos de assets, metadados DDS e geometria/material O3D foram organizados em `MapStudio.Core.Tests/Omsi/Assets/`, com namespace `MapStudio.Core.Tests.Omsi.Assets`. O workflow `native-preview-release.yml` teve atualizados os caminhos e as leituras PowerShell para os testes de catálogo que ele referencia explicitamente. Continue a migração por domínio e confira referências antes de cada movimentação.
