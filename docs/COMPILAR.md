# Compilar y modificar Clipsa

Desde 0.8.0 la obra combinada se distribuye bajo GPL versión 3. Puedes obtener y modificar el código fuente sin pagar ni obtener una clave privada. Conserva los avisos de terceros. Las releases públicas incluyen el enlace al ZIP de código fuente del mismo tag que los binarios.

1. Instala el SDK .NET 10.0.404 (global.json) en Windows x64 y clona el repositorio o extrae el ZIP del tag de la release.
2. Ejecuta `dotnet build Clipsa/Clipsa.csproj -c Release`.
3. Ejecuta la aplicación con `dotnet run --project Clipsa/Clipsa.csproj -c Release`.
4. Las pruebas puras del motor se ejecutan con `dotnet run --project verification/Clipsa.Checks.csproj -c Release -- --sharex-only`. Las pruebas interactivas requieren un escritorio Windows disponible; no deben ejecutarse mientras manipulas datos reales en otra aplicación.

Para un payload autocontenido: `dotnet publish Clipsa/Clipsa.csproj -c Release -r win-x64 --self-contained true -o staging/installed`. Se incluye .NET/WPF 10.0.15. Para distribuir, incluye LICENSE, THIRD-PARTY-NOTICES.md, docs y licenses; el workflow `.github/workflows/build.yml` prepara el payload, reúne los avisos de los runtime packs de NuGet, genera el inventario de archivos, crea el ZIP portable y compila el instalador con Inno Setup. El código de Setup está en packaging/Clipsa.iss. El marcador portable.mode y la carpeta data se describen en USO.md.

La clave privada del canal oficial sólo firma las releases oficiales: no se necesita para compilar, modificar o ejecutar Clipsa. Si publicas tu propio canal, genera tu propia clave con release-tools, incorpora su clave pública en UpdateTrust.cs y configura el repositorio/manifiesto correspondiente; no intentes firmar como el canal de otra persona. Véase ACTUALIZACIONES.md.

El motor adaptado se encuentra en Clipsa/Capture y Clipsa/ScrollDriver.cs. Su versión de referencia, archivos de origen y modificaciones figuran en licenses/ShareX-PROVENANCE.md. El código WPF no depende de instalar ShareX, Skia ni Avalonia.
