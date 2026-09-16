# AGENTS.md

## Propósito

Este repositorio implementa Smart Home Media Bridge: una solución .NET 10 que expone un reproductor multimedia de Raspberry Pi a Google Home. Estas instrucciones describen la tecnología, la estructura real y las reglas que deben seguir los agentes antes de modificar código.

La especificación funcional completa está en [Especificacion_SmartHome_MediaBridge.md](Especificacion_SmartHome_MediaBridge.md). El desglose ejecutable y el estado de implementación están en [tareas.md](tareas.md). La guía operativa de desarrollo y despliegue está en [README.md](README.md). Consultar esos documentos para contratos completos; no duplicarlos aquí.

## Tecnología

- Target framework: `net10.0` en todos los proyectos.
- Solución: `SmartHome.slnx`, formato moderno de solución de .NET 10.
- Backend: ASP.NET Core Web API con controladores, OpenAPI en Development, ProblemDetails, CORS configurable y autenticación Bearer local.
- Frontend: ASP.NET Core Blazor Web App con renderizado estático del lado del servidor, Bootstrap 5.3.3 y autenticación por cookie para administración local.
- Shared: biblioteca .NET con Entity Framework Core 10.0.12, proveedor SQLite, migraciones y contratos `System.Text.Json`.
- Persistencia: SQLite compartido por Frontend y Backend mediante `IDbContextFactory<SmartHomeDbContext>`.
- Identidad administrativa: `PasswordHasher<Usuario>` de ASP.NET Core y cookie `SmartHome.Admin`.
- OAuth: authorization code y refresh token propios, almacenados en SQLite y validados con opciones configurables.
- Audio Linux: `System.Diagnostics.Process` con `ProcessStartInfo.ArgumentList`; no se usa shell.
- Reproductor: `mpv --no-video ytsearch:<consulta>`; `yt-dlp` debe estar instalado para que mpv resuelva búsquedas.
- Volumen: `amixer set <mixer> <porcentaje>%` y `amixer get <mixer>`.
- Pruebas: xUnit, `Microsoft.NET.Test.Sdk`, SQLite temporal y `WebApplicationFactory` para integración HTTP.
- Plataforma de desarrollo verificada: Windows con SDK .NET 10.0.401. El despliegue objetivo es Linux ARM64 en Raspberry Pi.

## Estructura de proyectos

```text
MiniGoogleHome/
|-- SmartHome.slnx
|-- AGENTS.md
|-- README.md
|-- Especificacion_SmartHome_MediaBridge.md
|-- tareas.md
|-- .gitignore
|-- deploy/
|   |-- smarthome.env.example
|   |-- smarthome-backend.service
|   `-- smarthome-frontend.service
|-- data/                         # SQLite local; ignorado por Git
|-- SmartHome.Shared/
|   |-- Entities/                 # Entidades EF: Usuario, OauthCode, OauthToken, HistorialReproduccion
|   |-- Persistence/              # DbContext, factory de diseño, migraciones, seeder y bootstrap
|   |-- Contracts/                # DTOs y constantes JSON de Google Home
|   |-- Configuration/            # OAuthOptions
|   `-- Migrations/
|-- SmartHome.Backend/
|   |-- Authentication/           # LocalBearerAuthenticationHandler
|   |-- Configuration/            # AudioOptions
|   |-- Contracts/                # DTOs del endpoint OAuth token
|   |-- Controllers/              # OAuth, SmartHome y estado de volumen
|   |-- Services/                 # OAuth, MediaBridge, audio y procesos externos
|   |-- Program.cs                # DI, pipeline, auth, health y bootstrap de DB
|   `-- appsettings*.json
|-- SmartHome.Frontend/
|   |-- Components/
|   |   |-- Layout/               # MainLayout
|   |   |-- Pages/                # Home, Login, Authorize, Dashboard y errores
|   |   |-- App.razor
|   |   `-- Routes.razor
|   |-- Services/                 # Login administrativo y códigos OAuth
|   |-- wwwroot/                  # CSS y assets estáticos
|   |-- Program.cs                # DI, cookie auth y endpoints de formularios
|   `-- appsettings*.json
|-- SmartHome.Shared.Tests/       # Unitarias y pruebas de contratos/servicios
|-- SmartHome.Backend.Tests/      # Integración HTTP del Backend
`-- SmartHome.Frontend.Tests/     # Integración HTTP OAuth del Frontend
```

