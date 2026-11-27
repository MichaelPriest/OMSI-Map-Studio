# Estrutura do repositório

[English](../en/REPOSITORY_STRUCTURE.md)

## Raiz

```text
.github/        automação e CI
src/            código do editor e bibliotecas
tests/          testes automatizados por domínio
docs/           documentação, releases e galeria
installer/      empacotamento Windows
tools/          scripts de manutenção e captura
README.md       entrada do projeto
Directory.Build.props  configuração compartilhada do MSBuild
.gitignore      regras de arquivos locais
```

Os READMEs localizados permanecem na raiz para preservar links públicos existentes. `Directory.Build.props` deve permanecer acima dos projetos para descoberta automática pelo MSBuild. Não versionar binários de build, cache, instalação local do OMSI ou relatórios temporários.

## Código

- `src/MapStudio.Native`: host WinUI 3, controles, serviços e ViewModels.
- `src/MapStudio.Core`: formatos OMSI, geração, configuração e workspace.
- `src/MapStudio.Renderer`: Direct3D 11, cena, picking e viewport.
- `src/MapStudio.Desktop` e `src/MapStudio.UI`: trilha legada, mantida por compatibilidade.

## Testes

Os projetos .csproj continuam nos mesmos caminhos. Os arquivos .cs foram movidos por domínio, sem alterar conteúdo, namespaces ou referências de projeto. Os globs do SDK incluem subpastas automaticamente. O CI continua testando os projetos completos.

| Pasta | Arquivos movidos |
| --- | ---: |
| `tests/MapStudio.Core.Tests/AI` | 6 |
| `tests/MapStudio.Core.Tests/Commercial` | 1 |
| `tests/MapStudio.Core.Tests/Editing` | 2 |
| `tests/MapStudio.Core.Tests/Generation` | 1 |
| `tests/MapStudio.Core.Tests/Generation/Buildings` | 7 |
| `tests/MapStudio.Core.Tests/Generation/Geography` | 3 |
| `tests/MapStudio.Core.Tests/Generation/Infrastructure` | 10 |
| `tests/MapStudio.Core.Tests/Generation/Roads` | 21 |
| `tests/MapStudio.Core.Tests/Generation/Scene` | 9 |
| `tests/MapStudio.Core.Tests/Generation/Terrain` | 8 |
| `tests/MapStudio.Core.Tests/Generation/Vegetation` | 8 |
| `tests/MapStudio.Core.Tests/Omsi` | 9 |
| `tests/MapStudio.Core.Tests/Omsi/Assets` | 9 |
| `tests/MapStudio.Core.Tests/Omsi/Config` | 1 |
| `tests/MapStudio.Core.Tests/Omsi/Objects` | 3 |
| `tests/MapStudio.Core.Tests/Omsi/Scenery` | 4 |
| `tests/MapStudio.Core.Tests/Omsi/Splines` | 7 |
| `tests/MapStudio.Core.Tests/Omsi/Tiles` | 13 |
| `tests/MapStudio.Core.Tests/Omsi/Timetables` | 13 |
| `tests/MapStudio.Core.Tests/Settings` | 1 |
| `tests/MapStudio.Core.Tests/Simulators` | 1 |
| `tests/MapStudio.Core.Tests/Workspace` | 2 |
| `tests/MapStudio.Renderer.Tests/Graphics` | 5 |
| `tests/MapStudio.Renderer.Tests/Picking` | 4 |
| `tests/MapStudio.Renderer.Tests/Scene` | 12 |
| `tests/MapStudio.Renderer.Tests/Scene/Objects` | 7 |
| `tests/MapStudio.Renderer.Tests/Scene/Roads` | 10 |
| `tests/MapStudio.Renderer.Tests/Scene/Terrain` | 3 |
| `tests/MapStudio.Renderer.Tests/Scene/Traffic` | 2 |
| `tests/MapStudio.Renderer.Tests/Viewport` | 16 |

Novos testes devem entrar no domínio correspondente. As notas históricas de releases podem mencionar caminhos anteriores à reorganização.

## Imagens e builds

Capturas revisadas em `docs/images/editor/`; capturas temporárias e pacotes em `artifacts/` (ignorado). Consulte [galeria](EDITOR_GALLERY.md) e [manual](USER_MANUAL.md).
