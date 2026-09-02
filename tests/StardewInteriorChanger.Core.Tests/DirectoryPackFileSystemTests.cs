using System.Diagnostics;
using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class DirectoryPackFileSystemTests
{
    private const string PackId = "Example.Interiors";

    [Fact]
    public void Build_MapThroughDirectoryLinkOutsidePack_IsRejectedWithoutReadingTarget()
    {
        using var fixture = new TemporaryPack();
        Directory.CreateDirectory(Path.Combine(fixture.PackPath, "assets"));
        File.WriteAllText(Path.Combine(fixture.OutsidePath, "interior.tmx"), "outside-map");
        string linkedGameplayRoot = Path.Combine(fixture.PackPath, "assets", "test");
        CreateDirectoryLink(linkedGameplayRoot, fixture.OutsidePath);

        try
        {
            RegistryBuildResult result = Build(fixture.PackPath);

            Assert.Empty(result.Registry.Entries);
            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics, diagnostic =>
                diagnostic.Code == RegistryDiagnosticCode.ContentReadFailed);
            Assert.Contains("symbolic links or junctions", diagnostic.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(linkedGameplayRoot);
        }
    }

    [Fact]
    public void Build_DirectoryLinkBelowGameplayRoot_IsRejectedWithoutEnumeratingTarget()
    {
        using var fixture = new TemporaryPack();
        string gameplayRoot = Path.Combine(fixture.PackPath, "assets", "test");
        Directory.CreateDirectory(gameplayRoot);
        File.WriteAllText(Path.Combine(gameplayRoot, "interior.tmx"), "map");
        File.WriteAllText(Path.Combine(fixture.OutsidePath, "outside-tiles.png"), "outside-tiles");

        string linkedDirectory = Path.Combine(gameplayRoot, "linked");
        CreateDirectoryLink(linkedDirectory, fixture.OutsidePath);

        try
        {
            RegistryBuildResult result = Build(fixture.PackPath);

            Assert.Empty(result.Registry.Entries);
            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics, diagnostic =>
                diagnostic.Code == RegistryDiagnosticCode.ContentEnumerationFailed);
            Assert.Contains("symbolic links or junctions", diagnostic.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(linkedDirectory);
        }
    }

    private static RegistryBuildResult Build(string packPath)
    {
        var document = new InteriorPackDocument
        {
            FormatVersion = InteriorRegistryBuilder.SupportedFormatVersion,
            Interiors = new List<InteriorDefinitionDto>
            {
                new()
                {
                    Id = "test",
                    DisplayName = "Test",
                    Target = "Greenhouse",
                    GameplayRoot = "assets/test",
                    Map = "interior.tmx"
                }
            }
        };

        return new InteriorRegistryBuilder().Build(
            PackId,
            document,
            new DirectoryPackFileSystem(packPath));
    }

    private static void CreateDirectoryLink(string path, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(path, target);
            return;
        }

        string commandProcessor = Environment.GetEnvironmentVariable("ComSpec")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        var startInfo = new ProcessStartInfo(commandProcessor)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(path);
        startInfo.ArgumentList.Add(target);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the Windows command processor.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new IOException(
                $"Could not create test junction (exit {process.ExitCode}): {output}{error}");
        }
    }

    private sealed class TemporaryPack : IDisposable
    {
        public TemporaryPack()
        {
            BasePath = Path.Combine(
                Path.GetTempPath(),
                "StardewInteriorChanger.Core.Tests",
                Guid.NewGuid().ToString("N"));
            PackPath = Path.Combine(BasePath, "pack");
            OutsidePath = Path.Combine(BasePath, "outside");
            Directory.CreateDirectory(PackPath);
            Directory.CreateDirectory(OutsidePath);
        }

        public string BasePath { get; }

        public string PackPath { get; }

        public string OutsidePath { get; }

        public void Dispose()
        {
            if (Directory.Exists(BasePath))
            {
                Directory.Delete(BasePath, recursive: true);
            }
        }
    }
}
