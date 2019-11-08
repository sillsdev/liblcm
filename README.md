LCModel Library
===============

Description
-----------

The library for the SIL Language and Culture Model.
The liblcm library is the core [FieldWorks](github.com/sillsdev/FieldWorks) model for linguistic analyses of languages. Tools in this library provide the ability to store and interact with language and culture data, including anthropological, text corpus, and linguistics data.

Instructions
------------

1. Install Required Software
- git
- Visual Studio 2015 or MonoDevelop

2. Clone the liblcm repository
- Open a terminal (or git bash on Windows) and cd into a desired directory.
- Run `git clone https://github.com/sillsdev/liblcm.git`

3. Build liblcm
- cd into the directory of the cloned liblcm repository.

**On Windows**
- Run the appropriate `vsvars*.bat`. Alternatively, `LCM.sln` can be built from within Visual Studio.
- Run `build.cmd` to build the liblcm library.

**On Linux**
- Run `build.sh` to build the liblcm library.

By default, this will build liblcm in the Debug configuration.
To build with a different configuration, use:

    build.(cmd|sh) (Debug|Release)

Debugging
---------

The LCModel library depends on multiple libpalaso files that are downloaded automatically by triggering the build script. The option to build liblcm using locally built dependencies is also available to assist with debugging. Copy all of the relevent files from the libpalaso output folder into the lib/downloads folder in liblcm, then build with the command:

    build.(cmd|sh) Debug Build True

Build a 64-bit build with the command:

    build.(cmd|sh) Debug Build False x64

Tests
-----

Linux

    (. environ && cd artifacts/Debug/ && ICU_DATA="IcuData/" nunit-console SIL.LCModel*Tests.dll )
