# Installing the Puma Compiler

## Deployment model

Puma is published as a framework-dependent `win-x64` application. The installation contains the compiler executable, `Puma.dll`, and the `.deps.json` and `.runtimeconfig.json` files required by the .NET host. It does not bundle the .NET runtime.

Install the .NET 10 runtime for Windows x64 before running the compiler.

## Publish and install

From the repository root, run the Release publish-and-install target:

	dotnet msbuild Puma.csproj -t:PublishAndInstallPumaCompiler -p:Configuration=Release

The target publishes the compiler and copies the complete publish output to `%USERPROFILE%\Puma`, preserving separately installed Puma runtime headers and libraries. To use a different destination, set `PumaInstallDirectory`:

	dotnet msbuild Puma.csproj -t:PublishAndInstallPumaCompiler -p:Configuration=Release -p:PumaInstallDirectory=C:\Tools\Puma

To compile Puma programs that use standard libraries, set `PUMA_STDLIB_ROOT` to the installed root containing `include\` and `lib\x64\Release\`. This root applies to all Puma standard libraries, including PumaType, PumaConsole, and PumaFile, and may be separate from the compiler source project:

	$env:PUMA_STDLIB_ROOT = 'C:\Users\dabur\Puma'

For a persistent user setting, use `[Environment]::SetEnvironmentVariable('PUMA_STDLIB_ROOT', 'C:\Users\dabur\Puma', 'User')` and restart Visual Studio or open a new terminal so child compiler and test processes inherit it.

When the variable is unset or blank, the compiler searches beside its executable for `include\` and `lib\x64\Release\`. An invalid configured root reports an error rather than silently selecting another installation. `PUMA_HOME` and the standard-library source-tree layout are no longer used for runtime discovery. Native builds link the Release libraries.

## Verify the installation

Open a new PowerShell session and run:

	& "$env:USERPROFILE\Puma\Puma.exe" --version

The command should print the Puma compiler version and return exit code `0`.