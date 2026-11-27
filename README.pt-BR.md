# OMSI Map Studio

[English](README.en.md) · **Português (Brasil)**

Editor independente de mapas para **OMSI 2**, com viewport 3D nativo, ferramentas de construção e geração de mapas reais. O projeto é independente do OMSI NavBR Multiplayer.

## Testar o editor nativo

- [Build Windows x64 — CI 1720](https://github.com/MichaelPriest/OMSI-Map-Studio/actions/runs/46060172393) — 27/11/2026, commit `db032ec9`; baixar o artefato `OMSI-Map-Studio-Windows-x64-test` (GitHub pode exigir login; retenção de 14 dias).
- [CI da main após integração](https://github.com/MichaelPriest/OMSI-Map-Studio/actions/runs/46060526562).
- [Última prerelease numerada nesta atualização: v0.2.0-alpha.5-test.10.143-native](https://github.com/MichaelPriest/OMSI-Map-Studio/releases/tag/v0.2.0-alpha.5-test.10.143-native).

Extraia todo o ZIP da build de teste para uma pasta separada e execute `MapStudio.Native.exe`. Trabalhe em um workspace de teste, preservando os mapas originais. A aprovação do CI não substitui a homologação no OMSI 2.

## Funcionalidades

| Área | Recursos |
| --- | --- |
| Editor 3D | Navegação, seleção, objetos O3D, splines SLI, terreno e edição por tiles |
| Road Studio | Desenho direto, continuação, modo de precisão, rotatórias e perfil longitudinal (consulta) |
| Geo Studio | Referência OSM sem chave, reconstrução por camadas, elevação Terrarium e Open-Meteo |
| Construção | Biblioteca de assets, ferramentas de objetos, edificações, terreno e infraestrutura |
| Segurança | Backups, validação e preservação de conteúdo OMSI desconhecido |

A atualização de 27/11/2026 integra relevo Terrarium compartilhado entre ruas e terreno, referência hidrográfica OSM com ilhas e cancelamento de download. A água é uma referência visual; superfícies físicas de água, integração completa de rotatórias e homologação visual continuam pendentes.

## Documentação

- [Manual do usuário](docs/pt-BR/USER_MANUAL.md)
- [Roadmap](docs/pt-BR/ROADMAP.md)
- [Arquitetura nativa](docs/pt-BR/NATIVE_MIGRATION.md)
- [Paridade com OpenMapper](docs/pt-BR/OPENMAPPER_NATIVE_PARITY.md)
- [Notas da build 1720](docs/pt-BR/releases/native-ci-1720.md)
- [Galeria e roteiro de capturas reais](docs/pt-BR/EDITOR_GALLERY.md)

## Desenvolvimento

Stack nativa: .NET 10, WinUI 3, Direct3D 11/Vortice e SQLite. O Core é a autoridade dos formatos OMSI. WPF/WebView2 e React/Babylon.js pertencem à trilha legada.

```powershell
dotnet test tests/MapStudio.Core.Tests/MapStudio.Core.Tests.csproj
dotnet test tests/MapStudio.Renderer.Tests/MapStudio.Renderer.Tests.csproj
dotnet build src/MapStudio.Native/MapStudio.Native.csproj -c Release -p:Platform=x64
```

A compilação do host nativo exige Windows. Documentação oficial em pt-BR e inglês. Não substituir dados ausentes por dados fictícios nem descartar comandos desconhecidos na gravação.

[Organização das pastas do repositório](docs/pt-BR/REPOSITORY_STRUCTURE.md)

## Pacote portátil organizado

Os novos ZIPs de CI e release agrupam os arquivos em `app/` (runtime intacto), `Documentacao/` (manuais e verificação), e deixam apenas `Iniciar OMSI Map Studio.cmd` e `LEIA-ME.txt` na raiz. Extraia tudo antes de iniciar. Os ZIPs anteriores ainda usam o layout antigo.