`bin/` y `obj/` son artefactos de compilación y no deben editarse. `data/` contiene bases locales y nunca debe versionarse. Los archivos de pruebas pueden compartir referencias a Frontend/Backend solo cuando sea necesario; los proyectos de integración están separados para evitar la colisión del tipo top-level `Program`.

## Límites de responsabilidad

### SmartHome.Shared

- Es la única ubicación para entidades EF, `SmartHomeDbContext`, configuración del modelo, migraciones y contratos Google Home reutilizables.
- `PersistenceServiceCollectionExtensions.AddSmartHomePersistence` lee `ConnectionStrings:SharedDatabase`, crea el directorio padre y registra `IDbContextFactory` con SQLite.
- `SmartHomeDatabaseBootstrapper` solo debe ejecutarse desde Backend cuando `Database:ApplyMigrations=true`.
- `SmartHomeDatabaseSeeder` es idempotente y no debe contener secretos.
- Las entidades no deben exponerse directamente desde controladores; usar DTOs de `Contracts`.

### SmartHome.Backend

- Es una API sin sesión de usuario administrativa. Google se autentica con `Authorization: Bearer` y el esquema `LocalBearerAuthenticationHandler`.
- `POST /oauth/token` acepta `application/x-www-form-urlencoded` para `authorization_code` y `refresh_token`.
- `POST /api/smarthome` es el webhook protegido y conserva `requestId`.
- `GET /health` comprueba aplicación y conexión SQLite.
- `GET /api/status/volume` lo consume el dashboard local; debe permanecer detrás de la red local o un proxy que controle el acceso.
- `OAuthTokenService` consume códigos una sola vez dentro de una transacción y rota refresh tokens.
- `MediaBridgeService` coordina reproducción, volumen, historial y cancelación; no se debe acceder a SQLite desde los adaptadores de procesos.
- `IAudioPlayer`, `IVolumeController` e `IExternalProcessRunner` son las fronteras para fakes de prueba.
- Los ejecutables Linux y sus rutas vienen de `Audio` en configuración; nunca concatenar una consulta de usuario en una shell.

### SmartHome.Frontend

- Es la interfaz administrativa SSR. Las páginas son estáticas por defecto; no introducir interactividad Blazor sin necesidad.
- Usa cookie `SmartHome.Admin` para la sesión local. Esa cookie nunca sustituye al Bearer de Google.
- Rutas principales:
  - `GET /`: portada.
  - `GET /login` y `POST /login`: sesión administrativa local.
  - `GET /oauth/authorize`: pantalla SSR de autorización.
  - `POST /oauth/authorize/submit`: valida credenciales, allowlist y genera el código OAuth.
  - `GET /dashboard`: página protegida con tokens enmascarados, historial, health y volumen.
  - `POST /logout`: cierra la cookie administrativa.
- `Backend:BaseUrl` configura el `HttpClient` nombrado `Backend` usado por el dashboard.
- El formulario SSR debe conservar antiforgery y no escribir tokens/contraseñas en HTML o logs.

## Contratos que no deben cambiarse accidentalmente

- Device id: `pi_media_speaker_01`.
- Agent user inicial: `usr_master_pi_01`.
- Nombre: `Parlante Raspberry`.
- Tipo: `action.devices.types.SPEAKER`.
- Traits: `MediaState` y `Volume` de Google Home.
- Fabricante/modelo: `Niquelsoft` / `PiMediaBridgeV1`.
- Comandos: `action.devices.commands.mediaPlay` y `action.devices.commands.setVolume`.
- Valores de estado: `PLAYING`, `SUCCESS`, `ERROR`, `online`, `currentVolume`.
- Los DTOs se serializan con `GoogleHomeJson.Options`, camelCase y omisión de null.

Si se modifica un identificador, actualizar la especificación, las pruebas de contrato y cualquier configuración de Google Home en el mismo cambio.

## Persistencia y rutas SQLite

