# Limitaciones de la beta 0.8.0

El destino es Windows 11 x64. Windows 10 22H2 x64 no está validado: el instalador permite técnicamente el build 19045 o posterior, pero ese umbral no certifica compatibilidad ni pruebas. No se ofrece soporte para x86, ARM64 nativo ni emulación ARM64 en este paquete.

La captura de ventanas protegidas, contenido DRM, escritorio seguro/UAC, sesiones bloqueadas o remotas puede fallar o producir imágenes negras. Deben verificarse varios monitores, escalas DPI mixtas, posiciones negativas y cambios de pantalla. Un atajo puede estar ocupado por otra aplicación; configura otro si no responde.

Desde 0.8.0, el motor incorpora la captura GDI y la unión de filas de ShareX 22.4.0, adaptadas a WPF y System.Drawing. El automático controla rueda, flecha abajo, Av. página o mensajes de scroll y espera la estabilización entre pasos. Se conserva el modo manual mantenido. La salida se limita a 60 millones de píxeles y 65.535 filas; el automático tiene un máximo de 600 pasos por sesión.

La unión requiere filas visibles comunes. Cabeceras/pies fijos se gestionan con el algoritmo de ShareX, pero menús laterales grandes, animaciones, contenido repetitivo, ventanas protegidas o saltos que exceden la vista pueden impedirla o producir resultados que necesitan revisión. Selecciona sólo la parte desplazable. El motor compone hacia abajo y no añade contenido por encima del inicio al retroceder. Los avances estimados están desactivados inicialmente; si los habilitas, se marcan y requieren confirmación antes de guardar. No se promete una captura perfecta de todas las aplicaciones o dispositivos.

En las pruebas nuevas se verificaron Chrome real con desplazamiento continuo y con rueda automática, un destino WPF animado, rueda/flechas mantenidas, cancelación y ausencia de fugas GDI. No se certificaron todos los trackpads físicos, navegadores, escalas ni varios monitores físicos. El automático se detiene al cambiar de ventana o sacar el ratón del área, en lugar de enviar entradas a otra aplicación.

El runtime incluido evita instalar .NET, pero sus correcciones de seguridad requieren una nueva distribución de Clipsa. Desde 0.5.0 hay canal beta, descarga verificada y actualización con confirmación para instalada y portable. No se garantiza que un antivirus permita la operación ni que una carpeta de sólo lectura pueda actualizarse. Las versiones 0.4.x necesitan la primera actualización manual. [Procedimiento y seguridad](ACTUALIZACIONES.md).

Los manifiestos de actualización están firmados con una clave pública incorporada en la app. La firma Authenticode de EXE e instalador sigue pendiente de un certificado de editor externo. Un SHA256 por sí solo no identifica al editor. Windows puede mostrar advertencias para esta beta sin Authenticode; no se garantiza reputación de SmartScreen incluso con un certificado.

La compilación, las pruebas gráficas y las pruebas de instalación, reinstalación y desinstalación con un usuario no administrador han pasado en este equipo. El instalador de prueba utilizó identidad, carpeta y accesos directos aislados, pero no una máquina virtual. Siguen pendientes otro equipo limpio sin .NET, varios monitores físicos con DPI mixto y la firma con credenciales externas. Consulta los resultados y el alcance exacto en [VALIDACION.md](VALIDACION.md).
