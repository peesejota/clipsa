# Uso de Clipsa

Esta documentación describe el contrato de la beta 0.4.0. Antes de distribuirla, el responsable debe completar las comprobaciones de [validación](VALIDACION.md), incluidas las funciones incorporadas en paralelo en la aplicación.

1. Abre Clipsa y usa los botones del panel, el menú de la bandeja o los atajos configurables: `Ctrl+Alt+T` (pantalla), `Ctrl+Alt+R` (región), `Ctrl+Alt+S` (scroll).
2. Para una región, arrastra la selección; `Esc` cancela. Para scroll, selecciona solo contenido desplazable, desplázate dejando una zona común entre tramos y pulsa Terminar.
3. Abre una captura para editarla. Puedes anotar, recortar y usar `Ctrl+Z` para deshacer o `Ctrl+Y`/`Ctrl+Shift+Z` para rehacer. Exporta las imágenes que quieras conservar.
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