- Desarrollo: `data/smarthome.db` en la raíz; las aplicaciones usan `Data Source=../data/smarthome.db` desde sus directorios.
- Raspberry Pi: usar una ruta absoluta, normalmente `/var/lib/smarthome-media-bridge/smarthome.db`.
- La ruta debe venir de `ConnectionStrings:SharedDatabase`; no añadir rutas absolutas de la máquina del desarrollador al código.
- Fechas persistidas y comparaciones de expiración usan UTC.
- No usar `EnsureCreated` en producción; las migraciones se aplican mediante `dotnet ef` o el bootstrap explícito del Backend.
- No activar `Database__ApplyMigrations=true` en Frontend y Backend a la vez.
- Antes de una migración o actualización en Raspberry Pi, realizar backup de SQLite.

## Configuración y secretos

Los JSON contienen valores de desarrollo vacíos o seguros. Los secretos reales deben entrar por variables de entorno o un archivo protegido fuera del repositorio:

- `ConnectionStrings__SharedDatabase`
- `OAuth__ClientId`
- `OAuth__ClientSecret`
- `OAuth__AllowedRedirectUris__0`
- `InitialAdmin__Username`
- `InitialAdmin__Password`
- `InitialAdmin__AgentUserId`
- `Backend__BaseUrl` para Frontend
- `Audio__MpvPath`, `Audio__YtDlpPath`, `Audio__AmixerPath`, `Audio__MixerName`

No imprimir contraseñas, client secrets, códigos OAuth, access tokens, refresh tokens ni headers `Authorization`. Aunque los tokens se almacenan actualmente en los campos SQLite definidos por la especificación, el archivo de datos debe tener permisos restrictivos.

## Comandos verificados

Ejecutar desde la raíz del repositorio en PowerShell:

```powershell
dotnet restore
dotnet build SmartHome.slnx
dotnet test SmartHome.slnx
dotnet test SmartHome.Shared.Tests\SmartHome.Shared.Tests.csproj
dotnet test SmartHome.Backend.Tests\SmartHome.Backend.Tests.csproj
dotnet test SmartHome.Frontend.Tests\SmartHome.Frontend.Tests.csproj
dotnet ef migrations add <MigrationName> --project SmartHome.Shared\SmartHome.Shared.csproj --output-dir Migrations
dotnet ef database update --project SmartHome.Shared\SmartHome.Shared.csproj
dotnet publish SmartHome.Backend\SmartHome.Backend.csproj -c Release -r linux-arm64 --self-contained false
dotnet publish SmartHome.Frontend\SmartHome.Frontend.csproj -c Release -r linux-arm64 --self-contained false
```

La suite completa usa dobles para `mpv`, `yt-dlp` y `amixer`; no instalar ni ejecutar esos binarios para validar cambios unitarios. Para cambios de contrato o pipeline, ejecutar las pruebas de integración de Backend y Frontend además de `dotnet test SmartHome.slnx`.

## Reglas de implementación

1. Leer primero la tarea correspondiente en [tareas.md](tareas.md) y comprobar sus dependencias.
2. Mantener nullable reference types, `ImplicitUsings` y APIs públicas existentes.
3. Preferir cambios pequeños dentro del proyecto responsable; no mover entidades, contratos o servicios entre proyectos sin actualizar referencias y pruebas.
4. Pasar `CancellationToken` a EF Core, HTTP y procesos externos.
5. Usar `IDbContextFactory` en servicios y trabajo concurrente; no compartir un DbContext entre peticiones.
6. Validar límites de consultas, volumen 0-100, expiración, un solo uso y allowlist de redirect URI.
7. Añadir o actualizar una prueba junto con cada cambio de comportamiento.
8. Mantener los adaptadores Linux detrás de interfaces y argumentos separados.
9. No editar archivos generados en `bin/` u `obj/`.
10. Antes de terminar, ejecutar el build y las pruebas aplicables; ante un fallo, reparar la causa en el mismo slice y repetir la comprobación.
11. No crear commits ni ramas automáticamente.
12. No modificar archivos ajenos al alcance de la tarea ni reemplazar cambios existentes del usuario.

## Criterio de finalización

Un cambio está listo cuando compila con `dotnet build SmartHome.slnx`, las pruebas relevantes pasan, los contratos Google Home permanecen compatibles, no aparecen secretos en el diff y la configuración/documentación refleja cualquier nueva variable, ruta o dependencia.
