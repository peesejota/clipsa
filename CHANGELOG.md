# Cambios

## 0.8.0 · beta · 16 de noviembre de 2026

- Sustituye el motor anterior por código adaptado de ShareX 22.4.0: captura GDI nativa compartida por pantalla, región y scroll; unión de filas contra la imagen acumulada, con detección de pies fijos y vistas repetidas.
- Scroll automático tras soltar la selección, con rueda, flecha abajo, Av. página o mensaje de scroll, pausa entre avances y espera de la animación. Se conserva el gesto manual mantenido y la cancelación con Escape/clic derecho.
- Uniones estimadas opcionales, marcadas y confirmadas antes de guardar. Se conserva el resultado anterior si no hay coincidencia; límites de memoria/tamaño y cancelación fuera del hilo de interfaz.
- Elimina ScrollStitcher y la lógica anterior de coincidencias de la ventana. La interfaz WPF queda como adaptador del motor.
- Clipsa pasa a GPL-3.0 con autorización del autor, conserva los créditos de ShareX y las licencias MIT previas, e incluye licencia/créditos/código fuente accesibles desde Ajustes.

## 0.7.0 · beta · 15 de noviembre de 2026

- Captura con scroll manteniendo el botón: dibuja el área y empieza a desplazar con rueda/trackpad o flechas antes de soltar; el primer movimiento fija la selección y soltar termina la captura.
- La primera vista se guarda antes de cerrar el selector, cuando Windows permite excluirlo de la captura, para conservar el comienzo durante el cambio de ventanas.
- Marco sobre la página que deja pasar la entrada y queda excluido de la imagen; controles compactos fuera del área cuando hay espacio, sin ventana de previsualización ni generación continua de miniaturas.
- Reconocimiento con regiones centrales amplias para tolerar laterales fijos, conservando la verificación de contenido y el rechazo de desplazamientos ambiguos o sin solapamiento.
- Las uniones fallidas transitorias no muestran inmediatamente el aviso; el marco indica cuándo hace falta recuperar una unión. Si al terminar falta el último tramo, permanece abierta para recuperarlo o guardar parcialmente con confirmación.
- Se conservan el modo de área fija y el guiado, así como Escape/clic derecho para cancelar.

## 0.6.2 · beta · 15 de noviembre de 2026

- Corrige la orientación al colocar la barra arriba, abajo o en las esquinas: siempre horizontal, incluso con una preferencia vertical guardada. Los laterales siguen siendo verticales.
- El ajuste de orientación manual queda limitado a posición libre y se desactiva al elegir un borde fijo.
- Regresión de los nueve anclajes con ambas preferencias guardadas, dimensiones reales de ventana, miniaturas compactas y ajustes.

## 0.6.1 · beta · 15 de noviembre de 2026

- Cabecera, separador y esquinas de la barra adaptados al borde superior, inferior o lateral; las capturas mantienen su orientación.
- Deslizadores independientes para mostrar al mantener el ratón (150–1500 ms) y ocultar al alejarlo (200–5000 ms), con instrucciones para activar la ocultación al encoger.
- Nombres de pantalla legibles; selector de pantalla de la barra sólo con varios monitores y explicación de su función.
- Scroll manual con muestreo de 16–20 ms, sin desplazar el pintado de la aplicación; descarte temprano de candidatos incompatibles manteniendo las comprobaciones de ambigüedad.
- Guiado que captura durante la animación y espera tres muestras estables antes de avanzar, en lugar de pausas fijas. Reintenta uniones transitorias antes de pausar y detecta antes el final comunicado por accesibilidad.
- Referencia de unión estable para evitar que se acumulen errores de un píxel en movimientos pequeños con escala fraccionaria; al terminar el guiado se espera y captura el resto de la animación.

## 0.6.0 · beta · 15 de noviembre de 2026

