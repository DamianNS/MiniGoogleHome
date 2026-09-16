# Copilot Instructions

## Alcance

Estas instrucciones se aplican a todo el repositorio `MiniGoogleHome`. El proyecto implementa Smart Home Media Bridge sobre .NET 10 para exponer audio de una Raspberry Pi a Google Home.

Para la descripción técnica completa, consultar [AGENTS.md](../AGENTS.md). Para el diseño funcional consultar [Especificacion_SmartHome_MediaBridge.md](../Especificacion_SmartHome_MediaBridge.md), para el plan ejecutable [tareas.md](../tareas.md) y para operación/despliegue [README.md](../README.md).

## Stack obligatorio

- .NET 10 y `net10.0`.
- Solución `SmartHome.slnx`.
- ASP.NET Core Web API para Backend.
- Blazor Web App SSR estático para Frontend.
- Entity Framework Core 10.0.12 con SQLite.
- `System.Text.Json` con camelCase para contratos Google Home.
- xUnit y `WebApplicationFactory` para pruebas.
- Despliegue objetivo: Linux ARM64/Raspberry Pi.

Mantener nullable reference types e `ImplicitUsings` habilitados. No introducir tecnologías, paquetes o patrones nuevos si la solución existente ya cubre la necesidad.

## Arquitectura

La solución tiene tres proyectos de producción y tres proyectos de pruebas:

- `SmartHome.Shared`: entidades EF, `SmartHomeDbContext`, migraciones, seeder, bootstrap de base de datos, opciones OAuth y DTOs Google Home.
- `SmartHome.Backend`: endpoints OAuth, webhook Google Home, autenticación Bearer, health check y ejecución de audio/volumen Linux.
- `SmartHome.Frontend`: administración Blazor SSR, cookie de sesión local, autorización OAuth y dashboard.
- `SmartHome.Shared.Tests`: unitarias de persistencia, contratos y servicios.
- `SmartHome.Backend.Tests`: integración HTTP del Backend con SQLite temporal y fakes.
- `SmartHome.Frontend.Tests`: integración HTTP del flujo OAuth SSR.

No mezclar responsabilidades: las entidades/DTOs compartidos permanecen en Shared; el acceso a procesos Linux permanece en Backend; las vistas y sesión administrativa permanecen en Frontend.

## Contratos críticos

No cambiar estos valores sin actualizar especificación, pruebas y configuración de Google Home:

- Dispositivo: `pi_media_speaker_01`.
- Usuario de agente inicial: `usr_master_pi_01`.
- Nombre: `Parlante Raspberry`.
- Tipo: `action.devices.types.SPEAKER`.
- Traits: `action.devices.traits.MediaState` y `action.devices.traits.Volume`.
- Fabricante/modelo: `Niquelsoft` / `PiMediaBridgeV1`.
- Comandos: `action.devices.commands.mediaPlay` y `action.devices.commands.setVolume`.
- Respuestas: conservar `requestId`, `SUCCESS`, `ERROR`, `PLAYING`, `currentVolume` y `online`.

Usar los DTOs de `SmartHome.Shared/Contracts` y `GoogleHomeJson.Options`; no crear modelos JSON anónimos duplicados ni exponer entidades EF desde controladores.

## Rutas y autenticación

Backend:

- `GET /health`: salud de aplicación y SQLite.
- `POST /oauth/token`: `application/x-www-form-urlencoded`; grants `authorization_code` y `refresh_token`.
- `POST /api/smarthome`: webhook protegido con `Authorization: Bearer`.
- `GET /api/status/volume`: estado para el dashboard local.

Frontend:

- `GET /`: portada.
- `GET /login`, `POST /login`: sesión administrativa con cookie `SmartHome.Admin`.
- `GET /oauth/authorize`: pantalla SSR de autorización.
- `POST /oauth/authorize/submit`: validación y emisión del código OAuth.
- `GET /dashboard`: vista protegida.
- `POST /logout`: cierre de sesión.

La cookie administrativa nunca sustituye al Bearer de Google. Mantener antiforgery en formularios SSR. Validar siempre `client_id`, `client_secret`, `response_type`, `redirect_uri` mediante allowlist, expiración, `state`, uso único y asociación de usuario.

