@echo off

SET msbuild=C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe

if exist helicon\bin\Release (rmdir /s /q helicon\bin\Release)
%msbuild% -m -t:Build -property:Configuration=Release helicon\helicon.csproj
if errorlevel 1 goto :failed_helicon

if exist servx\bin\Release (rmdir /s /q servx\bin\Release)
%msbuild% -m -t:Build -property:Configuration=Release servx\servx.csproj
if errorlevel 1 goto :failed_servx

::if exist dist (rmdir /s /q dist)
if not exist dit (mkdir dist)

copy /y helicon\bin\Release\*.* dist
copy /y servx\bin\Release\*.* dist

del dist\*.txt
del dist\*.xml
del dist\*.pdb

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
