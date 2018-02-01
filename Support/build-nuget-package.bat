@echo off
SET enableextensions
SET PackagePath="%UserProfile%\Desktop\ffme.windows-3.4.0.2\"
SET ProjectPath="C:\projects\ffmediaelement\"
SET ReleasePath="%ProjectPath%Unosquare.FFME.Windows.Sample\bin\Release\"

md "%PackagePath%lib\net462"
copy "%ReleasePath%ffme.common.dll" "%PackagePath%lib\net462\"
copy "%ReleasePath%ffme.common.xml" "%PackagePath%lib\net462\"
copy "%ReleasePath%ffme.win.dll" "%PackagePath%lib\net462\"
copy "%ReleasePath%ffme.win.xml" "%PackagePath%lib\net462\"

copy "%ProjectPath%Support\readme.txt" "%PackagePath%"
copy "%ProjectPath%Support\ffme.win.nuspec" "%PackagePath%"

nuget pack ffme.win.nuspec