- Scroll guiado **Capturar más / Parar scroll**: pasos cortos, comprobación de cada unión y espera del último movimiento antes de guardar; accesibilidad Windows y rueda dirigida como alternativa.
- Panel de scroll sin bloqueo modal: permite usar la página con flechas, rueda y trackpad. El guiado se pausa si cambia la ventana activa.
- Barra en bordes, esquinas y posición libre, arrastrando el logo/nombre o ajustando porcentajes; formato vertical y monitor independiente.
- Ocultación opcional al colapsar, pestaña en el borde y espera configurable de 150–1500 ms para desplegar. Se oculta al alejar el ratón y permanece al usar las acciones.
- Reordenación y miniaturas adaptadas al formato vertical; se recuerda el estado colapsado.


## 0.5.3 · beta · 12 de noviembre de 2026

- Acciones visibles al pasar sobre una miniatura compacta: vista previa, editar, guardar, copiar, abrir y borrar, con etiquetas al mantener el ratón. Se conserva el arrastre para exportar o reordenar.
- Scroll con muestreo más frecuente y cola de imágenes adaptada al tamaño de región, hasta ocho vistas pendientes, separada del procesamiento. La miniatura se actualiza sin redibujar todo el historial en cada muestra.
- Reconoce desplazamientos grandes con una franja corta de solapamiento, manteniendo el rechazo de contenido ajeno o ambiguo. Muestra cómo recuperar un tramo omitido sin detener la sesión.
- El inicio de scroll espera a que se hayan renderizado y excluido los controles antes de guardar la primera vista y activar la captura.

- Escape y clic derecho cancelan la selección de región antes o durante el arrastre y liberan el ratón sin crear una captura.
- Cancelación de scroll aunque el foco esté en la web u otra aplicación; se descartan los tramos de la sesión cancelada.
- También se puede cancelar durante la preparación inicial de la captura; la barra vuelve a su estado anterior.
- Las pulsaciones de cancelación no se propagan a la aplicación capturada. La escucha se retira al finalizar y se conserva el funcionamiento normal de teclado y ratón.
- Indicación de cancelación en la selección y en el estado inicial de scroll; los avisos de selección quedan fuera de la captura de región.

## 0.5.2 · beta · 12 de noviembre de 2026

- Corrige el falso aviso de aplicación abierta al actualizar: el auxiliar cierra el identificador del mutex antes de iniciar Setup.
- El instalador comprueba y bloquea la propiedad real del mutex, en lugar de su mera existencia. Compatible con los auxiliares 0.5.0/0.5.1 que conservan un identificador sin propietario.
- Cierre explícito para actualizar, aunque esté activado mantener en la bandeja o haya ventanas ocultas. Se mantiene la confirmación para guardar capturas.
- Carpetas independientes por intento para permitir reintentar una actualización sin conflictos con el auxiliar anterior.
- Prueba de cierre WPF y regresión del instalador con identificador heredado abierto, además de rechazo de instancias realmente activas.

## 0.5.1 · beta · 12 de noviembre de 2026

- Impr Pant + 1/2/3 como valores iniciales: pantalla, región y scroll. Se respetan las configuraciones guardadas.
- Compatible con mantener Impr Pant o pulsarla y después el número en 750 ms, incluidos teclados Fn que emiten una pulsación breve o sólo una liberación.
- La pulsación simple espera cuando comparte tecla con combinaciones; se evitan capturas duplicadas, repeticiones y secuencias caducadas.
- Pruebas adicionales de secuencias y funcionamiento real con la barra visible, compacta y oculta.

## 0.5.0 · beta · 12 de noviembre de 2026

- Canal beta oficial y comprobación al iniciar/cada seis horas, con aviso persistente.
- Descarga integrada con progreso, manifiesto RSA-PSS y verificación SHA256; separación instalada/portable.
- Actualizador fuera del proceso principal: conserva capturas tras confirmación, ajustes y exportaciones; portable con respaldo y rollback.
- Rutas relativas para portable y migración de carpetas portables anteriores; no usa datos de otra instalación si su carpeta no permite escribir.
- El inicio automático sólo se cambia al modificar la opción y no elimina el de otra copia.
- Aviso de cambios sin guardar al cerrar el editor, goma sobre texto y guardado explícito de scroll parcial.
- Runtime .NET/WPF 10.0.15 y SDK 10.0.404.
- Herramienta de publicación local con clave privada en Windows CNG; nunca se incluyen claves privadas ni credenciales en el código.

