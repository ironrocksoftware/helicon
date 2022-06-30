@echo off

SET msbuild=C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe

if exist helicon\bin\Debug (rmdir /s /q helicon\bin\Debug)
%msbuild% -m -t:Build -property:Configuration=Debug helicon\helicon.csproj
if errorlevel 1 goto :failed_helicon

if exist servx\bin\Debug (rmdir /s /q servx\bin\Debug)
%msbuild% -m -t:Build -property:Configuration=Debug servx\servx.csproj
if errorlevel 1 goto :failed_servx

if exist dist (rmdir /s /q dist)
if not exist dist (mkdir dist)

copy /y helicon\bin\Debug\*.* dist
copy /y servx\bin\Debug\*.* dist

del dist\*.txt
del dist\*.xml

echo *******************
echo Success!
echo *******************
goto :eof

:failed_helicon
echo *******************
echo ERROR: Unable to build helicon project.
echo *******************
goto :eof

:failed_servx
echo *******************
echo ERROR: Unable to build servx project.
echo *******************
goto :eof
