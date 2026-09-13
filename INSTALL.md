# Installing the Puma Compiler

## Deployment model

Puma is published as a framework-dependent `win-x64` application. The installation contains the compiler executable, `Puma.dll`, and the `.deps.json` and `.runtimeconfig.json` files required by the .NET host. It does not bundle the .NET runtime.

Install the .NET 10 runtime for Windows x64 before running the compiler.

## Publish and install

From the repository root, run the Release publish-and-install target:

	dotnet msbuild Puma.csproj -t:PublishAndInstallPumaCompiler -p:Configuration=Release

The target publishes the compiler and replaces the contents of `%USERPROFILE%\Puma` with the complete publish output. To use a different destination, set `PumaInstallDirectory`:

	dotnet msbuild Puma.csproj -t:PublishAndInstallPumaCompiler -p:Configuration=Release -p:PumaInstallDirectory=C:\Tools\Puma

## Verify the installation

Open a new PowerShell session and run:

	& "$env:USERPROFILE\Puma\Puma.exe" --version

The command should print the Puma compiler version and return exit code `0`.