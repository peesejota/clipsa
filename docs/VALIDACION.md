# Validación de Clipsa

## Revisión 0.5.0 · 12 de noviembre de 2026

SDK 10.0.404 y runtime objetivo .NET/WPF 10.0.15. Pruebas superadas: firma RSA-PSS con clave pública fijada, selección instalada/portable, rechazo de manifiestos alterados, SHA256 incorrecto y rutas de ZIP peligrosas, conservación de datos/exportaciones, restauración de archivos al fallar una actualización portable, pertenencia del inicio automático y rutas relativas portables. El material firmado de prueba usa versión 0.0.0 y dominio reservado `.invalid`; no es una actualización ejecutable.

También pasaron las pruebas completas existentes de ajustes atómicos y recuperación, transparencia independiente, texto/trazos/goma/recorte/rotación y deshacer/rehacer, selección de recorte con ratón real y Ctrl+Z/Ctrl+Y, arrastre de miniaturas a receptor externo, icono en color y scroll manual. Se corrigieron la sincronización de pintado/DPI y la espera asíncrona del arnés antiguo de scroll. Las pruebas específicas de texto, fondo blanco, desplazamiento continuo y recuperación de tramos siguen pasando.

Los informes históricos siguientes no se presentan como pruebas nuevas de esta versión. Siguen pendientes un PC limpio y varios monitores físicos/DPI mixto; las limitaciones externas de certificado Authenticode y revisión de Kaspersky no desaparecen por firmar los manifiestos.

