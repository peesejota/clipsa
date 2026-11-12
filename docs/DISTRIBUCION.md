# Construcción y distribución

Estado del repositorio (12/11/2026): la vía vigente para compilar desde cero es `.github/workflows/build.yml`, con pruebas no interactivas, empaquetado autocontenido y publicación manual de borradores. Consulta [GITHUB.md](GITHUB.md). Las instrucciones de scripts locales que siguen son históricas; no restaure los scripts bloqueados para seguirlas. El nuevo workflow no ejecuta esos archivos ni desactiva antivirus.

Actualización 0.4.1 (14/10/2026): a petición del usuario se han regenerado instalador y portable usando directamente `dotnet publish` e Inno Setup, sin restaurar los scripts bloqueados ni ejecutar instalación/desinstalación automática. La entrega está en `releases/Clipsa-0.4.1`. Se comprobaron los atajos, la carga autocontenida y la portable extraída; Kaspersky analizó 996 objetos con cero detecciones. La alerta histórica sigue pendiente.

**Aviso de seguridad:** `tools/Build-Release.ps1`, `tools/Test-Packages.ps1` y el workflow de GitHub ya no están en disco tras la alerta comunicada de Kaspersky. El flujo siguiente es documentación histórica, no instrucciones ejecutables en el estado actual. No se han restaurado ni sustituido los scripts bloqueados. Preparar nuevas releases queda suspendido hasta aclarar la detección; consulta [SEGURIDAD.md](SEGURIDAD.md).

Clipsa 0.4.0 es una beta autocontenida para Windows 11 x64. No es necesario instalar .NET para usarla. La portable es una carpeta completa dentro de un ZIP; no copies sólo el EXE.

## Construir

En Windows, con SDK .NET 10.0.401 e Inno Setup 6.7.3, ejecuta:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Release.ps1 -DotnetPath "C:\ruta\dotnet.exe" -InnoPath "C:\ruta\ISCC.exe"
```

El script no descarga herramientas. En este workspace existen copias locales en `.build-tools`. La salida es `releases/0.4.0`; si ya existe, usa `-OutputDirectory` con una carpeta nueva. El SDK se fija en `global.json`; la versión y el hash del compilador quedan registrados.

Se ejecuta el arnés WPF de `verification` y se publica con runtime incluido, sin recortar ensamblados WPF. `-SkipTests` sólo sirve cuando las pruebas se realizan por separado y sus resultados se documentan. Las pruebas gráficas necesitan un escritorio desbloqueado.

Para comprobar el arrastre a Edge y Chrome con perfiles temporales aislados, configura `CLIPSA_BROWSER_QA=only` y ejecuta el arnés.

Se generan el ZIP, el instalador, `release.json`, el manifiesto de actualizaciones y `SHA256SUMS.txt`. No se empaquetan ajustes ni capturas reales del desarrollador.

## Firma

Configura `CLIPSA_SIGN_CERT_THUMBPRINT` con la huella de un certificado de firma de código accesible en el almacén de Windows y `CLIPSA_SIGNTOOL_PATH` con el SignTool local. `CLIPSA_TIMESTAMP_URL` debe apuntar al servicio HTTPS de sellado de tiempo del proveedor. No incluyas certificados, claves o tokens en el repositorio.

Se firma el lanzador y se configuran firma y verificación del instalador y su desinstalador. Sin certificado se genera una beta sin firma: el proceso no simula una identidad de editor.

## Publicar y actualizar

El script prepara archivos locales: no publica ni crea repositorios. Cuando exista una URL propia, añade `-DownloadBaseUrl https://tu-web-o-releases/...` y, opcionalmente, `-ReleaseNotesUrl`.

Esto genera `update-manifest.json`. Sin URL sólo se genera `update-manifest.template.json`, que NO es un feed válido. El manifiesto incluye `version`, `downloadUrl` HTTPS y `sha256`. Configura su URL HTTPS en Ajustes de Clipsa. Consultar abre la descarga en el navegador; instalar es manual y requiere cerrar la sesión de capturas.

Para actualizar la instalada, ejecuta el nuevo Setup: conserva `%APPDATA%\Clipsa`. Para la portable, cierra Clipsa, conserva `data/` y sustituye los archivos de programa por los de la nueva carpeta. No mezcles runtimes.

## Validación

Consulta `VALIDACION.md` para resultados obtenidos y `PRUEBAS-MANUALES.md` para comprobaciones en otros equipos. La firma y la publicación pública requieren identidad y destino reales. No se considera una máquina limpia un proceso que sólo cambia DOTNET_ROOT.

Ejecuta `tools/Test-Packages.ps1 -ReleaseDirectory "C:\ruta\release"` para probar ambos paquetes, instancia única e instalación/reinstalación/desinstalación con identidad aislada. No necesita administrador; evita hacerlo mientras otra copia de Clipsa esté abierta. Guarda un informe JSON en una carpeta QA nueva.

El workflow manual `.github/workflows/build.yml` compila y adjunta paquetes como artefactos privados de Actions, sin crear una release pública. Usa el Inno Setup disponible en el runner, cuyo hash se registra. No ejecuta las pruebas de ratón en un runner sin escritorio; sí comprueba la carga del paquete con `--self-test`. Referencias de las acciones: [checkout](https://github.com/actions/checkout), [setup-dotnet](https://github.com/actions/setup-dotnet) y [upload-artifact](https://github.com/actions/upload-artifact).
