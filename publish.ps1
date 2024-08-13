$ErrorActionPreference = 'Stop';

if ($isWindows) {
  Write-Host Publish Windows executables
  dotnet publish -c Release ./AmigaAdf/AmigaAdf.csproj -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -r win-x64 --no-restore --nologo --self-contained
  dotnet publish -c Release ./AmigaAdf/AmigaAdf.csproj -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -r win-x86 --no-restore --nologo --self-contained
  Write-Host Pack zips for Windows
  7z a AmigaTools-Windows.zip "./AmigaAdf/bin/Any CPU/Release/net7.0/win-x64/publish/AmigaAdf.exe" "./x64/api-ms-win-core-winrt-l1-1-0.dll" -mx9
  7z a AmigaTools-Windows.zip "./AmigaAdf/bin/Any CPU/Release/net7.0/win-x86/publish/AmigaAdf.exe" "./x86/api-ms-win-core-winrt-l1-1-0.dll" -mx9
} elseif ($isLinux) {
  Write-Host Publish Linux executable
  dotnet publish -c Release ./AmigaAdf/AmigaAdf.csproj -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -r linux-x64 --no-restore --nologo --self-contained
  Write-Host Pack tar for Linux
  7z a AmigaTools-Linux.tar "./AmigaAdf/bin/Any CPU/Release/net7.0/linux-x64/publish/AmigaAdf"
  7z a AmigaTools-Linux.tar.gz AmigaTools-Linux.tar -mx9
  rm AmigaTools-Linux.tar
} else {
  Write-Host Publish Mac executables
  dotnet publish -c Release ./AmigaAdf/AmigaAdf.csproj -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -r osx-x64 --no-restore --nologo --self-contained
  dotnet publish -c Release ./AmigaAdf/AmigaAdf.csproj -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -r osx-arm64 --no-restore --nologo --self-contained
  Write-Host Pack zips for Mac
  sudo xcode-select -s /Applications/Xcode-13.2.1.app
  Start-Process -FilePath codesign -Wait -WorkingDirectory . -ArgumentList '-s','-','--force','--verbose','--deep','--no-strict','"./AmigaAdf/bin/Any CPU/Release/net7.0/osx-x64/publish/AmigaAdf"'
  7z a AmigaAdf-Mac.zip "./AmigaAdf/bin/Any CPU/Release/net7.0/osx-x64/publish/AmigaAdf" -mx9
  Start-Process -FilePath codesign -Wait -WorkingDirectory . -ArgumentList '-s','-','--force','--verbose','--deep','--no-strict','"./AmigaAdf/bin/Any CPU/Release/net7.0/osx-arm64/publish/AmigaAdf"'
  7z a AmigaAdf-Mac.zip "./AmigaAdf/bin/Any CPU/Release/net7.0/osx-arm64/publish/AmigaAdf" -mx9
}
