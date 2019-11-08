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

if "%3"=="" (
	SET FILESAVAILABLE=False
) else (
	SET FILESAVAILABLE=%3
)

if "%4"=="" (
	SET PLATFORM=x86
) else (
	SET PLATFORM=%4
)

msbuild /t:%TARGET% /p:Configuration=%CONFIG% /p:Platform=%PLATFORM% /p:UseLocalFiles=%FILESAVAILABLE% LCM.sln