## 0.4.3 · beta · 14 de octubre de 2026

- Elimina el aviso modal inicial en cada captura con scroll.
- Añade miniatura en vivo de los tramos acumulados, conservando el manejo con rueda, trackpad o barra lateral.
- Reconoce texto disperso y márgenes blancos con firmas de filas; comprueba los píxeles del contenido para rechazar uniones que sólo coinciden en el fondo blanco.
- Mejora la tolerancia a la barra de desplazamiento y conserva el rechazo de contenido ajeno o realmente ambiguo.
- Prueba adicional en página real de Edge con texto y fondo blanco, perfil temporal aislado y 15 desplazamientos continuos. No se promete reconstruir contenido nunca mostrado ni capturar todas las webs animadas.

## 0.4.2 · beta · 14 de octubre de 2026

- Guarda el primer tramo de scroll antes de mostrar la indicación para desplazar.
- Elimina la espera de tres muestras estables que podía convertir el tramo final en el primer y único tramo.
- Muestrea más frecuentemente y une tramos durante el desplazamiento continuo, sin exigir pausas en cada uno.
- Mantiene los tramos previos cuando una muestra no solapa; permite recuperar la posición y seguir sin perder el inicio.
- Regresión real: 15 desplazamientos a intervalos de 90 ms; resultado 250 × 525 px, con las primeras filas conservadas píxel a píxel.

## 0.4.1 · beta · 14 de octubre de 2026

- Selección de Impr Pant aunque Windows intercepte la tecla antes de llegar a WPF.
- Combinaciones Impr Pant + 1, 2 y 3, con botón de asignación a completa/región/scroll; compatible con teclado numérico.
- Evita repeticiones y doble captura; dispara al soltar Impr Pant y mantiene intacta la escritura fuera del gesto.
- Conserva la compatibilidad de ajustes y atajos anteriores.
- Incluye autoría Francisco Serrano Jiménez, avisos opcionales de actualización por GitHub y licencia original separada.
- Instalador y portable regenerados sin restaurar los scripts bloqueados ni cambiar la protección antivirus. La alerta histórica de System Watcher no se declara resuelta.

## 0.4.0 · beta · paquetes construidos y pruebas locales superadas

- Distribución portable e instalable para Windows x64 con .NET 10/WPF autocontenido.
- Instalación por usuario, sin elevación; actualización y desinstalación conservan datos.
- Marcador `portable.mode`; separación de los datos portables y los de `%APPDATA%\Clipsa`.
- Pipeline local con SDK 10.0.401, ZIP, instalador Inno Setup, SHA256, metadatos y plantilla de feed de actualización.
- Hook opcional de firma para aplicación, instalador y desinstalador; sin certificado disponible en este trabajo.
- Documentación española de uso, privacidad, recuperación opcional y comprobación manual de actualizaciones.

- Instancia única con activación de la ventana existente; inicio con Windows en segundo plano.
- Ajustes atómicos con copia de respaldo y recuperación opcional de sesiones interrumpidas, sin borrar sesiones activas de otra instancia.
- Registros locales y mensajes de error para fallos de captura, edición, guardado y atajos ocupados.
- Selección de monitor y colocación en coordenadas físicas; scroll en ambos sentidos con detección de cabeceras fijas y uniones ambiguas.
- Comprobación HTTPS manual de actualizaciones con límite de tamaño y tiempo de espera para toda la respuesta.
- Pruebas reales de ratón, recorte, deshacer/rehacer, arrastre desde vista compacta y entrega de PNG a Edge y Chrome.
- Instalación sin administrador, reinstalación con conservación de ajustes y desinstalación sin eliminar datos; protección del inicio automático de otras copias.

Resultados y verificaciones externas pendientes en [docs/VALIDACION.md](docs/VALIDACION.md).

## 0.3.1

Versión declarada en el proyecto al comenzar la preparación de distribución. No se reconstruye un historial de cambios no verificado.
