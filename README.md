LCModel Library
===============

Description
-----------

The library for the SIL Language and Culture Model.
The liblcm library is the core FieldWorks model for linguistic analyses of languages. Tools in this library provide the ability to store and interact with language and culture data, including anthropological, text corpus, and linguistics data.

Instructions
------------

1. Install Required Software
- git
- Visual Studio 2015 or MonoDevelop

2. Clone the liblcm repository
- Open a terminal (or git bash Windows) and cd into a desired directory.
- Run `git clone https://github.com/sillsdev/liblcm.git`

3. Build liblcm
cd into the directory of the cloned liblcm repository.

**On Windows**
- Run `build.cmd` to build the liblcm library.

**On Linux**
- Run `build.sh` to build the liblcm library.

By default, this will build liblcm in the Debug configuration.
To build with a different configuration, use `build.(cmd|sh) (Debug|Release)`