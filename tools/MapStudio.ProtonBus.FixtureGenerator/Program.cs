using System.Security.Cryptography;
using MapStudio.Core.ProtonBus;

var outputRoot =
    args.Length > 0
        ? Path.GetFullPath(args[0])
        : Path.GetFullPath(
            Path.Combine(
                Environment.CurrentDirectory,
                "artifacts",
                "fixture"));

Directory.CreateDirectory(
    outputRoot);

var staging =
    Path.Combine(
        outputRoot,
        "staging");

if (
    Directory.Exists(
        staging))
{
    Directory.Delete(
        staging,
        recursive:
            true);
}

Directory.CreateDirectory(
    staging);

var fixture =
    ProtonBusValidationFixtureBuilder
        .Build(
            staging,
            createZipArchive:
                false);

var archivePath =
    Path.Combine(
        outputRoot,
        "MapStudio-ProtonBus-Validation-Fixture.zip");

ProtonBusPackageArchiveWriter
    .Write(
        fixture.Package,
        archivePath);

var digest =
    Convert
        .ToHexString(
            SHA256.HashData(
                File.ReadAllBytes(
                    archivePath)))
        .ToLowerInvariant();

var hashPath =
    archivePath +
    ".sha256";

File.WriteAllText(
    hashPath,
    digest +
    "  " +
    Path.GetFileName(
        archivePath) +
    Environment.NewLine);

var readmePath =
    Path.Combine(
        outputRoot,
        "MapStudio-ProtonBus-Validation-Fixture-README.txt");

File.WriteAllText(
    readmePath,
    """
    OMSI Map Studio — Proton Bus Validation Fixture

    Objetivo:
    Validar rapidamente o formato exportado pelo Map Studio dentro de uma build real do Proton Bus, sem depender de um mapa OMSI grande.

    O fixture inclui:
    - chão simples com collider;
    - path de veículo;
    - path de pedestre;
    - path de trem;
    - parada com trigger;
    - entrypoint;
    - mesh GPS;
    - semáforo com vermelho/amarelo/verde e trigger;
    - street light real.

    Instalação:
    Extraia o conteúdo de MapStudio-ProtonBus-Validation-Fixture.zip na pasta raiz de conteúdo/mods usada pela build Proton Bus alvo, preservando a pasta maps/.

    Mapa:
    MapStudioValidation

    Importante:
    Este fixture é experimental e serve para diagnóstico. Se algo não aparecer ou não funcionar, registre exatamente o item afetado (geometria, colisão, path, parada, GPS, semáforo ou luz) e a build do Proton Bus usada.
    """
    .Replace(
        "\n",
        Environment.NewLine));

Console.WriteLine(
    $"Fixture package: {archivePath}");

Console.WriteLine(
    $"SHA256: {digest}");

Console.WriteLine(
    $"Instructions: {readmePath}");
