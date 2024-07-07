using System;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[GitHubActions(
    "ci",
    GitHubActionsImage.WindowsLatest,
    On = [GitHubActionsTrigger.Push],
    InvokedTargets = [nameof(Test)],
    FetchDepth = 0
)]
[GitHubActions(
    "publish",
    GitHubActionsImage.WindowsLatest,
    On = [GitHubActionsTrigger.WorkflowDispatch],
    InvokedTargets = [nameof(Publish)],
    ImportSecrets = [nameof(NuGetApiKey)],
    FetchDepth = 0
)]
class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Pack);

    [Secret] [Parameter("NuGet API key")] readonly string NuGetApiKey = null!;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")] readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [GitVersion] readonly GitVersion GitVersion = null!;

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            DotNetClean();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore();
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(c => c
                .EnableNoRestore()
                .SetConfiguration(Configuration)
            );
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(c => c
                .EnableNoRestore()
                .EnableNoBuild()
                .SetConfiguration(Configuration)
            );
        });

    Target Pack => _ => _
        .DependsOn(Restore)
        .Produces(OutputDirectory / "*.nupkg")
        .Executes(() =>
        {
            DotNetPublish(c => c
                .EnableNoRestore()
                .SetConfiguration(Configuration)
                .SetVersion(GitVersion.AssemblySemVer)
            );
            DotNetPack(c => c
                .EnableNoRestore()
                .EnableNoBuild()
                .SetConfiguration(Configuration)
                .SetVersion(Version)
                .SetOutputDirectory(OutputDirectory)
            );
        });

    Target Publish => _ => _
        .DependsOn(Pack)
        .Requires(() => Configuration == Configuration.Release)
        .Executes(() =>
        {
            DotNetNuGetPush(c => c
                .SetTargetPath(PackagePath)
                .SetApiKey(NuGetApiKey));
        });

    AbsolutePath PackagePath => OutputDirectory / $"Seq.App.OTelMetrics.{Version}.nupkg";

    string Version => Configuration == Configuration.Release
        ? GitVersion.FullSemVer
        : $"{GitVersion.SemVer}.{SecondsSinceLastCommit}";

    int SecondsSinceLastCommit => (int)(Now - DateTime.Parse(GitVersion.CommitDate)).TotalSeconds;
    readonly DateTime Now = DateTime.Now;
    static AbsolutePath OutputDirectory => RootDirectory / "dist";
}