# GitHub y actualizaciones de Clipsa

Autor: Francisco Serrano Jiménez. Repositorio: [peesejota/clipsa](https://github.com/peesejota/clipsa). La alerta histórica de Kaspersky sigue pendiente de aclarar; la primera subida de fuentes no certifica ni publica automáticamente los binarios.

## Compilar y preparar una publicación

El workflow `Build Clipsa Windows packages` se ejecuta al subir cambios a `main`, en pull requests y manualmente desde Actions. Lee la versión del proyecto, compila con el SDK fijado, ejecuta las pruebas de HTTPS no interactivas y genera instalador, portable, licencias, hashes y dos manifiestos separados. Los paquetes quedan como artefactos de Actions durante 14 días. Los tests de ratón, scroll y otras pruebas de escritorio deben hacerse aparte.

Para preparar una Release, abre Actions → Build Clipsa Windows packages → Run workflow. Marca `draft_release` y usa un tag como `v0.4.3-beta.1` que corresponda con la versión del proyecto. Se crea sólo un borrador, nunca se publica automáticamente. Usa un tag nuevo para cada intento de Release. Revisa los paquetes, su procedencia, antivirus y limitaciones antes de publicar manualmente el borrador. No subas capturas ni ajustes reales.

El instalador usa `update-manifest.json`; la portable tiene `update-manifest-portable.json`, cuyo enlace descarga el ZIP y no el Setup. En una Release estable publicada, configura en la instalada el enlace del repositorio y, en la portable, `https://github.com/peesejota/clipsa/releases/latest/download/update-manifest-portable.json`. Esos enlaces aún no sirven si no hay una Release estable publicada con esos archivos. Para betas, `latest` no sirve: se necesita un canal beta persistente antes de prometer avisos continuos. El enlace a un manifiesto de un tag concreto sólo comprueba ese tag y no avisa de versiones futuras.

No se ha restaurado `tools/Build-Release.ps1`: el workflow contiene pasos explícitos ejecutados en el runner de GitHub. Tampoco se han añadido claves privadas ni tokens al código. El permiso de escritura se concede sólo al job manual que crea borradores.

## Preparado en el código

En Ajustes se puede introducir `https://github.com/peesejota/clipsa` o un manifiesto HTTPS. La URL del repositorio se transforma en `https://github.com/peesejota/clipsa/releases/latest/download/update-manifest.json`.

«Avisar de nuevas versiones al iniciar Clipsa» está desactivado por defecto. Si se activa con un canal configurado, consulta una vez por inicio y muestra un aviso de bandeja cuando la versión es posterior. Pulsarlo abre la descarga en el navegador. Buscar actualizaciones en Ajustes sigue funcionando manualmente. Windows puede suprimir los avisos; Ajustes es la alternativa.

La consulta limita el tiempo y tamaño de respuesta, exige HTTPS y rechaza redirecciones a HTTP o con credenciales. No descarga ni ejecuta instaladores automáticamente. El SHA256 es un dato de integridad publicado; la app no verifica el archivo descargado a través del navegador.

## Publicación tras resolver la alerta

1. Crear el repositorio elegido. Para descargas sin autenticación, las Releases deben ser públicas. No insertar tokens en el ejecutable.
2. Subir sólo fuentes, recursos de marca, licencias y documentación. No subir capturas, ajustes, logs, informes con rutas personales, `data`, `.build-tools`, cachés, binarios de pruebas ni certificados. Revisar antes de subir; no publicar la carpeta completa indiscriminadamente.
3. Los paquetes 0.4.1 incorporan autoría corregida y avisos opcionales; validar una Release real antes de activar el canal público. Los paquetes 0.4.0 anteriores NO incorporan esos cambios.
4. Publicar una Release con instalador, portable, SHA256SUMS y `update-manifest.json`. Rellenar este esquema con valores reales:

```json
{
  "version": "VERSION_NUEVA",
  "downloadUrl": "https://github.com/peesejota/clipsa/releases/download/TAG/INSTALADOR.exe",
  "sha256": "SHA256_REAL_DEL_INSTALADOR"
}
```

El ejemplo no es válido hasta rellenarlo. No publicar la plantilla vacía como un feed activo.

[GitHub latest release](https://docs.github.com/en/rest/releases/releases#get-the-latest-release) excluye borradores y prereleases: no marcar una beta como estable sólo para que aparezca en `latest`. Para un canal beta, configurar una URL HTTPS directa a un manifiesto beta revisado, en lugar de `releases/latest`.

## Autoactualización

No está implementada. Necesitaría consentimiento, descarga temporal, comprobación real de SHA256 y firma del editor, conservación de capturas pendientes y tratamiento distinto para instalada/portable. Un hash junto al instalador no protege frente al compromiso del mismo repositorio. Nunca ejecutar una actualización para eludir una advertencia del antivirus.
