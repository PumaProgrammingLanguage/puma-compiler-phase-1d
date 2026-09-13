# Installing the Puma Compiler

## Deployment model

Puma is published as a framework-dependent `win-x64` application. The installation contains the compiler executable, `Puma.dll`, and the `.deps.json` and `.runtimeconfig.json` files required by the .NET host. It does not bundle the .NET runtime.

Install the .NET 10 runtime for Windows x64 before running the compiler.

## Publish and install

From the repository root, run the Release publish-and-install target:

	dotnet msbuild Puma.csproj -t:PublishAndInstallPumaCompiler -p:Configuration=Release

The target publishes the compiler and copies the complete publish output to `%USERPROFILE%\Puma`, preserving separately installed Puma runtime headers and libraries. To use a different destination, set `PumaInstallDirectory`:

	dotnet msbuild Puma.csproj -t:PublishAndInstallPumaCompiler -p:Configuration=Release -p:PumaInstallDirectory=C:\Tools\Puma

To compile Puma programs that use runtime dependencies, the compiler first searches `%USERPROFILE%\Puma`. If its runtime artifacts are unavailable, set `PUMA_HOME` to an installed runtime directory (`include\` and `lib\x64\Release\`) or the PumaStdLib source tree (`PumaType\`, `PumaConsole\`, `PumaFile\`, and `x64\Release\`):

	$env:PUMA_HOME = '<path-to-PumaStdLib>'

## Verify the installation

Open a new PowerShell session and run:

	& "$env:USERPROFILE\Puma\Puma.exe" --version

The command should print the Puma compiler version and return exit code `0`.