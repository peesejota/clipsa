# Impr Pant en Clipsa 0.4.1

En Ajustes pulsa «Usar Impr Pant + 1 / 2 / 3» y Guarda. Después:

- Impr Pant + 1: captura completa.
- Impr Pant + 2: selección de región.
- Impr Pant + 3: scroll manual.

Mantén Impr Pant, pulsa y suelta el número, y suelta Impr Pant. La acción se ejecuta al soltar Impr Pant. Admite los números de la fila superior y del teclado numérico (con Bloq Num activado). Para Impr Pant sola, pulsa el botón de un atajo, pulsa y suelta Impr Pant, y guarda. Esc cancela la elección; Supr desactiva el atajo. El botón de combinaciones cambia los tres atajos sólo al guardar; Cancelar conserva los anteriores.

Windows puede usar Impr Pant para abrir Recortes y WPF no recibir esa pulsación. Clipsa utiliza un listener de Windows en un hilo dedicado para los gestos Impr Pant asignados. No guarda ni envía el texto tecleado. Al cerrar o entrar en Ajustes se desactiva el listener de capturas; durante la elección del atajo sólo se usa el de configuración. No se modifica el registro ni la opción de Recortes de Windows.

Si otro programa intercepta la tecla, puede haber conflicto. En algunos portátiles hace falta Fn + Impr Pant: Fn se procesa en el teclado, no como modificador configurable en Windows. Si el firmware sólo emite Impr Pant al soltarla, la tecla sola se admite, pero no se puede garantizar un gesto mantenido. Una tecla que realmente emita Win + Shift + S en lugar de Impr Pant debe configurarse con esa combinación o desde el software del teclado.

Referencias: [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey), [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc).
