# Cambios

## 0.5.3 · beta · 12 de noviembre de 2026

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
