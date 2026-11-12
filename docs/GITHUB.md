# GitHub y publicaciones

Repositorio: [peesejota/clipsa](https://github.com/peesejota/clipsa). Autor: Francisco Serrano Jiménez.

La compilación de `main` y pull requests genera instalador y ZIP portable autocontenidos. El workflow lee la versión del proyecto y verifica el runtime, recursos, firma de manifiestos de prueba y seguridad del actualizador. Sus artefactos duran 14 días; las Releases publicadas son las descargas permanentes.

La publicación de binarios y la activación del canal se realizan desde la herramienta local con la clave CNG del desarrollador. La clave privada no se entrega al runner ni se incluye en Git. El workflow manual sólo prepara borradores, que no se deben publicar con manifiestos sin firmar.

El canal beta firmado es `https://raw.githubusercontent.com/peesejota/clipsa/main/updates/beta.json`. Resuelve la exclusión de prereleases en `latest`. Incluye destinos separados para instalada/portable y sólo debe avanzar tras publicar y comprobar los paquetes correspondientes. No contiene credenciales.

Lee el [procedimiento completo](ACTUALIZACIONES.md) para construir, verificar, firmar, publicar y activar una versión. No subir capturas, ajustes, logs, cachés, binarios de pruebas, certificados o claves privadas; el `.gitignore` los excluye y hay que revisar el índice antes de cada subida.
