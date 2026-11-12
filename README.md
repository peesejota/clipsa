# Clipsa 0.5.2 · beta para Windows

Capturas, edición y scroll manual en una barra flotante de cristal. Autor: **Francisco Serrano Jiménez**.

[Descargas y versiones](https://github.com/peesejota/clipsa/releases) · [Uso](docs/USO.md) · [Actualizaciones](docs/ACTUALIZACIONES.md) · [Privacidad](docs/PRIVACIDAD.md)

**Descargar 0.5.2 beta:** [Instalador para Windows x64](https://github.com/peesejota/clipsa/releases/download/v0.5.2-beta.1/Clipsa-0.5.2-win-x64-setup.exe) · [ZIP portable](https://github.com/peesejota/clipsa/releases/download/v0.5.2-beta.1/Clipsa-0.5.2-win-x64-portable.zip) · [Cambios y archivos de verificación](https://github.com/peesejota/clipsa/releases/tag/v0.5.2-beta.1).

## Instalada o portable

Destino probado: Windows 11 x64. Ambos paquetes incluyen .NET/WPF 10.0.15; no necesitas instalar .NET por separado.

- **Instalador:** `Clipsa-0.5.2-win-x64-setup.exe`. Instalación por usuario sin administrador; ajustes en `%APPDATA%\Clipsa`.
- **Portable:** extrae todo `Clipsa-0.5.2-win-x64-portable.zip` y abre `Clipsa.exe`. Conserva `portable.mode`. Los ajustes, sesiones y actualizaciones están en `data` dentro de la carpeta portable. No basta con copiar el EXE.

Guarda las capturas pendientes y elige **Salir** desde la bandeja antes de sustituir una versión antigua. Las versiones 0.4.x necesitan esta primera instalación manual para recibir el nuevo sistema de actualizaciones.

## Funciones

- Captura de pantalla o región; atajos configurables. Impr Pant + 1/2/3 como valores iniciales, manteniendo la tecla o pulsándola antes del número; se conservan los atajos ya guardados.
- Barra centrada, fondo transparente, modo compacto, reordenación y arrastre a carpetas u otras aplicaciones.
- Edición con lápiz, resaltador, goma, texto, formas, recorte libre/proporcional, rotación y deshacer/rehacer.
- Scroll con rueda, trackpad o barra lateral, miniatura del recorrido y opción de guardar los tramos aceptados si no se puede completar.
- Bandeja, inicio con Windows opcional y recuperación local de sesiones interrumpidas.

El scroll captura lo que aparece en pantalla, no el documento interno del navegador: no puede reconstruir zonas que un salto brusco ha omitido. Contenido animado o repetido puede impedir una unión fiable. [Limitaciones](docs/LIMITACIONES.md).

## Actualizaciones desde GitHub

El canal beta oficial se consulta al iniciar y cada seis horas; puede desactivarse en Ajustes. Cuando hay una versión posterior aparece un aviso y un botón persistente. Puedes **Descargar** sin instalar o elegir **Actualizar ahora**. Antes de cerrar se ofrece guardar las capturas temporales.

La app comprueba una firma RSA-PSS del manifiesto con una clave pública incorporada, selecciona instalador o portable según su modo y verifica el SHA256 del paquete. La portable actualiza los archivos del programa conservando `data` y las capturas exportadas, con copia de respaldo y restauración si falla la sustitución. No ejecuta actualizaciones de fuentes sin firma de confianza.

La firma del manifiesto **no equivale a un certificado Authenticode de editor de Windows**. Esta beta no tiene ese certificado; Windows o el antivirus pueden mostrar advertencias. No desactives protección ni añadas exclusiones. La alerta histórica de Kaspersky sigue documentada en [SEGURIDAD.md](docs/SEGURIDAD.md).

## Desarrollo y publicación

SDK .NET **10.0.404**, fijado en `global.json`. GitHub Actions compila y empaqueta; las publicaciones se firman con la herramienta local de [release-tools](release-tools/Clipsa.ReleaseTools.csproj). La clave privada no está en este repositorio ni en los binarios. [Procedimiento de publicación](docs/ACTUALIZACIONES.md).

```powershell
dotnet run --project verification/Clipsa.Checks.csproj --configuration Release -- --updates-only
```

Las pruebas gráficas necesitan escritorio interactivo; el resultado de CI no sustituye las pruebas en otro PC limpio. [Resultados y alcance](docs/VALIDACION.md).

Clipsa para Windows es una reescritura WPF inspirada en [Tendedero de Alejandro Buján](https://github.com/alejandrobujan/tendedero). Se conserva su aviso original en `licenses/`, sin atribuirle esta aplicación Windows ni usar su nombre o icono. [Licencia](LICENSE) · [Avisos de terceros](THIRD-PARTY-NOTICES.md).
