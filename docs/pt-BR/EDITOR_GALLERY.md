# Galeria do editor nativo

[English](../en/EDITOR_GALLERY.md)

As imagens devem ser capturas PNG do executável real, não mockups. Nesta atualização não há novas capturas verificadas. Para atualizar a galeria, use a [build Windows x64 validada](https://github.com/MichaelPriest/OMSI-Map-Studio/actions/runs/46060172393), abra uma instalação autorizada do OMSI 2 em workspace separado e capture a janela real (Alt+Print Screen ou Win+Shift+S). Evite dados pessoais, credenciais e conteúdo sem autorização.

| Captura a produzir | Arquivo previsto |
| --- | --- |
| Visão geral 3D e módulos | `docs/images/editor/01-overview.png` |
| Road Studio: início, continuação e precisão | `docs/images/editor/02-road-studio.png` |
| Perfil longitudinal | `docs/images/editor/03-road-profile.png` |
| Geo Studio: seleção OSM e Terrarium | `docs/images/editor/04-geo-studio.png` |
| Terreno reconstruído e continuidade dos tiles | `docs/images/editor/05-terrain.png` |
| Referência de hidrografia com ilhas | `docs/images/editor/06-hydrography.png` |
| Biblioteca e Inspector | `docs/images/editor/07-assets-inspector.png` |
| Validação do mapa | `docs/images/editor/08-validation.png` |

Registrar commit de BUILD-INFO.txt, resolução, DPI e cenário junto das imagens. Usar o mesmo conjunto de PNGs nas duas documentações; só incorporar links depois de os arquivos existirem. Capturas devem mostrar o resultado real, inclusive limitações, sem simular funções ausentes.

## Captura automatizada

O CI nativo possui um job separado de captura em Windows. Ele inicia a build real, tenta capturar a janela em primeiro plano e, quando acessíveis, as telas Road Studio e Geo Studio sem mapa carregado. O artefato `OMSI-Map-Studio-editor-screenshots` inclui PNGs e manifesto SHA-256. O runner pode não possuir desktop interativo; nesse caso a captura é sinalizada como indisponível, sem invalidar a build. A captura ainda precisa de revisão visual antes de ser incorporada à galeria.

Para uma captura com mapa real carregado, em uma sessão Windows interativa, execute a partir da raiz do repositório:

```powershell
powershell -NoProfile -File tools/capture_native_editor.ps1 -ProcessId (Get-Process MapStudio.Native).Id -OutputDirectory docs/images/editor -Name 05-terrain -Commit '<commit de BUILD-INFO.txt>'
```

Mantenha somente uma instância do editor aberta. Revise os PNGs para confirmar que o painel desejado está visível e não contém credenciais. O script não cria mapas artificiais nem salva modificações no mapa.
