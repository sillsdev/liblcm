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

msbuild /t:%TARGET% /p:Configuration=%CONFIG% /p:Platform=x86 /p:UseLocalFiles=%FILESAVAILABLE% LCM.sln