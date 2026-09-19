# Alpha.3 Test.1 external build / Build externo Alpha.3 Test.1

## pt-BR

Esta pasta é **somente auxiliar de build**. Ela não substitui a branch de desenvolvimento e não deve ser mesclada na PR #2.

- Release alvo: `v0.1.0-alpha.3-test.1`
- HEAD fonte imutável: `b4f18fcf3e132a5780e5be6257539a3bcdc784e1`
- Branch de desenvolvimento: `feature/alpha3-material-preview`
- Branch auxiliar: `build/alpha3-test1`
- PR #2 deve permanecer **draft** e **sem merge**.

Os arquivos de código necessários para testes, UI e publish foram serializados em `chunk1.json` até `chunk7.json`. O `manifest.json` registra o tamanho e o Git blob SHA-1 de cada arquivo. Antes do build, `reconstruct.mjs` recria a árvore de fontes e valida cada blob contra o HEAD acima.

O pipeline externo executa, nesta ordem:

1. reconstrução + validação dos chunks;
2. instalação oficial do .NET SDK `10.0.401`;
3. `dotnet test tests/MapStudio.Core.Tests/MapStudio.Core.Tests.csproj --configuration Release`;
4. `npm install` e `npm run build` em `src/MapStudio.UI`;
5. `dotnet publish` do Desktop para `win-x64`, self-contained, sem ReadyToRun e sem trimming;
6. validação de `OMSI Map Studio.exe` e `ui/index.html`;
7. geração do ZIP `OMSI-Map-Studio-v0.1.0-alpha.3-test.1-win-x64.zip`, SHA-256 e `build-info.json`.

Nenhuma alpha deve ser publicada se qualquer etapa falhar.

## en

This directory is **build-only infrastructure**. It does not replace the development branch and must not be merged into PR #2.

- Target release: `v0.1.0-alpha.3-test.1`
- Immutable source HEAD: `b4f18fcf3e132a5780e5be6257539a3bcdc784e1`
- Development branch: `feature/alpha3-material-preview`
- Auxiliary branch: `build/alpha3-test1`
- PR #2 must remain **draft** and **unmerged**.

The source files required by tests, UI build and publish are serialized in `chunk1.json` through `chunk7.json`. `manifest.json` records the size and Git blob SHA-1 of every file. Before building, `reconstruct.mjs` recreates the source tree and validates every blob against the immutable HEAD above.

The external pipeline runs, in order:

1. chunk reconstruction + validation;
2. official .NET SDK `10.0.401` installation;
3. `dotnet test tests/MapStudio.Core.Tests/MapStudio.Core.Tests.csproj --configuration Release`;
4. `npm install` and `npm run build` in `src/MapStudio.UI`;
5. Desktop `dotnet publish` for self-contained `win-x64`, with ReadyToRun and trimming disabled;
6. validation of `OMSI Map Studio.exe` and `ui/index.html`;
7. creation of `OMSI-Map-Studio-v0.1.0-alpha.3-test.1-win-x64.zip`, SHA-256 and `build-info.json`.

No alpha must be published if any step fails.
