# Smart Home Media Bridge

Solución .NET 10 para exponer el audio de una Raspberry Pi a Google Home mediante OAuth 2.0, SQLite y un webhook REST.

## Proyectos

- `SmartHome.Frontend`: administración local con Blazor SSR.
- `SmartHome.Backend`: OAuth, webhook Google Home y procesos de audio.
- `SmartHome.Shared`: entidades, migraciones, DbContext y contratos.
- `SmartHome.Backend.Tests` y `SmartHome.Frontend.Tests`: integración HTTP.

## Desarrollo local

```powershell
dotnet restore
dotnet build SmartHome.slnx
dotnet ef database update --project SmartHome.Shared\SmartHome.Shared.csproj
dotnet test SmartHome.Shared.Tests\SmartHome.Shared.Tests.csproj
dotnet test SmartHome.Backend.Tests\SmartHome.Backend.Tests.csproj
dotnet test SmartHome.Frontend.Tests\SmartHome.Frontend.Tests.csproj
```

La base de desarrollo compartida está en `data/smarthome.db`. Las aplicaciones usan `../data/smarthome.db` desde sus directorios de proyecto. En Raspberry Pi se debe usar una ruta absoluta.

## Configuración

No guardar secretos en JSON ni en el repositorio. Establecer por variables de entorno o un archivo protegido:

- `ConnectionStrings__SharedDatabase`
- `OAuth__ClientId`
- `OAuth__ClientSecret`
- `OAuth__AllowedRedirectUris__0`
- `InitialAdmin__Username`
- `InitialAdmin__Password`
- `InitialAdmin__AgentUserId`
- `Audio__MpvPath`
- `Audio__YtDlpPath`
- `Audio__AmixerPath`
- `Audio__MixerName`
- `Backend__BaseUrl` para Frontend

`Database:ApplyMigrations` está desactivado por defecto. El Backend puede aplicar migraciones y ejecutar el seeder idempotente cuando se active explícitamente. Frontend no debe arrancar con ese flag habilitado.

## Raspberry Pi

Los archivos systemd de ejemplo están en `deploy/`. El procedimiento recomendado es:

1. Crear un usuario de sistema sin privilegios llamado `smarthome`.
2. Crear `/opt/smarthome-media-bridge` para las publicaciones y `/var/lib/smarthome-media-bridge` para SQLite.
3. Instalar el runtime .NET 10, `mpv`, `yt-dlp` y `alsa-utils`.
4. Publicar Frontend y Backend con `dotnet publish -c Release -r linux-arm64 --self-contained false`.
5. Copiar `deploy/smarthome.env.example` fuera del repositorio, completar los secretos y restringirlo a `root:smarthome` con permisos `640`.
6. Ejecutar una copia de seguridad y aplicar la migración una sola vez habilitando `Database__ApplyMigrations=true` en Backend.
7. Deshabilitar nuevamente esa variable y arrancar los servicios systemd.
8. Publicar el Backend detrás de HTTPS mediante un proxy inverso y configurar la URL OAuth pública en Google Home.

No ejecutar los servicios como root. Respaldar el archivo SQLite antes de actualizar la aplicación o aplicar migraciones.

## Decisiones de seguridad

- Las contraseñas locales se almacenan únicamente como hashes de ASP.NET Core.
- Los códigos OAuth son aleatorios, de un solo uso y de corta duración.
- Los access y refresh tokens siguen los campos de la especificación y se almacenan en SQLite para permitir la validación local. El archivo debe pertenecer a `smarthome:smarthome` con permisos restrictivos; el hashing de tokens en reposo queda como evolución posterior si el despliegue requiere protección adicional.
- El endpoint de volumen no usa el bearer de Google porque lo consume el dashboard local; debe permanecer detrás de la red local o del proxy que controle el acceso al Frontend.
- `mpv` y `amixer` reciben argumentos separados mediante `ProcessStartInfo.ArgumentList`; nunca se ejecuta una shell con texto de usuario.
- No se deben habilitar `Database__ApplyMigrations=true` simultáneamente en Frontend y Backend.
