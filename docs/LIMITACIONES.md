# Limitaciones de la beta 0.4.0

El destino es Windows 11 x64. Windows 10 22H2 x64 no está validado: el instalador permite técnicamente el build 19045 o posterior, pero ese umbral no certifica compatibilidad ni pruebas. No se ofrece soporte para x86, ARM64 nativo ni emulación ARM64 en este paquete.

La captura de ventanas protegidas, contenido DRM, escritorio seguro/UAC, sesiones bloqueadas o remotas puede fallar o producir imágenes negras. Deben verificarse varios monitores, escalas DPI mixtas, posiciones negativas y cambios de pantalla. Un atajo puede estar ocupado por otra aplicación; configura otro si no responde.

La captura larga une tramos de scroll manual y depende de su solapamiento. Cabeceras fijas, animaciones, contenido repetitivo, cambios de anchura o desplazamientos grandes pueden dificultar la unión. Se rechazan uniones ambiguas y se limita la salida a 60 millones de píxeles y 65.535 filas para controlar el consumo de memoria. El historial de edición también consume memoria; no se promete recuperación perfecta.

El runtime incluido evita instalar .NET, pero sus correcciones de seguridad requieren una nueva distribución de Clipsa. La comprobación de actualizaciones es gestionada por la aplicación mediante un feed HTTPS configurable; la descarga y la instalación son manuales. No hay URL pública configurada ni publicación desde los scripts.

La firma no está disponible sin certificado y clave accesibles. Un SHA256 permite detectar cambios respecto al hash recibido, pero por sí solo no identifica al editor. Windows puede mostrar advertencias para una beta sin firma; no se garantiza reputación de SmartScreen incluso con un certificado.

La compilación, las pruebas gráficas y las pruebas de instalación, reinstalación y desinstalación con un usuario no administrador han pasado en este equipo. El instalador de prueba utilizó identidad, carpeta y accesos directos aislados, pero no una máquina virtual. Siguen pendientes otro equipo limpio sin .NET, varios monitores físicos con DPI mixto y la firma con credenciales externas. Consulta los resultados y el alcance exacto en [VALIDACION.md](VALIDACION.md).
