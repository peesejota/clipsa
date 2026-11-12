# Uso de Clipsa

Uso de la beta 0.5.3. Consulta las pruebas realizadas y sus límites en [validación](VALIDACION.md).

1. Abre Clipsa y usa los botones del panel, el menú de la bandeja o los atajos configurables. En configuraciones nuevas: `Impr Pant + 1` (pantalla), `Impr Pant + 2` (región), `Impr Pant + 3` (scroll). Mantén Impr Pant mientras pulsas el número, o púlsala y después el número en menos de 0,75 segundos. Si tu teclado exige Fn para emitir Impr Pant, úsala también. Los atajos guardados se conservan; para cambiar los anteriores, elige **Usar Impr Pant + 1 / 2 / 3** en Ajustes y guarda.
2. Para una región, arrastra la selección. `Esc` o clic derecho cancelan, también durante el arrastre. Para scroll, selecciona solo contenido desplazable, desplázate dejando una zona común entre tramos y pulsa Terminar. `Esc` o clic derecho descartan la captura de scroll activa aunque el foco esté en la web; no guardan una imagen parcial. Estas acciones también cancelan los instantes iniciales de preparación. Las capturas ya completadas permanecen en la barra.
3. Abre una captura para editarla. Puedes anotar, recortar y usar `Ctrl+Z` para deshacer o `Ctrl+Y`/`Ctrl+Shift+Z` para rehacer. Con la barra colapsada, pasa el ratón sobre una miniatura para ver una vista previa y los botones de editar, guardar, copiar, abrir y borrar. Puedes mover el ratón hasta ellos y mantenerlo encima para leer su explicación. Arrastra desde la miniatura para exportar o reordenar. Exporta las imágenes que quieras conservar.
4. Ajusta los atajos, el aspecto y el comportamiento desde Ajustes. El inicio con Windows es opcional. Si mueves la carpeta portable, vuelve a configurar el inicio con Windows para que apunte a la nueva ruta.
5. La X puede mantener la aplicación en la bandeja. Usa **Salir** para terminarla completamente, especialmente antes de actualizar.

## Dónde se guardan los datos

| Modo | Marcador | Raíz de datos |
| --- | --- | --- |
| Portable | `portable.mode` junto a `Clipsa.exe` | `data/` junto al ejecutable |
| Instalado | Sin marcador | `%APPDATA%\Clipsa` |

En cada raíz se guardan la configuración, los registros en `logs/` y las capturas de sesión en `sessions/<GUID>/`. “Portable” no significa que no pueda dejar rastros en Windows, el portapapeles o una carpeta de exportación. No añadas el marcador a una instalación normal: cambia la ubicación de los datos.

Para actualizar la versión portable, sal de Clipsa, haz una copia de seguridad de `data/` y extrae el nuevo paquete en una carpeta nueva. Copia `data/` a esa carpeta antes de abrirlo; mantén el marcador. Para actualizar una instalación, usa el nuevo instalador con la misma identidad y ubicación. El instalador no migra ni copia datos de una versión portable a una instalada.

## Recuperación y actualizaciones

La recuperación tras fallo requiere activar `RecoverAfterCrash`; comienza desactivada. Cuando se active, trata las capturas recuperables como información sensible y revisa las sesiones locales tras un fallo. No presupongas que una captura no exportada estará disponible sin esa opción.

`AppSettings.UpdateFeedUrl` comienza vacío. Con una URL HTTPS configurada, el comprobador consulta un manifiesto y presenta la actualización; el usuario abre el navegador y decide descargar e instalar. No es una actualización automática ni una ejecución silenciosa del instalador. Comprueba el hash y la firma cuando estén disponibles.
