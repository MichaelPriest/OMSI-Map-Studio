# Build nativa 1720 — 27/11/2026

[English](../../en/releases/native-ci-1720.md)

- [Execução validada](https://github.com/MichaelPriest/OMSI-Map-Studio/actions/runs/46060172393), commit `db032ec9af806a1a240eaf57e724d2aeac55d144`.
- [PR #21](https://github.com/MichaelPriest/OMSI-Map-Studio/pull/21) integrado à main em `9f2c59ab0a0168256b59c63a312612c07c597729`.
- Artefato: `OMSI-Map-Studio-Windows-x64-test`, Windows x64 autocontido, 99.345.444 bytes. SHA-256 do ZIP de artefato informado pelo GitHub: `cb4c654d0f2d5541c2bef730d10ed9c8ca3957bae6fb2e015e735e9aeed2fb3f`. Expiração prevista: 11/12/2026 UTC.
- CI geral, testes de edição, Core, renderer, compilação WinUI e publicação aprovados.

## Mudanças

Relevo Terrarium sem API Key, datum comum entre tiles e vias, hidrografia OSM na referência com ilhas, progresso e cancelamento, correção da limpeza de conexões SQLite nos testes Windows.

## Homologação

Extraia o ZIP inteiro, confira BUILD-INFO.txt e SHA256SUMS.txt, abra um workspace de teste. Importe uma área pequena, aplique Terrarium, reconstrua vias e confira cotas e bordas dos tiles após reabertura. Ative a referência OSM e verifique rios, lagos e ilhas. Teste o cancelamento antes da escrita. Abra o resultado no OMSI 2. A água física não é gerada nesta etapa.

A build é um artefato de CI, não uma nova prerelease numerada. Capturas reais e homologação manual permanecem pendentes.
