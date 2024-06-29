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
    FetchDepth = 0
)]
[GitHubActions(
    "publish",
    GitHubActionsImage.WindowsLatest,
    On = [GitHubActionsTrigger.WorkflowDispatch],
    InvokedTargets = [nameof(Pack)],
    ImportSecrets = [nameof(NuGetApiKey)],
    FetchDepth = 0
)]
class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Pack);
    
    [Secret]
    [Parameter("NuGet API key")]
    readonly string NuGetApiKey = null!;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    
    [GitVersion]
    readonly GitVersion GitVersion = null!;

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
        .Executes(() =>
        {
            DotNetBuild(c => c.SetConfiguration(Configuration));
        });

    Target Pack => _ => _
        .DependsOn(Restore)
        .Produces(OutputDirectory / "*.nupkg")
        .Executes(() =>
        {
            DotNetPublish(c => c.SetConfiguration(Configuration));
            DotNetPack(c => c
                .SetConfiguration(Configuration)
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

    AbsolutePath PackagePath => OutputDirectory / $"Seq.App.OTelMetrics.{GitVersion.NuGetVersion}.nupkg";
    static AbsolutePath OutputDirectory => RootDirectory / "dist";
}
