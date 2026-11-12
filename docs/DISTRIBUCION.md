# Construcción y distribución

Clipsa usa WPF para Windows x64. SDK fijado: 10.0.404; runtime autocontenido: .NET/WPF 10.0.15. GitHub Actions compila el código, ejecuta pruebas no interactivas y produce el Setup de Inno y ZIP portable con licencias e inventario de archivos del programa.

Las pruebas de escritorio se ejecutan aparte con una sesión interactiva. La compilación CI no valida todos los monitores o aplicaciones, ni sustituye un análisis antivirus o una prueba en un PC limpio. [Resultados](VALIDACION.md).

Para las publicaciones utiliza [ACTUALIZACIONES.md](ACTUALIZACIONES.md). La herramienta `release-tools` firma los manifiestos con la clave local de Windows CNG, comprueba el inventario y publica con la cuenta autenticada en Git Credential Manager. Nunca solicita que pegues tokens en el chat. No se han restaurado `Build-Release.ps1` ni los scripts bloqueados históricamente.

El Setup instala por usuario sin administrador. El ZIP debe extraerse completo y conservar `portable.mode`; en portable los ajustes y respaldos están en `data`. El actualizador preserva los archivos personales y ofrece guardar capturas temporales antes de cerrar. La instalada y la portable descargan tipos de paquete distintos.

Esta beta no incluye firma Authenticode de editor. El manifiesto RSA-PSS protege las actualizaciones, pero no identifica al editor ante Windows ni elimina advertencias de SmartScreen. No añadir exclusiones ni desactivar el antivirus para instalar.
