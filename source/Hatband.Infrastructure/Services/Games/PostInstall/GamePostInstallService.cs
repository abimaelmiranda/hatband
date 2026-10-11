using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Enums.Host;
using Hatband.Core.Models.Games;
using Hatband.Core.Models.Games.PostInstall;

namespace Hatband.Infrastructure.Services.Games.PostInstall;

/// <summary>Applies a source-provided post-install plan inside a game's compatibility prefix.</summary>
public sealed class GamePostInstallService
{
    private const string StateDirectoryName = ".hatband";
    private const string StateFileName = "post_install_state.json";
    private static readonly JsonSerializerOptions StateJsonOptions = new(JsonSerializerDefaults.Web);

    public void ValidateDefinition(GamePostInstallPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ValidatePlan(plan);
        if (plan.Diagnostics.Count > 0)
        {
            var diagnostics = string.Join(Environment.NewLine, plan.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
            throw new InvalidOperationException($"The post-install definition contains errors:{Environment.NewLine}{diagnostics}");
        }
    }

    public async Task PrepareAsync(
        GamePostInstallPlan plan,
        IGamePostInstallEnvironment environment,
        string prefixPath,
        IProgress<GamePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefixPath);
        if (!Path.IsPathFullyQualified(prefixPath))
        {
            throw new ArgumentException("The compatibility prefix path must be absolute.", nameof(prefixPath));
        }

        ValidateDefinition(plan);

        var statePath = Path.Combine(prefixPath, StateDirectoryName, StateFileName);
        var stateDirectory = Path.GetDirectoryName(statePath)
            ?? throw new InvalidOperationException("The post-install state file must have a parent directory.");
        Directory.CreateDirectory(stateDirectory);

        var state = await ReadStateAsync(statePath, plan.Revision, cancellationToken);
        var is64BitWindows = await environment.Is64BitWindowsAsync(cancellationToken);
        var completedSteps = 0;
        for (var index = 0; index < plan.Steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = plan.Steps[index];
            if (!ShouldRun(step, is64BitWindows))
            {
                completedSteps++;
                Report(progress, GetStage(step), step.Name, completedSteps, plan.Steps.Count);
                continue;
            }

            var fingerprint = GetFingerprint(step);
            try
            {
                if (step is GamePostInstallInstallerStep installerStep && installerStep.HasRunKey is not null)
                {
                    var marker = await environment.ReadRegistryValueAsync(
                        installerStep.HasRunKey,
                        installerStep.RegistryView,
                        step.Name,
                        cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (marker is GamePostInstallDwordRegistryEntry markerValue && markerValue.Value >= installerStep.MinimumHasRunValue)
                    {
                        state.CompletedSteps[step.Id] = fingerprint;
                        state.ToleratedFailures.Remove(step.Id);
                        await WriteStateAsync(statePath, state, cancellationToken);
                        completedSteps++;
                        Report(progress, GetStage(step), step.Name, completedSteps, plan.Steps.Count);
                        continue;
                    }
                }
                else if (state.CompletedSteps.TryGetValue(step.Id, out var completedFingerprint)
                    && string.Equals(completedFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    completedSteps++;
                    Report(progress, GetStage(step), step.Name, completedSteps, plan.Steps.Count);
                    continue;
                }

                var stage = GetStage(step);
                Report(progress, stage, step.Name, completedSteps, plan.Steps.Count);
                var outcome = await ApplyStepAsync(step, environment, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (outcome.Succeeded)
                {
                    state.CompletedSteps[step.Id] = fingerprint;
                    state.ToleratedFailures.Remove(step.Id);
                }
                else
                {
                    state.CompletedSteps.Remove(step.Id);
                    state.ToleratedFailures[step.Id] = outcome.ToleratedFailure
                        ?? throw new InvalidOperationException("A tolerated installer failure must include its outcome.");
                }

                await WriteStateAsync(statePath, state, cancellationToken);
                completedSteps++;
                Report(progress, stage, step.Name, completedSteps, plan.Steps.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Post-install step '{step.Name}' failed: {exception.Message}", exception);
            }
        }

        Report(progress, GamePreparationStage.Completed, null, completedSteps, plan.Steps.Count);
    }

    private static async Task<PostInstallStepOutcome> ApplyStepAsync(
        GamePostInstallStep step,
        IGamePostInstallEnvironment environment,
        CancellationToken cancellationToken)
    {
        switch (step)
        {
            case GamePostInstallRegistryStep registryStep:
                await environment.EnsureRegistryKeyAsync(registryStep.KeyPath, registryStep.View, cancellationToken);
                foreach (var value in registryStep.Values)
                {
                    var resolvedValue = value is GamePostInstallStringRegistryEntry stringValue
                        ? stringValue with
                        {
                            Value = await environment.ResolveStringAsync(
                                stringValue.Value,
                                registryStep.InstallDirectory,
                                cancellationToken)
                        }
                        : value;
                    await environment.SetRegistryValueAsync(registryStep.KeyPath, registryStep.View, resolvedValue, cancellationToken);
                }

                return new PostInstallStepOutcome(true, null);

            case GamePostInstallInstallerStep installerStep:
                var installerOutcome = await RunInstallerAsync(installerStep, environment, cancellationToken);
                if (installerOutcome.Succeeded && installerStep.HasRunKey is not null)
                {
            cancellationToken.ThrowIfCancellationRequested();
                    var view = installerStep.RegistryView;
                    await environment.EnsureRegistryKeyAsync(installerStep.HasRunKey, view, cancellationToken);
                    await environment.SetRegistryValueAsync(
                        installerStep.HasRunKey,
                        view,
                        new GamePostInstallDwordRegistryEntry
                        {
                            Name = step.Name,
                            Value = installerStep.MinimumHasRunValue
                        },
                        cancellationToken);
                }

                return installerOutcome;

            case GamePostInstallCopyFolderStep copyStep:
                var sourceDirectory = await environment.ResolvePathAsync(copyStep.SourceTemplate, copyStep.InstallDirectory, cancellationToken);
                var destinationDirectory = await environment.ResolvePathAsync(copyStep.DestinationTemplate, copyStep.InstallDirectory, cancellationToken);
                await environment.CopyDirectoryAsync(sourceDirectory, destinationDirectory, cancellationToken);
                return new PostInstallStepOutcome(true, null);

            default:
                throw new InvalidOperationException($"Unsupported post-install step type '{step.GetType().Name}'.");
        }
    }

    private static async Task<PostInstallStepOutcome> RunInstallerAsync(
        GamePostInstallInstallerStep step,
        IGamePostInstallEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (step.Processes.Count == 0)
        {
            throw new InvalidOperationException("The installer has no process entries.");
        }

        var toleratedFailures = new List<string>();
        foreach (var process in step.Processes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var executable = await environment.ResolvePathAsync(process.ExecutableTemplate, step.InstallDirectory, cancellationToken);
            IReadOnlyList<string> arguments = process.Arguments is null
                ? []
                : ParseArguments(await environment.ResolveStringAsync(
                    process.Arguments,
                    step.InstallDirectory,
                    cancellationToken));
            var workingDirectory = Path.GetDirectoryName(executable);
            var result = await environment.RunProcessAsync(executable, arguments, workingDirectory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.ExitCode != 0 && !step.IgnoreExitCode)
            {
                var detail = string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput.Trim()
                    : result.StandardError.Trim();
                var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}";
                throw new InvalidOperationException($"Installer process '{Path.GetFileName(executable)}' exited with code {result.ExitCode}.{suffix}");
            }

            if (result.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput.Trim()
                    : result.StandardError.Trim();
                var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}";
                toleratedFailures.Add($"'{Path.GetFileName(executable)}' exited with code {result.ExitCode}.{suffix}");
            }
        }

        return toleratedFailures.Count == 0
            ? new PostInstallStepOutcome(true, null)
            : new PostInstallStepOutcome(false, string.Join(Environment.NewLine, toleratedFailures));
    }

    private static IReadOnlyList<string> ParseArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return [];
        }

        var parsed = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        var started = false;
        for (var index = 0; index < arguments.Length; index++)
        {
            var character = arguments[index];
            if (character == '\\' && quote != '\'')
            {
                if (index + 1 < arguments.Length && (arguments[index + 1] == '"' || arguments[index + 1] == '\\'))
                {
                    current.Append(arguments[++index]);
                }
                else
                {
                    current.Append(character);
                }

                started = true;
                continue;
            }

            if (quote is null && (character == '"' || character == '\''))
            {
                quote = character;
                started = true;
                continue;
            }

            if (quote == character)
            {
                quote = null;
                continue;
            }

            if (quote is null && char.IsWhiteSpace(character))
            {
                if (started)
                {
                    parsed.Add(current.ToString());
                    current.Clear();
                    started = false;
                }

                continue;
            }

            current.Append(character);
            started = true;
        }

        if (quote is not null)
        {
            throw new InvalidOperationException("Installer arguments contain an unmatched quote.");
        }

        if (started)
        {
            parsed.Add(current.ToString());
        }

        return parsed;
    }

