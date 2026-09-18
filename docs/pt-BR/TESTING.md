# Testes — v0.1.0-alpha.2

[English](../en/TESTING.md) · **Português (Brasil)**

Esta é a segunda versão pública de teste do OMSI Map Studio. Ela é deliberadamente **somente leitura**.

## Instalação

1. Baixe o arquivo `OMSI-Map-Studio-v0.1.0-alpha.2-win-x64.zip` da prerelease.
2. Extraia o ZIP para uma pasta comum.
3. Execute `OMSI Map Studio.exe`.
4. O Microsoft Edge WebView2 Runtime precisa estar disponível no Windows.
5. Clique em **Abrir OMSI** e selecione a pasta raiz do OMSI 2, a pasta que contém `maps`, `Sceneryobjects` e `Splines`.

O pacote é self-contained para .NET 10; não exige instalação separada do .NET Desktop Runtime.

## Principal correção da alpha.2

A abertura da pasta raiz do OMSI agora usa catálogo leve. O programa lê primeiro apenas os `global.cfg`; os arquivos `.map` são carregados somente para o mapa selecionado e cada tile é lido uma única vez para estatísticas e objetos.

Isso deve reduzir bastante a demora inicial em instalações com muitos mapas.

## O que testar

- tempo de abertura da pasta raiz do OMSI;
- abertura e troca da instalação do OMSI;
- mudança entre mapas e reutilização do conteúdo já carregado;
- lista de mapas encontrados;
- quantidade e disposição dos tiles;
- indicação de tiles ausentes;
- quantidade de objetos e splines;
- seleção de objetos em mapas cartesianos;
- dados do `.map` no inspetor;
- `friendlyname`, grupos e meshes do `.sco`;
- indicação de meshes encontrados/ausentes;
- preview geométrico de meshes `.o3d` não criptografados.

## Limitações conhecidas

- não existe criação ou salvamento de mapas nesta alpha;
- splines e terreno ainda não são renderizados;
- texturas e materiais do O3D ainda não são aplicados;
- arquivos O3D criptografados não recebem preview geométrico;
- arquivos `.x` são detectados, mas ainda não são renderizados;
- mapas com `[worldcoordinates]` usam visualização esquemática e ainda não posicionam objetos globalmente;
- altura do terreno ainda não é aplicada à posição visual dos objetos;
- pitch/bank e orientação de alguns objetos ainda precisam de validação visual em mapas reais;
- meshes muito grandes podem ser recusados pelo limite de segurança do preview.

## Segurança

Esta versão não possui operação de escrita de mapas. O botão **Salvar** permanece desabilitado.

Não use esta alpha como substituta do editor original para modificar mapas. O objetivo é validar leitura, compatibilidade e visualização antes de habilitar qualquer gravação.
