
#tool "nuget:?package=NUnit.ConsoleRunner"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var version = Argument("nuget_version", "1.0.0.0-beta");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

var solution = "./src/FragmentedFileUpload.sln";
var nuspec = GetFiles("./**/*.nuspec");

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Restore-NuGet-Packages")
    .Does(() =>
{
    NuGetRestore(solution);
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    if(IsRunningOnWindows())
    {
        // Use MSBuild
        MSBuild(solution, settings => {
            settings.SetConfiguration(configuration);
            settings.MSBuildPlatform = Cake.Common.Tools.MSBuild.MSBuildPlatform.x86;
        });
    }
    else
    {
        // Use DotNetBuild
        DotNetBuild(solution, settings =>
            settings.SetConfiguration(configuration));
    }
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    NUnit3("./src/build/tests/bin/" + configuration + "/**/*.Tests.dll");
});

Task("NuGet")
    .IsDependentOn("Test")
    .Does (() =>
{
    if(!DirectoryExists("./build/nuget/"))
        CreateDirectory("./build/nuget");
        
    NuGetPack(nuspec, new NuGetPackSettings {
        ArgumentCustomization = args=>args.Append("-Properties configuration=" + configuration),
        BasePath = "./",
        OutputDirectory = "./build/nuget/",
        Symbols = true,
        Verbosity = NuGetVerbosity.Detailed,
        Version = version
    });	
});

Task("Default")
  .IsDependentOn("NuGet");

RunTarget(target);