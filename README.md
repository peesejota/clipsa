# Clipsa 0.4.3 · beta para Windows

Repositorio oficial: [peesejota/clipsa](https://github.com/peesejota/clipsa). Las compilaciones de GitHub Actions generan instalador y portable; las Releases se preparan como borradores y requieren revisión antes de publicarse. [Cómo compilar y publicar](docs/GITHUB.md). La autoactualización todavía no está implementada.

Autor de Clipsa para Windows: **Francisco Serrano Jiménez**. Consulta la [preparación de GitHub y avisos de actualización](docs/GITHUB.md). Incluye autoría corregida, avisos opcionales de actualización, atajos Impr Pant y scroll continuo con inicio guardado antes del desplazamiento.

Aviso: hay una alerta de Kaspersky sobre el proceso de empaquetado pendiente de aclarar. El análisis posterior de los archivos finales no ha detectado amenazas, pero no resuelve la alerta de comportamiento. No restaurar ni ejecutar los scripts bloqueados. Consulta [la revisión de seguridad](docs/SEGURIDAD.md) antes de distribuir esta beta.

Clipsa es una reescritura para Windows con WPF, inspirada en [Tendedero de Alejandro Buján](https://github.com/alejandrobujan/tendedero). Permite capturar, ordenar y editar imágenes desde un panel y la bandeja del sistema. El crédito al trabajo original se conserva en [los avisos de terceros](THIRD-PARTY-NOTICES.md).

El destino de esta versión es Windows 11 x64. Windows 10 22H2 x64 está pendiente de validación; no se afirma que esté probado. No se distribuyen versiones x86 ni ARM64. Ambos paquetes incluyen el runtime de .NET 10 y WPF; no hay que instalar .NET por separado.

Para usar la versión portable, extrae **todo** `Clipsa-0.4.3-win-x64-portable.zip` en una carpeta donde puedas escribir y abre `Clipsa.exe`. Conserva `portable.mode` junto al ejecutable: los datos se guardan en `data/` junto a él. No es suficiente copiar solo el EXE.

Para instalar o actualizar, ejecuta `Clipsa-0.4.3-win-x64-setup.exe` con tu usuario habitual. Se instala en `%LOCALAPPDATA%\Programs\Clipsa`, sin solicitar permisos de administrador. Los ajustes y datos se guardan en `%APPDATA%\Clipsa`. Las actualizaciones y la desinstalación conservan esos datos. Cierra Clipsa con **Salir** desde la bandeja antes de instalar, actualizar o desinstalar; la X puede ocultar el panel.

Los atajos iniciales son `Ctrl+Alt+T` para pantalla completa, `Ctrl+Alt+R` para región y `Ctrl+Alt+S` para scroll manual. Puedes cambiarlos en Ajustes. Para scroll, espera «Inicio guardado», desplázate continuamente dejando solapamiento y pulsa Terminar. No hace falta parar en cada tramo. Un salto directo del principio al final no permite reconstruir el contenido intermedio; cabeceras fijas y animaciones pueden dificultar la unión.

Puedes usar rueda, trackpad o barra lateral mientras ves la página. No hay aviso modal previo: seleccionas el área y la miniatura muestra lo acumulado. Sigue siendo captura de pantalla, no lectura del documento interno del navegador; los saltos que omiten zonas visibles no pueden reconstruirse. 0.4.3 mejora el reconocimiento de texto y márgenes blancos y se ha probado con una página local real de Edge, sin usar perfiles del usuario.

En Ajustes, el botón **Usar Impr Pant + 1 / 2 / 3** asigna completa, región y scroll, respectivamente. Guarda los ajustes; mantén Impr Pant, pulsa el número y suelta Impr Pant para capturar. También se puede elegir Impr Pant sola pulsando el atajo y soltando esa tecla. No se cambia la configuración de Recortes de Windows; Clipsa sólo intercepta los gestos asignados mientras está abierta. Algunos portátiles requieren Fn para emitir Impr Pant. Si el teclado sólo emite la pulsación al soltar, Impr Pant sola funciona, pero las combinaciones mantenidas dependen del hardware.

La recuperación tras un cierre inesperado es opcional y comienza desactivada (`RecoverAfterCrash=false`). Las capturas, los ajustes y los informes de errores son locales. La consulta de actualizaciones usa un feed HTTPS configurable, vacío por defecto; abre el navegador para una actualización manual, sin instalarla automáticamente.

Consulta [uso](docs/USO.md), [privacidad](docs/PRIVACIDAD.md), [limitaciones](docs/LIMITACIONES.md), [compilación y distribución](docs/DISTRIBUCION.md) y [resultados de validación](docs/VALIDACION.md). El instalador y la portable se han construido y probado localmente, incluida instalación sin administrador, reinstalación y desinstalación. Siguen pendientes la firma con certificado propio, el destino público de actualizaciones y la comprobación en otro equipo limpio.
