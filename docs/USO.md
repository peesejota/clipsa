# Uso de Clipsa

Uso de la beta 0.8.0. Consulta las pruebas realizadas y sus límites en [validación](VALIDACION.md).

1. Abre Clipsa y usa los botones del panel, el menú de la bandeja o los atajos configurables. En configuraciones nuevas: `Impr Pant + 1` (pantalla), `Impr Pant + 2` (región), `Impr Pant + 3` (scroll). Mantén Impr Pant mientras pulsas el número, o púlsala y después el número en menos de 0,75 segundos. Si tu teclado exige Fn para emitir Impr Pant, úsala también. Los atajos guardados se conservan; para cambiar los anteriores, elige **Usar Impr Pant + 1 / 2 / 3** en Ajustes y guarda.
2. Para una región, arrastra la selección. Para scroll, arrastra el área de contenido y suelta: empieza el avance **Automático**. Elige rueda, flecha abajo, Av. página o mensaje de scroll según la aplicación. **Parar** detiene el avance; **Terminar y guardar** añade el resultado a la barra. Si prefieres desplazar tú, marca el área y empieza a usar rueda/trackpad o flechas sin soltar el botón izquierdo: el primer movimiento fija el marco y soltar termina la captura. Escape o clic derecho cancelan también durante la selección o preparación, sin guardar una imagen parcial.
3. Abre una captura para editarla. Puedes anotar, recortar y usar `Ctrl+Z` para deshacer o `Ctrl+Y`/`Ctrl+Shift+Z` para rehacer. Con la barra colapsada, pasa el ratón sobre una miniatura para ver una vista previa y los botones de editar, guardar, copiar, abrir y borrar. Puedes mover el ratón hasta ellos y mantenerlo encima para leer su explicación. Arrastra desde la miniatura para exportar o reordenar. Exporta las imágenes que quieras conservar.
4. Ajusta los atajos, el aspecto y el comportamiento desde Ajustes. El inicio con Windows es opcional. Si mueves la carpeta portable, vuelve a configurar el inicio con Windows para que apunte a la nueva ruta.
5. La X puede mantener la aplicación en la bandeja. Usa **Salir** para terminarla completamente, especialmente antes de actualizar.

En **Ajustes → Posición y ocultación de la barra**, elige borde, esquina, monitor y orientación. **Posición libre** admite porcentajes horizontal/vertical o arrastrar el logo y el nombre con la barra extendida. Arriba, abajo y las esquinas usan siempre formato horizontal; los laterales, vertical. El ajuste manual de orientación sólo se aplica a **Posición libre**, aunque conserves una preferencia vertical de una versión anterior. Activa **Ocultar automáticamente la barra al encogerla**, guarda y pulsa la flecha de encoger para dejar sólo una pestaña. Dos deslizadores independientes controlan cuánto mantener el ratón para mostrarla (150–1500 ms) y cuánto esperar para ocultarla después de alejarlo (200–5000 ms). La cabecera acompaña al borde elegido; las imágenes permanecen derechas. El selector de pantalla de la barra sólo aparece con varios monitores y no afecta a la pantalla de captura. La X mantiene el comportamiento de bandeja hasta volver a abrir Clipsa.

Durante el scroll ves la página con un marco que deja pasar el ratón, sin ventana de previsualización. Los controles se colocan fuera del área cuando hay espacio. El automático espera entre avances y a que termine la animación; se detiene si cambias de ventana o sacas el ratón del área. Puedes pausar y continuar manualmente con rueda, trackpad, flechas o barra lateral.

La unión usa el algoritmo de filas de ShareX 22.4.0 contra la imagen acumulada. Selecciona sólo el contenido que se desplaza, excluyendo menús laterales grandes. Si un paso no se puede unir, conserva lo anterior y permite probar un avance menor con flechas o volver al último tramo reconocido. **Permitir avance estimado** reutiliza un desplazamiento aprendido cuando falla una coincidencia: es opcional y el resultado requiere confirmación para revisarlo. El motor compone hacia abajo; volver arriba no añade filas por encima del inicio. Un salto que omite una zona completa no puede reconstruirse.

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