Los paquetes finales proceden del [run 43273944990 de GitHub Actions](https://github.com/peesejota/clipsa/actions/runs/43273944990), commit `16d85ed43a94f2d5ea84e38cf00cf8cbce8d62d4`. Pasaron instalación, carga WPF/runtime incluido, reinstalación y desinstalación conservando datos en el runner aislado; también la portable extraída en una ruta con espacios, sin usar un runtime externo. Ambas incluyen .NET/WPF 10.0.15.

Prueba local adicional: actualización completa de una copia portable de QA con versión 0.4.99 a los binarios finales 0.5.0, usando un manifiesto firmado de la Release real. Se comprobaron firma y hash, arranque del proceso auxiliar, sustitución con respaldo, reinicio de la aplicación nueva, conservación de ajustes y una exportación, carpeta `data` original y cierre normal. La prueba no alteró la instalación ni los datos del usuario.

Análisis de los paquetes finales con Kaspersky 21.27, bases del 12 de noviembre de 2026: 1.004 objetos analizados, cero detecciones, sospechas, omisiones, archivos corruptos o errores. El informe permanece local en `docs/verificacion/kaspersky-0.5.0-scan.txt`; no se publican logs privados. Un análisis de archivos sin detecciones no resuelve la alerta histórica de comportamiento.

La [Release pública 0.5.0 beta](https://github.com/peesejota/clipsa/releases/tag/v0.5.0-beta.1) tiene instalador, ZIP, manifiestos firmados, metadatos e inventario SHA256. Se verificaron las seis huellas contra los digests de GitHub y el acceso público a ambas descargas. El canal beta apunta al manifiesto firmado publicado.

## Revisión 0.4.3 · 14 de octubre de 2026

El arnés añadió texto renderizado con márgenes blancos y barra lateral móvil, además de una página HTML real servida sólo en localhost y abierta en un perfil temporal aislado de Edge. La prueba detectó una altura errónea con el reconocimiento anterior. Tras verificar el contenido en vez de diluir sus errores con fondo blanco, pasó la prueba de 15 desplazamientos continuos: salida 902 × 1081 px y desplazamiento comunicado por la página de 456 CSS px. Se mantienen las pruebas de contenido aleatorio, cabeceras/pies fijos, inversión de scroll, zonas ambiguas y conservación de tramos tras un salto rechazado.

Este test desplaza la página mediante su propia API de scroll y verifica la captura de pantalla resultante; no certifica físicamente todos los ratones/trackpads ni todas las webs. No se usó el perfil del usuario. La miniatura es una vista de lo aceptado, no una garantía de reconstrucción de zonas omitidas por saltos bruscos.

La prueba final también pasó en Chrome: 902 × 1081 px y 456 CSS px de recorrido. Varias repeticiones con Edge no pudieron abrir la ventana del perfil temporal y se detuvieron antes de capturar; no se declaran superadas esas repeticiones. La carga del payload instalado y de la portable extraída pasó con .NET 10.0.12 y versión 0.4.3.0. Kaspersky analizó ambos paquetes: 995 objetos, cero detecciones, sospechas, omisiones o errores; [informe local](verificacion/kaspersky-0.4.3-scan.txt). La alerta histórica de comportamiento sigue pendiente.

## Revisión 0.4.2 · 14 de octubre de 2026

Pruebas de scroll superadas: primer tramo disponible desde Loaded antes de comenzar a desplazar, 15 desplazamientos continuos cada 90 ms sin pausas largas, resultado 250 × 525 px y primeras 30 filas idénticas al inicio. Se probó también que una muestra sin solapamiento no sustituye la referencia aceptada, que se puede retomar después y que volver hacia arriba no pierde ni duplica lo anterior. Las pruebas de cabeceras/pies fijos y contenido ajeno/repetitivo se mantienen. No es una validación de todas las webs, animaciones o teclados físicos.

Paquetes 0.4.2 autocontenidos comprobados mediante carga del payload instalado y portable extraída, con .NET 10.0.12 y versión 0.4.2.0. Análisis de Kaspersky: 995 objetos, cero detecciones, sospechas, omisiones o errores. [Informe local](verificacion/kaspersky-0.4.2-scan.txt). No se repitió la instalación/desinstalación automatizada ni se declara resuelta la alerta histórica de comportamiento.

## Revisión 0.4.1 · 14 de octubre de 2026

Compilación y publicación autocontenida x64 realizadas. Pruebas específicas superadas: Impr Pant sola y + 1/2/3 mediante el listener nativo con pulsaciones inyectadas, asignación desde el selector real de Ajustes, botón de presets, números del teclado numérico, ausencia de repetición y de interferencia con la escritura, duplicados y compatibilidad JSON con ajustes anteriores. También se repitieron las pruebas de resolución GitHub y redirecciones HTTPS. No se ha probado físicamente la tecla Fn del portátil ni que otro software de teclado no intercepte antes los gestos.

Los informes de instalación de abajo corresponden a 0.4.0. En 0.4.1 no se repite la prueba automatizada de instalación/desinstalación que cambiaba el inicio automático: los scripts bloqueados siguen sin restaurarse. Se comprueba la carga de los paquetes con `--self-test`, sin instalar ni alterar los ajustes reales del usuario. La aplicación que estaba abierta se conserva y debe cerrarse desde la bandeja antes de actualizar.

Los resultados históricos siguientes se mantienen como tales.

Resultado final 0.4.1: comprobación de carga en los payloads instalado y portable y en el ZIP extraído, todos con .NET 10.0.12 incluido y versión 0.4.1.0. Autoría de EXE/instalador y licencia comprobadas; aviso original conservado. No hay logs ni informes privados dentro de los paquetes. Análisis posterior de Kaspersky: 996 objetos correctos, cero detecciones, sospechas, omisiones o errores. Informe local: [verificacion/kaspersky-0.4.1-scan.txt](verificacion/kaspersky-0.4.1-scan.txt). La alerta de comportamiento histórica sigue pendiente.

Fecha: 12 de octubre de 2026. Equipo: Windows 11 IoT Enterprise LTSC x64, build 26100, escritorio interactivo con escala 125 %. SDK 10.0.401, runtime .NET/WPF 10.0.12 e Inno Setup 6.7.3. Proceso de instalación ejecutado sin elevación.

## Pruebas superadas

- Compilación y publicación autocontenida x64. Carga del runtime incluido, recursos WPF, icono en color de nueve resoluciones y geometría de recorte desde los paquetes.
- Opacidad del fondo a 0, 68 y 100 % de transparencia; los píxeles de las capturas permanecen completamente opacos.
- Ajustes atómicos, copia de respaldo ante JSON dañado o `null`, validación de atajos duplicados, desactivados y ocupados por Windows.
- Recuperación de sesiones abandonadas, protección de sesiones activas y limpieza limitada a la sesión propia.
- Historial de texto, trazos, borrado parcial, selección y aplicación de recortes, rotación y nuevas ramas de edición.
- Selección de recorte con ratón real, Ctrl+Z/Ctrl+Y y deshacer el recorte aplicado. Recorte libre y proporcional a resolución nativa, sin líneas de selección en el resultado.
- Arrastre real desde las miniaturas compactas a un receptor externo; archivo original conservado. Entrega de PNG a Edge y Chrome mediante perfiles temporales aislados.
- Scroll sintético: solapamiento, cabeceras y pies fijos, desplazamiento inverso y rechazo de contenido ajeno o repetitivo ambiguo. Sesión real sobre una ventana controlada con tres desplazamientos; resultado de 250 × 375 px.
- Portable extraída en una ruta con espacios y ejecutada con DOTNET_ROOT apuntando a una ruta inexistente. Esto comprueba el runtime empaquetado, no sustituye la prueba en un PC limpio.
- Segunda ejecución activa la primera instancia y termina; cierre normal correcto.
- Instalador con identidad de prueba aislada: instalación por usuario, inicio del programa, reinstalación conservando ajustes, desinstalación conservando datos y eliminación del inicio automático sólo si pertenece a esa instalación. El inicio automático de otra copia se conserva y el valor original del usuario se restaura tras la prueba.

Arnés: `verification/Clipsa.Checks.csproj`. Las pruebas de paquetes se ejecutaron con `tools/Test-Packages.ps1`, ya no disponible tras la alerta comunicada de Kaspersky. El informe final se ha conservado en [verificacion/paquetes-final.json](verificacion/paquetes-final.json) y registra explícitamente `elevated=false`, `passed=true` y `cleanMachine=false`. Las carpetas QA de releases se enviaron a la Papelera. Las pruebas gráficas se ejecutaron por separado; el empaquetado final usó `-SkipTests` para no repetir la interacción con el escritorio durante la publicación.

## Pendientes registrados en 0.4.0 (histórico)

Esta sección describe el estado anterior. La publicación, el canal y las pruebas nuevas de 0.5.0 se detallan al principio del documento.

Cambios posteriores a los paquetes 0.4.0: compilación de la autoría Francisco Serrano Jiménez y pruebas sin red/interfaz de resolución de repositorios GitHub, rechazo de credenciales, feeds HTTPS y redirecciones seguras superadas. La licencia original se comparó con `tendedero-main/LICENSE` y conserva exactamente su texto. Los avisos de actualización aún necesitan una prueba de extremo a extremo con una Release real, y estos cambios no están en los paquetes existentes.

- Otro PC limpio o una máquina virtual sin SDK/runtime preinstalados. No había Windows Sandbox disponible en este equipo.
- Varios monitores físicos, DPI mixto, conectar/desconectar pantallas, escritorio remoto y accesibilidad completa.
- Compatibilidad con Windows 10, ARM64, HDR, contenido protegido y escenarios prolongados con muchas capturas.
- Firma digital: no se dispone de un certificado de editor. El instalador y el ejecutable de esta beta no están firmados.
- Publicación y consulta de una actualización alojada: falta una URL HTTPS propia. La plantilla generada no es un feed público activo.
- El workflow de GitHub no se ejecutó en remoto y ya no está disponible en disco.
- La detección de comportamiento comunicada de Kaspersky sigue sin aclararse: consulta [SEGURIDAD.md](SEGURIDAD.md). El análisis posterior de archivos no ha encontrado amenazas, pero no certifica el comportamiento en ejecución.

La publicación actual es beta, no una certificación de estabilidad ni de ausencia de alertas. Se mantienen las pruebas externas pendientes y el seguimiento de Kaspersky descritos al principio y en [SEGURIDAD.md](SEGURIDAD.md).
