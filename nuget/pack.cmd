dotnet restore ..\src\SignalW
dotnet pack ..\src\SignalW -c RELEASE -o ..\artifacts

dotnet restore ..\src\SignalW.Client
dotnet pack ..\src\SignalW.Client -c RELEASE -o ..\artifacts

pause