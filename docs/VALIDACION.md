# Validación de Clipsa 0.4.0

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

## Lo que no se ha validado todavía

Cambios posteriores a los paquetes 0.4.0: compilación de la autoría Francisco Serrano Jiménez y pruebas sin red/interfaz de resolución de repositorios GitHub, rechazo de credenciales, feeds HTTPS y redirecciones seguras superadas. La licencia original se comparó con `tendedero-main/LICENSE` y conserva exactamente su texto. Los avisos de actualización aún necesitan una prueba de extremo a extremo con una Release real, y estos cambios no están en los paquetes existentes.

- Otro PC limpio o una máquina virtual sin SDK/runtime preinstalados. No había Windows Sandbox disponible en este equipo.
- Varios monitores físicos, DPI mixto, conectar/desconectar pantallas, escritorio remoto y accesibilidad completa.
- Compatibilidad con Windows 10, ARM64, HDR, contenido protegido y escenarios prolongados con muchas capturas.
- Firma digital: no se dispone de un certificado de editor. El instalador y el ejecutable de esta beta no están firmados.
- Publicación y consulta de una actualización alojada: falta una URL HTTPS propia. La plantilla generada no es un feed público activo.
- El workflow de GitHub no se ejecutó en remoto y ya no está disponible en disco.
- La detección de comportamiento comunicada de Kaspersky sigue sin aclararse: consulta [SEGURIDAD.md](SEGURIDAD.md). El análisis posterior de archivos no ha encontrado amenazas, pero no certifica el comportamiento en ejecución.

No se presenta esta versión como estable ni libre de alertas. Antes de distribuirla públicamente, aclarar la detección de Kaspersky. Después, completar [PRUEBAS-MANUALES.md](PRUEBAS-MANUALES.md) en otro equipo y resolver firma y alojamiento.
