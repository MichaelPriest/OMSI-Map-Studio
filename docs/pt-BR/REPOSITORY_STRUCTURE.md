# Organização de pastas do OMSI Map Studio

[English](../en/REPOSITORY_STRUCTURE.md) · **Português (Brasil)**

## Regra geral

A raiz do repositório deve conter somente arquivos de entrada e configuração global. Código de produto, testes, documentação, instalador e ferramentas ficam nas pastas próprias. Não mover arquivos de forma massiva sem atualizar referências, soluções, workflows e caminhos relativos e executar a CI Windows.

## Estrutura canônica

```text
OMSI-Map-Studio/
├── .github/                 # workflows e automações do repositório
├── docs/
│   ├── pt-BR/               # documentação em português
│   │   └── releases/        # notas de versão pt-BR
│   ├── en/                  # documentação em inglês
│   │   └── releases/        # release notes em inglês
│   ├── ARCHITECTURE.md      # visão de arquitetura compartilhada
│   └── ROADMAP.md           # direção técnica compartilhada
├── installer/               # scripts/projetos de empacotamento
├── src/
│   ├── MapStudio.Core/      # domínio, formatos OMSI, edição e geração
│   ├── MapStudio.Native/    # host desktop nativo, janelas e serviços
│   ├── MapStudio.Renderer/  # renderização, cena, picking e shaders
│   ├── MapStudio.Desktop/   # integração desktop e recursos
│   └── MapStudio.UI/        # interface web quando usada pelo produto
├── tests/                   # testes por componente
├── tools/                   # ferramentas auxiliares de desenvolvimento
├── .gitignore               # exclusão de artefatos locais
├── Directory.Build.props    # propriedades .NET compartilhadas
├── README.md                # entrada curta
├── README.pt-BR.md          # visão geral pt-BR
└── README.en.md             # visão geral inglês
```

## Regras de manutenção

- **Não adicionar pastas de build à raiz**: `bin/`, `obj/`, `node_modules/`, `dist/`, logs, perfis locais e resultados temporários não são código-fonte.
- **Core por domínio**: ao criar funcionalidades, colocar o código no subdomínio existente (por exemplo, `Editing`, `Generation`, `IO`, `Omsi`, `Workspace`) em vez de criar novas pastas genéricas na raiz.
- **UI separada do domínio**: componentes visuais ficam em Native/Renderer/UI conforme a responsabilidade; regras de arquivos OMSI e persistência não devem ser movidas para a UI.
- **Testes próximos por componente**: adicionar testes ao projeto de teste existente correspondente. Criar novo projeto de teste somente quando a separação for real.
- **Documentação por idioma**: manter pt-BR e inglês juntos, com estrutura equivalente.
- **Release notes organizadas**: notas pertencem a `docs/pt-BR/releases/` e `docs/en/releases/`, nunca à raiz. Uma reorganização adicional por linha de versão deve atualizar links e ser feita separadamente.
- **Arquivos globais limitados**: novos arquivos na raiz exigem justificativa (configuração que precisa ser global, ponto de entrada ou instrução principal).
- **Não apagar ou mover por aparência**: antes de remover/mover, buscar referências em projetos, scripts, workflows, documentação e caminhos relativos.
- **Sem alteração funcional nesta política**: organizar pastas não pode alterar formatos OMSI, comportamento do editor ou conteúdo de mapas.

## Auditoria inicial

Na árvore Git atual, a raiz tem cinco arquivos e seis diretórios principais (`.github`, `docs`, `installer`, `src`, `tests`, `tools`). Portanto, o maior volume não está na raiz em si: está principalmente sob `docs/*/releases`, além dos arquivos internos dos projetos. Pastas `bin/obj` ou outras geradas que aparecem no computador local devem ser tratadas como artefatos e não versionadas.

Esta auditoria não renomeia nem move arquivos existentes. Ela estabelece a estrutura-alvo para que a limpeza seja segura e incremental.
