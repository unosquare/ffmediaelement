@echo off
SET enableextensions
SET PackagePath="%UserProfile%\Desktop\ffme.windows-3.4.230\"
SET ProjectPath="C:\projects\ffmediaelement\"
SET ReleasePath="%ProjectPath%Unosquare.FFME.Windows.Sample\bin\Release\"

md "%PackagePath%lib\net461"
copy "%ReleasePath%ffme.common.dll" "%PackagePath%lib\net461\"
copy "%ReleasePath%ffme.common.xml" "%PackagePath%lib\net461\"
copy "%ReleasePath%ffme.win.dll" "%PackagePath%lib\net461\"
copy "%ReleasePath%ffme.win.xml" "%PackagePath%lib\net461\"

copy "%ProjectPath%Support\readme.txt" "%PackagePath%"
copy "%ProjectPath%Support\ffme.win.nuspec" "%PackagePath%"

cd "%PackagePath%"
echo choco install nuget.commandline
nuget pack ffme.win.nuspec
