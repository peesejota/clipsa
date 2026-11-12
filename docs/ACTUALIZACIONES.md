# Actualizaciones de Clipsa

## Usuario

Desde 0.5.0 el canal beta oficial está configurado. Se comprueba al iniciar y cada seis horas, sin bloquear capturas; la opción se puede desactivar en Ajustes. No se instalan actualizaciones sin pulsar **Actualizar ahora** y confirmar. **Descargar** sólo guarda el paquete verificado y permite abrir su carpeta.

La instalada descarga el Setup; la portable descarga el ZIP. Se validan HTTPS, firma RSA-PSS del manifiesto y SHA256 del archivo. Una fuente personalizada sin firma puede abrirse en el navegador, pero no se instala automáticamente. Se rechazan manifiestos manipulados, ZIP con rutas fuera de la aplicación y sobrescritura de `data`.

Al actualizar se ofrece guardar todas las capturas temporales en una carpeta, descartarlas expresamente o cancelar. El programa espera a que la instancia actual cierre antes de modificar archivos. La portable respalda los archivos reemplazados y los restaura si falla la operación. Nunca elimina recursivamente la carpeta de la aplicación. Las capturas exportadas junto al EXE y `data` se conservan. Las copias y descargas quedan en `data/updates` (portable) o en la carpeta de ajustes de la instalada: pueden ocupar espacio y no son capturas de sesión.

Las versiones 0.4.x necesitan actualizar manualmente una primera vez. No prometemos autoactualización de esas versiones antiguas, ni del sistema operativo o de otras aplicaciones. Si Kaspersky bloquea una operación, no se intenta eludirlo: conservar el informe y revisar con el fabricante.

## Publicador: firma y canal

La clave privada se creó en el almacén CNG del usuario Windows del desarrollador, con nombre `Clipsa.UpdateSigning.2026.v1`. No se exporta en texto ni se sube a GitHub. La app contiene sólo su clave pública en `UpdateTrust.cs`. La firma del manifiesto no es una firma Authenticode del ejecutable ni garantiza reputación de SmartScreen.

Haz copia cifrada de la clave en un destino fuera del repositorio y con una contraseña larga que no se comparta en el chat:

```powershell
dotnet run --project release-tools/Clipsa.ReleaseTools.csproj -- backup-key C:\ruta-segura\clipsa-signing-backup.pk8
```

La contraseña se introduce en la terminal sin eco. Conservar el perfil Windows o la copia cifrada es imprescindible para seguir firmando versiones compatibles con la clave pública distribuida. No reemplazar la clave sin planificar una migración de confianza.

## Publicar una versión nueva

1. Incrementar `Version` en `Clipsa/Clipsa.csproj`, actualizar cambios y probar. Cada publicación funcional debe tener una versión numérica posterior; no reutilizar la misma versión cambiando sólo el sufijo beta.
2. Subir a `main` y esperar una compilación correcta de GitHub Actions. Se generan instalador, ZIP, licencias, inventario `app-files.json`, hashes y metadatos. Las pruebas de escritorio se ejecutan aparte en un entorno controlado.
3. Descargar los artefactos del run en una carpeta vacía:

```powershell
dotnet run --project release-tools/Clipsa.ReleaseTools.csproj -- fetch ID_DEL_RUN releases/Clipsa-0.5.1
```

4. Revisar y analizar los paquetes. Publicar desde el PC que tiene la clave de firma, usando la autenticación existente de Git Credential Manager; los tokens permanecen en memoria:

```powershell
dotnet run --project release-tools/Clipsa.ReleaseTools.csproj -- publish releases/Clipsa-0.5.1 0.5.1 v0.5.1-beta.1
```

La herramienta firma los dos manifiestos, regenera los hashes, crea un borrador, sube los archivos y publica la Release beta. No sobreescribe una versión existente. Si falla una subida, queda un borrador para revisar, no un canal apuntando a archivos incompletos.

5. Copiar **el manifiesto firmado publicado**, sin editarlo, a `updates/beta.json` y subir ese único cambio a `main`. El canal sólo debe avanzar después de publicar y comprobar las descargas. Verificar con la aplicación que una versión anterior lo detecta, descarga el paquete correcto y rechaza modificaciones.

No basta con marcar una prerelease como `latest`: GitHub no las ofrece como última versión estable. El canal persistente `updates/beta.json` resuelve esto sin incluir tokens en la app. Una publicación estable futura debe usar su propio canal revisado.

## Límites de validación

Las pruebas automáticas comprueban firmas, hashes, separación de modos, rutas peligrosas, conservación de datos y rollback. No equivalen a una auditoría de seguridad completa ni a compatibilidad garantizada con todos los equipos. Se necesita Windows x64 y una carpeta portable escribible; mover la portable a otra ubicación conserva rutas relativas, pero el inicio automático con Windows puede necesitar reconfiguración si cambia la ruta del EXE.