## SQLite y EF Core

- Usar `IDbContextFactory<SmartHomeDbContext>` en servicios y operaciones concurrentes.
- No compartir un `DbContext` entre peticiones ni usar `EnsureCreated` en producción.
- Desarrollo: `data/smarthome.db` en la raíz; los proyectos usan `Data Source=../data/smarthome.db`.
- Raspberry Pi: ruta absoluta, normalmente `/var/lib/smarthome-media-bridge/smarthome.db`.
- Usar UTC para expiraciones y fechas persistidas.
- Aplicar migraciones solo con `Database:ApplyMigrations=true`, preferentemente desde Backend; nunca activar esa opción simultáneamente en Frontend y Backend.
- El seeder debe ser idempotente y nunca contener secretos.
- Hacer backup de SQLite antes de migrar en Raspberry Pi.

## Audio Linux

Usar las interfaces existentes:

- `IAudioPlayer` para reproducción.
- `IVolumeController` para volumen.
- `IExternalProcessRunner` para procesos.

`mpv` recibe `--no-video` y `ytsearch:<consulta>`. `amixer` recibe argumentos separados para `set` y `get`. Usar `ProcessStartInfo.ArgumentList`; nunca ejecutar una shell ni concatenar entrada de usuario en comandos. Mantener rutas de ejecutables en configuración `Audio`. Los tests deben usar fakes y no requerir que `mpv`, `yt-dlp` o `amixer` estén instalados.

## Configuración y secretos

No añadir secretos a JSON, documentación, fixtures ni logs. Usar variables de entorno o un archivo protegido fuera del repositorio:

- `ConnectionStrings__SharedDatabase`
- `OAuth__ClientId`
- `OAuth__ClientSecret`
- `OAuth__AllowedRedirectUris__0`
- `InitialAdmin__Username`
- `InitialAdmin__Password`
- `InitialAdmin__AgentUserId`
- `Backend__BaseUrl`
- `Audio__MpvPath`, `Audio__YtDlpPath`, `Audio__AmixerPath`, `Audio__MixerName`

No registrar contraseñas, códigos OAuth, access tokens, refresh tokens ni headers `Authorization`. Mantener `data/`, `*.db`, `bin/` y `obj/` fuera del control de versiones.

## Flujo de trabajo

1. Leer la tarea relevante en [tareas.md](../tareas.md) y revisar sus dependencias.
2. Localizar primero el código que decide el comportamiento; evitar exploración amplia innecesaria.
3. Realizar el cambio mínimo compatible con la arquitectura existente.
4. Añadir o actualizar pruebas para cada cambio de comportamiento.
5. Pasar `CancellationToken` a operaciones de EF, HTTP y procesos externos.
6. Ejecutar validación focalizada inmediatamente después del cambio.
7. Antes de terminar, ejecutar el build y las pruebas aplicables.
8. No editar `bin/` ni `obj/`, no crear commits ni ramas, y no revertir cambios ajenos.

## Comandos

Ejecutar desde la raíz en PowerShell:

```powershell
dotnet restore
dotnet build SmartHome.slnx
dotnet test SmartHome.slnx
dotnet ef migrations add <MigrationName> --project SmartHome.Shared\SmartHome.Shared.csproj --output-dir Migrations
dotnet ef database update --project SmartHome.Shared\SmartHome.Shared.csproj
dotnet publish SmartHome.Backend\SmartHome.Backend.csproj -c Release -r linux-arm64 --self-contained false
dotnet publish SmartHome.Frontend\SmartHome.Frontend.csproj -c Release -r linux-arm64 --self-contained false
```

Para cambios limitados, ejecutar primero el proyecto de pruebas afectado y después ampliar a `dotnet test SmartHome.slnx` cuando se modifiquen contratos, autenticación, persistencia o pipeline HTTP.

## Definición de terminado

Un cambio está terminado cuando:

- Compila con `dotnet build SmartHome.slnx`.
- Las pruebas relevantes pasan.
- Los contratos Google Home permanecen compatibles.
- No se introducen secretos ni rutas específicas de una máquina.
- Se actualiza documentación o configuración si cambia una variable, endpoint, migración o procedimiento operativo.
