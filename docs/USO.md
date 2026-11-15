# Uso de Clipsa

Uso de la beta 0.7.0. Consulta las pruebas realizadas y sus límites en [validación](VALIDACION.md).

1. Abre Clipsa y usa los botones del panel, el menú de la bandeja o los atajos configurables. En configuraciones nuevas: `Impr Pant + 1` (pantalla), `Impr Pant + 2` (región), `Impr Pant + 3` (scroll). Mantén Impr Pant mientras pulsas el número, o púlsala y después el número en menos de 0,75 segundos. Si tu teclado exige Fn para emitir Impr Pant, úsala también. Los atajos guardados se conservan; para cambiar los anteriores, elige **Usar Impr Pant + 1 / 2 / 3** en Ajustes y guarda.
2. Para una región, arrastra la selección. `Esc` o clic derecho cancelan, también durante el arrastre. Para scroll, marca el área manteniendo el botón izquierdo y, sin soltarlo, empieza a usar rueda, trackpad o flechas. El primer movimiento fija el rectángulo; soltar el botón termina y añade la captura a la barra. Si sueltas antes de empezar a desplazar, puedes continuar con el área fija y pulsar Terminar. `Esc` o clic derecho descartan la captura de scroll activa aunque el foco esté en la web; no guardan una imagen parcial. Estas acciones también cancelan los instantes iniciales de preparación. Las capturas ya completadas permanecen en la barra.
3. Abre una captura para editarla. Puedes anotar, recortar y usar `Ctrl+Z` para deshacer o `Ctrl+Y`/`Ctrl+Shift+Z` para rehacer. Con la barra colapsada, pasa el ratón sobre una miniatura para ver una vista previa y los botones de editar, guardar, copiar, abrir y borrar. Puedes mover el ratón hasta ellos y mantenerlo encima para leer su explicación. Arrastra desde la miniatura para exportar o reordenar. Exporta las imágenes que quieras conservar.
4. Ajusta los atajos, el aspecto y el comportamiento desde Ajustes. El inicio con Windows es opcional. Si mueves la carpeta portable, vuelve a configurar el inicio con Windows para que apunte a la nueva ruta.
5. La X puede mantener la aplicación en la bandeja. Usa **Salir** para terminarla completamente, especialmente antes de actualizar.

En **Ajustes → Posición y ocultación de la barra**, elige borde, esquina, monitor y orientación. **Posición libre** admite porcentajes horizontal/vertical o arrastrar el logo y el nombre con la barra extendida. Arriba, abajo y las esquinas usan siempre formato horizontal; los laterales, vertical. El ajuste manual de orientación sólo se aplica a **Posición libre**, aunque conserves una preferencia vertical de una versión anterior. Activa **Ocultar automáticamente la barra al encogerla**, guarda y pulsa la flecha de encoger para dejar sólo una pestaña. Dos deslizadores independientes controlan cuánto mantener el ratón para mostrarla (150–1500 ms) y cuánto esperar para ocultarla después de alejarlo (200–5000 ms). La cabecera acompaña al borde elegido; las imágenes permanecen derechas. El selector de pantalla de la barra sólo aparece con varios monitores y no afecta a la pantalla de captura. La X mantiene el comportamiento de bandeja hasta volver a abrir Clipsa.

Durante el scroll ves la página directamente con un marco que deja pasar el ratón, sin una ventana de previsualización. Los controles pequeños se colocan fuera del área siempre que hay espacio. Puedes usar rueda, trackpad, flechas o barra de la página. **Capturar más** avanza la ventana seleccionada en pasos cortos y comprueba las uniones. **Parar scroll** permite revisar o continuar manualmente; **Terminar y guardar** espera el movimiento pendiente y guarda. El guiado se pausa si cambias de ventana. Escape o clic derecho cancelan. Si una aplicación no admite accesibilidad ni mensajes de rueda, utiliza el modo manual. Si al soltar no se ha podido unir el último tramo, la sesión sigue abierta para recuperarlo volviendo un poco arriba; puedes terminar con el botón o guardar explícitamente sólo lo aceptado. No se exporta una imagen cortada silenciosamente. Las zonas que nunca se mostraron no se pueden reconstruir.

## Dónde se guardan los datos

| Modo | Marcador | Raíz de datos |
| --- | --- | --- |
| Portable | `portable.mode` junto a `Clipsa.exe` | `data/` junto al ejecutable |
| Instalado | Sin marcador | `%APPDATA%\Clipsa` |

En cada raíz se guardan la configuración, los registros en `logs/` y las capturas de sesión en `sessions/<GUID>/`. “Portable” no significa que no pueda dejar rastros en Windows, el portapapeles o una carpeta de exportación. No añadas el marcador a una instalación normal: cambia la ubicación de los datos.

Para actualizar la versión portable, sal de Clipsa, haz una copia de seguridad de `data/` y extrae el nuevo paquete en una carpeta nueva. Copia `data/` a esa carpeta antes de abrirlo; mantén el marcador. Para actualizar una instalación, usa el nuevo instalador con la misma identidad y ubicación. El instalador no migra ni copia datos de una versión portable a una instalada.

## Recuperación y actualizaciones

La recuperación tras fallo requiere activar `RecoverAfterCrash`; comienza desactivada. Cuando se active, trata las capturas recuperables como información sensible y revisa las sesiones locales tras un fallo. No presupongas que una captura no exportada estará disponible sin esa opción.

El canal beta oficial viene configurado. Clipsa puede buscar actualizaciones al iniciar y cada seis horas; también puedes buscarlas desde Ajustes. Verifica la firma del manifiesto y el SHA256 del paquete antes de actualizar. **Actualizar ahora** aplica la actualización con confirmación, tanto instalada como portable; **Descargar** permite conservar el paquete. Consulta [ACTUALIZACIONES.md](ACTUALIZACIONES.md).
