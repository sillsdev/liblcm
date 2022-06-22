 @ECHO OFF

if "%1"=="" (
    SET CONFIG=Debug
) else (
    SET CONFIG=%1
)

if "%2"=="" (
    SET TARGET=Build
) else (
    SET TARGET=%2
)

if not "%3"=="" (
	echo Usage: "build [(Debug|Release) [<target>]]"
	exit /b 1
)

msbuild /t:Restore /p:Configuration=%CONFIG% /p:Platform="Any CPU" LCM.sln
msbuild /t:%TARGET% /p:Configuration=%CONFIG% /p:Platform="Any CPU" LCM.sln