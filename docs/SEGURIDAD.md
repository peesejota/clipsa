# Revisión de la alerta de Kaspersky

Actualización del 14/10/2026: el usuario ha solicitado reconstruir instalador y portable con los atajos Impr Pant. Se usan directamente los compiladores .NET e Inno Setup existentes, sin restaurar los scripts bloqueados, modificar el antivirus ni ejecutar pruebas automáticas de instalación o del registro. La alerta histórica continúa pendiente; un nuevo análisis de archivos no certifica el comportamiento. Los logs e informes de pruebas con rutas personales no se incluyen en los paquetes nuevos.

Fecha: 12 de octubre de 2026. Esta revisión no certifica ausencia de malware ni declara resuelta la alerta de comportamiento.

## Evento comunicado

- Componente: System Watcher.
- Detección: `PDM:Trojan.Win32.Generic`, objeto de tipo proceso.
- Objeto indicado: `tools/Build-Release.ps1`.
- MD5 comunicado por el usuario: `318EB0D1F8CA2DE4898B3A3C8ACF3A15`.
- Acciones comunicadas: «Objeto eliminado» y «No se puede eliminar».

Al revisar el disco, `Build-Release.ps1`, `Test-Packages.ps1` y `.github/workflows/build.yml` ya no existían. No se han restaurado desde cuarentena, recreado, renombrado ni ejecutado. La ausencia de varios archivos es compatible con una eliminación o reversión, pero no demuestra qué acción concreta tomó Kaspersky.

No es posible cotejar el MD5 ni analizar de nuevo el archivo exacto señalado porque ya no está disponible. La revisión del código previamente generado permite identificar las operaciones previstas: compilar con el SDK local, ejecutar pruebas gráficas, publicar el runtime, construir el ZIP e invocar Inno Setup. Las pruebas separadas de paquetes instalaban/desinstalaban copias aisladas y cambiaban temporalmente el valor `Clipsa` del inicio automático para comprobar su limpieza. Son operaciones que merecen revisión en una alerta de comportamiento; no se afirma que sean la causa demostrada.

[System Watcher analiza actividad y puede revertir cambios de archivos y registro](https://support.kaspersky.com/help/Kaspersky/Win21.7/en-US/68174.htm). Una detección de este componente no se descarta porque un análisis posterior de archivos no encuentre amenazas.

## Comprobaciones realizadas

- SHA256 del instalador, ZIP, metadatos y plantilla: todos coinciden con los hashes de la entrega anterior. No se han recompilado ni alterado los paquetes para evitar la detección.
- Revisión estática del código disponible: las capturas y ajustes son locales; el inicio automático se configura desde Ajustes; las actualizaciones consultan un feed HTTPS y abren el navegador, sin instalar código descargado automáticamente. No se ha identificado ofuscación, comandos codificados ni un envío de capturas a un servidor en el código revisado. No equivale a una auditoría exhaustiva de dependencias o del comportamiento de los binarios.
- Análisis con el Kaspersky 21.26 instalado, bases del 12/10/2026 20:18 según la herramienta: instalador, ZIP y carpetas `Clipsa`, `packaging`, `verification` y `tools`. Resultado: 3.618 objetos, 3.618 correctos, cero detecciones, sospechas, omisiones o errores.
- Firmas Authenticode válidas de `dotnet.exe` (Microsoft), `ISCC.exe` y el instalador de Inno Setup (Pyrsys). El SHA512 del ZIP del SDK 10.0.401 coincide con los [metadatos oficiales de Microsoft](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json). Esto verifica origen e integridad, no descarta una alerta de comportamiento.
- Se solicitó modo «sólo informar» (`/i0`), pero la herramienta avisó de que lo ignoraba por el modo no interactivo. No se modificó la configuración del antivirus; no hubo detecciones en esa tarea. Informe completo: [verificacion/kaspersky-scan.txt](verificacion/kaspersky-scan.txt).
- No se desactivó protección ni se añadieron exclusiones. No se ejecutó Clipsa ni el instalador durante esta revisión.

## Limpieza

Sólo queda `releases/Clipsa-0.4.0`, con el instalador, ZIP, hashes, metadatos y plantilla de actualizaciones. Las versiones incompletas, duplicados, carpetas QA y `work` se enviaron a la Papelera. Los informes de pruebas anteriores se conservaron en [verificacion/paquetes-final.json](verificacion/paquetes-final.json) y [verificacion/paquetes-candidato.json](verificacion/paquetes-candidato.json). No se borraron las capturas ni ajustes del usuario.

## Pendiente antes de cerrar la incidencia

Conservar el informe detallado de la detección en Kaspersky y pedir al fabricante que revise el evento y la muestra exacta de cuarentena. [Kaspersky OpenTIP](https://opentip.kaspersky.com/) es su portal de análisis; no se han subido archivos ni código del proyecto a servicios externos en esta revisión. Una muestra sólo debe remitirse conscientemente, comprobando que no contiene información privada.

No restaurar ni volver a ejecutar los scripts bloqueados mientras no se aclare la detección. El análisis estático limpio no garantiza que System Watcher no vuelva a detectar actividad al ejecutar la aplicación o instalarla. La preparación automatizada de nuevas releases está suspendida: la documentación de construcción anterior describe un flujo cuyos scripts ya no están presentes. No se declara esta beta libre de alertas ni se recomienda ignorar el aviso al compartirla.
