//////////////////////////////////////////////////////////////////////
// ADDINS
//////////////////////////////////////////////////////////////////////

#addin "Cake.FileHelpers"

//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////

#tool "nuget:?package=GitReleaseManager"
#tool "nuget:?package=GitVersion.CommandLine"
#tool "nuget:?package=OpenCover"
#tool "nuget:?package=ReportGenerator"
#tool "nuget:?package=vswhere"
#tool "nuget:?package=xunit.runner.console"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
if (string.IsNullOrWhiteSpace(target))
{
    target = "Default";
}

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

var solution = "./src/WeakEventListener.sln";

// Should MSBuild treat any errors as warnings?
var treatWarningsAsErrors = false;

// Build configuration
var local = BuildSystem.IsLocalBuild;
var isPullRequest = AppVeyor.Environment.PullRequest.IsPullRequest;
var isRepository = StringComparer.OrdinalIgnoreCase.Equals("ghuntley/weakeventlistener", AppVeyor.Environment.Repository.Name);

var isDevelopBranch = StringComparer.OrdinalIgnoreCase.Equals("develop", AppVeyor.Environment.Repository.Branch);
var isReleaseBranch = StringComparer.OrdinalIgnoreCase.Equals("master", AppVeyor.Environment.Repository.Branch);
var isTagged = AppVeyor.Environment.Repository.Tag.IsTag;


// Version
var gitVersion = GitVersion();
var majorMinorPatch = gitVersion.MajorMinorPatch;
var informationalVersion = gitVersion.InformationalVersion;
var nugetVersion = gitVersion.NuGetVersion;
var buildVersion = gitVersion.FullBuildMetaData;

// Artifacts
var artifactDirectory = "./artifacts/";
var testCoverageOutputFile = artifactDirectory + "OpenCover.xml";
var packageWhitelist = new[] { "ReactiveUI-Testing",
                               "ReactiveUI-Events",
                               "ReactiveUI-Events-WPF",
                               "ReactiveUI-Events-XamForms",
                               "ReactiveUI",
                               "ReactiveUI-AndroidSupport",
                               "ReactiveUI-Blend",
                               "ReactiveUI-WPF",
                               "ReactiveUI-Winforms",
                               "ReactiveUI-XamForms" };

// Define global marcos.
Action Abort = () => { throw new Exception("a non-recoverable fatal error occurred."); };

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup(context =>
{
    Information("Building version {0}. (isTagged: {1})", informationalVersion, isTagged);

    CreateDirectory(artifactDirectory);
});

Teardown(context =>
{
    // Executed AFTER the last task.
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("UpdateAppVeyorBuildNumber")
    .WithCriteria(() => AppVeyor.IsRunningOnAppVeyor)
    .Does(() =>
{
    AppVeyor.UpdateBuildVersion(buildVersion);

});

Task("Build")
    .Does (() =>
{
    Information("Building {0}", solution);

    FilePath msBuildPath = VSWhereLatest().CombineWithFilePath("./MSBuild/15.0/Bin/MSBuild.exe");

    MSBuild(solution, new MSBuildSettings() {
            ToolPath= msBuildPath
        }
        .WithTarget("restore;pack")
        .WithProperty("PackageOutputPath",  MakeAbsolute(Directory(artifactDirectory)).ToString())
        .WithProperty("TreatWarningsAsErrors", treatWarningsAsErrors.ToString())
        .SetConfiguration("Release")
        // Due to https://github.com/NuGet/Home/issues/4790 and https://github.com/NuGet/Home/issues/4337 we
        // have to pass a version explicitly
        .WithProperty("Version", nugetVersion.ToString())
        .SetVerbosity(Verbosity.Minimal)
        .SetNodeReuse(false));
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    Action<ICakeContext> testAction = tool => {

        tool.XUnit2("./src/**/Release/**/*.Tests.dll", new XUnit2Settings {
            OutputDirectory = artifactDirectory,
            XmlReportV1 = true,
            NoAppDomain = true
        });
    };

    OpenCover(testAction,
        testCoverageOutputFile,
        new OpenCoverSettings {
            ReturnTargetCodeOffset = 0,
            ArgumentCustomization = args => args.Append("-mergeoutput")
        }
        .WithFilter("+[*]* -[*.Testing]* -[*.Tests*]*")
        .ExcludeByAttribute("*.ExcludeFromCodeCoverage*")
        .ExcludeByFile("*/*Designer.cs;*/*.g.cs;*/*.g.i.cs;*splat/splat*"));

    ReportGenerator(testCoverageOutputFile, artifactDirectory);
});

Task("Package")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .Does (() =>
{
});


//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Test")
    .Does (() =>
{
});

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);