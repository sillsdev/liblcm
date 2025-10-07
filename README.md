# LCModel Library

## Description

The library for the SIL Language and Culture Model.

The liblcm library is the core [FieldWorks](https://github.com/sillsdev/FieldWorks) model for
linguistic analyses of languages. Tools in this library provide the ability to store and interact
with language and culture data, including anthropological, text corpus, and linguistics data.

## Instructions

1. Install Required Software

    - git
    - Visual Studio 2019 (with C++), MonoDevelop, or JetBrains Rider

2. Clone the liblcm repository

    - Open a terminal (or git bash on Windows) and cd into a desired directory.
    - Run `git clone https://github.com/sillsdev/liblcm.git`

3. Build liblcm

    - cd into the directory of the cloned liblcm repository.

    On Windows:

    - Run the appropriate `vsvars*.bat`. Alternatively, `LCM.sln` can be built from within Visual Studio.
    - Run `build.cmd` to build the liblcm library.

    On Linux:

    - Run `build.sh` to build the liblcm library.

By default, this will build liblcm in the Debug configuration.
To build with a different configuration, use:

```bash
build.(cmd|sh) (Debug|Release)
```

## Debugging

The LCModel library consumes multiple libpalaso files as NuGet packages. FieldWorks and other
projects consume LCModel as a NuGet package. Several options to debug across NuGet dependencies are
discussed on [this wiki](https://github.com/sillsdev/libpalaso/wiki/Developing-with-locally-modified-nuget-packages).
To publish and consume LCModel through local sources:

- Set an environment variable `LOCAL_NUGET_REPO` with the path to a folder on your computer (or
  local network) to publish locally-built packages
- See [these instructions](https://docs.microsoft.com/en-us/nuget/hosting-packages/local-feeds)
  to enable local package sources
- `build /t:pack` will pack nuget packages and publish them to `LOCAL_NUGET_REPO`

## Tests

### Linux

#### In JetBrains Rider

Open the solution in Rider and run them all there. Right-click the solution and choose _"Run Unit Tests"_.

#### In a terminal

- Install `NUnit.ConsoleRunner`
- then run (adjust the version number `3.11.1` accordingly):

	```bash
	(. environ && cd artifacts/Debug/net462/ && mono --debug ~/.nuget/packages/nunit.consolerunner/3.11.1/tools/nunit3-console.exe *Tests.dll )
	```

### Windows

#### With ReSharper

Open the solution in Visual Studio and run them all there. Right-click the solution and choose _"Run Unit Tests"_.

#### Without ReSharper

To run the tests for a single test dll:

1. Go to the `liblcm` directory.
2. Execute: `"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\msbuild.exe"`

   **Note:** Running the tests after building the solution from inside VS resulted in a `BadImageFormatException`.
   Running the tests after building from the cmd prompt worked.
3. Go to the `liblcm\artifacts\Debug\net462` directory.
4. Execute: `..\..\..\packages\NUnit.ConsoleRunner.3.9.0\tools\nunit3-console.exe SIL.LCModel.Tests.dll`

   (Or specify one of the other `SIL.LCModel*Tests.dll`)
5. To debug the tests from Visual Studio: Immediately after the tests have started
   running _"Attach to Process..."_ and select `nunit-agent.exe`.
