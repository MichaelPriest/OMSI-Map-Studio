# Migração nativa do OMSI Map Studio

Esta trilha recria o editor sobre a arquitetura nativa do Windows sem descartar o domínio OMSI já implementado.

## Stack alvo

- .NET 10;
- WinUI 3 / Windows App SDK;
- Direct3D 11;
- Vortice.Windows;
- MapStudio.Core preservado como autoridade de formatos, leitura, validação e gravação;
- SQLite/cache preservados;
- sem WebView2 no viewport principal;
- sem React no lifecycle do renderer.

A base usa Windows App SDK 2.5.1 e Vortice.Direct3D11 3.8.3.

## Estratégia

A migração acontece em paralelo ao editor atual.

### Fase N0 — fundação

- novo projeto `MapStudio.Renderer`;
- novo projeto `MapStudio.Native`;
- Direct3D 11 inicializado em runtime próprio;
- input de mouse recebido diretamente pelo WinUI;
- registry de picking com ID estável;
- codec para ID buffer;
- CI dedicado em Windows.

### Fase N1 — viewport OMSI mínimo

- ligar `SwapChainPanel` à swap chain DXGI;
- câmera perspectiva/top;
- terreno Gundorf;
- objetos O3D reais;
- splines;
- ID buffer;
- hover e seleção;
- validação de DPI 100/125/150/200%.

### Fase N2 — edição

- gizmo mover;
- gizmo rotacionar;
- snap;
- Inspector;
- salvar via MapStudio.Core;
- undo/redo.

### Fase N3 — interface completa

- Explorer nativo;
- bibliotecas;
- previews;
- construction tools;
- terreno;
- mapa real;
- diagnostics/Map Health;
- fullscreen;
- atalhos.

### Fase N4 — substituição

A versão nativa só substitui o host WebView2 quando atingir paridade funcional suficiente e passar os testes reais. Até lá, o aplicativo atual continua disponível para comparação.

## Seleção

A seleção nativa não dependerá de material, transparência ou textura do objeto. Cada entidade OMSI recebe um `PickingId`. Em uma passagem própria, o renderer grava esse ID num render target inteiro. O pixel sob o cursor identifica diretamente a entidade selecionada.

Isso elimina a cadeia de fallbacks que se tornou necessária no viewport WebView2/Babylon.