    private static bool ShouldRun(GamePostInstallStep step, bool is64BitWindows)
    {
        if (step.Condition?.Is64BitWindows is bool requiredArchitecture && requiredArchitecture != is64BitWindows)
        {
            return false;
        }

        if (step.Condition?.OperatingSystem is HostOperatingSystem.Unknown)
        {
            throw new InvalidOperationException($"Post-install step '{step.Name}' has an unsupported operating-system condition.");
        }

        if (step.Condition?.OperatingSystem is HostOperatingSystem requiredOperatingSystem
            && requiredOperatingSystem != HostOperatingSystem.Windows)
        {
            return false;
        }

        return true;
    }

    private static GamePreparationStage GetStage(GamePostInstallStep step)
    {
        return step switch
        {
            GamePostInstallRegistryStep => GamePreparationStage.PreparingRegistry,
            GamePostInstallInstallerStep => GamePreparationStage.InstallingDependency,
            GamePostInstallCopyFolderStep => GamePreparationStage.CopyingFiles,
            _ => throw new InvalidOperationException($"Unsupported post-install step type '{step.GetType().Name}'.")
        };
    }

    private static void ValidatePlan(GamePostInstallPlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plan.Revision);
        ArgumentNullException.ThrowIfNull(plan.Steps);
        ArgumentNullException.ThrowIfNull(plan.Diagnostics);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in plan.Steps)
        {
            ArgumentNullException.ThrowIfNull(step);
            ArgumentException.ThrowIfNullOrWhiteSpace(step.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(step.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(step.InstallDirectory);
            if (!Path.IsPathFullyQualified(step.InstallDirectory))
            {
                throw new InvalidOperationException($"Post-install step '{step.Name}' has a non-absolute install directory.");
            }

            if (!ids.Add(step.Id))
            {
                throw new InvalidOperationException($"The post-install plan contains duplicate step id '{step.Id}'.");
            }
        }
    }

    private static string GetFingerprint(GamePostInstallStep step)
    {
        var json = JsonSerializer.Serialize(step, step.GetType(), StateJsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(hash);
    }

    private static async Task<PostInstallState> ReadStateAsync(
        string path,
        string revision,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return CreateState(revision);
        }

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            var state = await JsonSerializer.DeserializeAsync<PostInstallState>(stream, StateJsonOptions, cancellationToken);
            if (state is null)
            {
                throw new InvalidDataException("The saved post-install state is empty.");
            }

            if (state.CompletedSteps is null || state.ToleratedFailures is null)
            {
                throw new InvalidDataException("The saved post-install state is missing its step results.");
            }

            if (string.IsNullOrWhiteSpace(state.Revision))
            {
                throw new InvalidDataException("The saved post-install state has no plan revision.");
            }

            if (!string.Equals(state.Revision, revision, StringComparison.Ordinal))
            {
                return CreateState(revision);
            }

            return state;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The saved post-install state is invalid and cannot be safely resumed.", exception);
        }
    }

    private static async Task WriteStateAsync(string path, PostInstallState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The post-install state file must have a parent directory.");
        var temporaryPath = Path.Combine(directory, $".{StateFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, state, StateJsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void Report(
        IProgress<GamePreparationProgress>? progress,
        GamePreparationStage stage,
        string? stepName,
        int completedSteps,
        int totalSteps)
    {
        progress?.Report(new GamePreparationProgress(stage, stepName, completedSteps, totalSteps));
    }

    private static PostInstallState CreateState(string revision)
    {
        return new PostInstallState
        {
            Revision = revision,
            CompletedSteps = new Dictionary<string, string>(StringComparer.Ordinal),
            ToleratedFailures = new Dictionary<string, string>(StringComparer.Ordinal)
        };
    }

    private sealed class PostInstallState
    {
        public PostInstallState()
        {
        }

        public required string Revision { get; init; }

        public required Dictionary<string, string> CompletedSteps { get; init; }

        public required Dictionary<string, string> ToleratedFailures { get; init; }
    }

    private sealed record PostInstallStepOutcome(bool Succeeded, string? ToleratedFailure);
}
