$msbuild = & 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -version '[16.0,17.0)' -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
& $msbuild (Join-Path $PSScriptRoot 'src\SSMSTabManager.csproj') -restore -p:Configuration=Release -v:minimal -nologo
