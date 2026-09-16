# Plan de implementación: Smart Home Media Bridge

Este archivo divide `Especificacion_SmartHome_MediaBridge.md` en fases y tareas ejecutables por agentes independientes. Cada tarea debe producir un cambio verificable y dejar el repositorio en un estado compilable o explicar claramente el bloqueo encontrado.

## Contexto común

### Objetivo del sistema
Construir una solución .NET 10 para Raspberry Pi que exponga un parlante multimedia a Google Home. La solución tendrá:

- `SmartHome.Frontend`: aplicación Blazor SSR para administración local y autorización OAuth 2.0.
- `SmartHome.Backend`: Web API REST sin estado para OAuth y el webhook de Google Home.
- `SmartHome.Shared`: entidades, `DbContext`, contratos compartidos y configuración común.
- SQLite: un único archivo de base de datos accesible por Frontend y Backend.

### Identificadores y valores contractuales
Estos valores son parte del contrato y no deben cambiarse sin actualizar todas las tareas y pruebas:

- Dispositivo: `pi_media_speaker_01`.
- `agentUserId` inicial: `usr_master_pi_01`.
- Nombre visible: `Parlante Raspberry`.
- Tipo: `action.devices.types.SPEAKER`.
- Traits: `action.devices.traits.MediaState` y `action.devices.traits.Volume`.
- Fabricante: `Niquelsoft`.
- Modelo: `PiMediaBridgeV1`.
- Versión de hardware: `Raspberry Pi`.
- Versión de software: `1.0.0`.
- Base de datos: SQLite compartido, configurable por `ConnectionStrings:SharedDatabase`.
- URL de autorización: `GET /oauth/authorize`.
- URL de intercambio: `POST /oauth/token`.
- Webhook: `POST /api/smarthome`.

### Reglas para todos los agentes

1. Usar .NET 10 y nullable reference types.
2. Mantener los nombres públicos indicados en este documento, salvo que exista una razón técnica documentada.
3. No almacenar contraseñas en texto plano. Usar el mecanismo de hashing de contraseñas de ASP.NET Core o equivalente seguro.
4. No registrar en logs contraseñas, `access_token`, `refresh_token` ni códigos OAuth completos.
5. Validar entrada, expiración, estado de uso y asociación de usuario en todos los flujos OAuth.
6. Usar `CancellationToken` en operaciones de base de datos, red y procesos externos.
7. Usar UTC para fechas persistidas y comparaciones de expiración.
8. Los cambios deben incluir pruebas automatizadas o una prueba manual reproducible cuando la integración no pueda ejecutarse en el entorno del agente.
9. No asumir que `mpv`, `yt-dlp` o `amixer` existen en el equipo de desarrollo. La integración debe ser configurable y testeable con ejecutables simulados.
10. Antes de terminar, ejecutar al menos `dotnet build` y las pruebas aplicables.

## Dependencias entre fases

```text
Fase 1 -> Fase 2 -> Fase 3 -> Fase 4 -> Fase 5
                    |             |
                    +-------------+
```

La Fase 3 puede empezar cuando existan las entidades y el contrato compartido de la Fase 1. La Fase 4 depende de que el Backend tenga un servicio invocable para reproducir y ajustar volumen, aunque puede implementarse con dobles de prueba. La Fase 5 valida el sistema completo.

# Fase 1: Solución, contratos y persistencia

## Tarea 1.1: Crear la solución .NET 10

**Agente responsable:** infraestructura .NET.

**Estado:** Terminada.

**Objetivo:** crear la solución base y los tres proyectos requeridos.

**Especificación completa:**

- Crear una solución `SmartHome.sln` o equivalente.
- Crear `SmartHome.Frontend` como aplicación Blazor Web App configurada para renderizado estático del lado del servidor.
- Crear `SmartHome.Backend` como ASP.NET Core Web API.
- Crear `SmartHome.Shared` como biblioteca de clases.
- Agregar referencias desde Frontend y Backend hacia Shared.
- Habilitar nullable reference types y `ImplicitUsings`.
- Configurar puertos mediante `appsettings.Development.json` o perfiles de lanzamiento, sin hardcodear direcciones de producción.
- Agregar los paquetes EF Core SQLite, EF Core Design y las dependencias de autenticación necesarias en los proyectos que las utilicen.
- Mantener una solución que compile sin funcionalidad de negocio.

**Entregables:** archivos de solución, tres proyectos, referencias y configuración inicial.

**Criterios de aceptación:**

- `dotnet restore` termina correctamente.
- `dotnet build` termina sin errores.
- Frontend y Backend pueden iniciarse por separado.
- Shared puede ser referenciado por ambos proyectos.

**Pruebas:** ejecutar `dotnet restore` y `dotnet build` desde la raíz.

**Dependencias:** ninguna.

## Tarea 1.2: Definir entidades de persistencia

**Agente responsable:** modelo de datos.

**Estado:** Terminada.

**Objetivo:** implementar las cuatro entidades SQLite descritas en la especificación.

**Especificación completa:** crear en `SmartHome.Shared`:

```csharp
public class Usuario
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string AgentUserId { get; set; } = string.Empty;
}

public class OauthCode
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string AgentUserId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
}

public class OauthToken
{
    public int Id { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string AgentUserId { get; set; } = string.Empty;
    public DateTime AccessExpiresAt { get; set; }
}

public class HistorialReproduccion
{
    public int Id { get; set; }
    public string QueryTexto { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public bool Exitoso { get; set; }
}
```

Configurar longitudes razonables, campos obligatorios, índices únicos para `Usuario.Username`, `Usuario.AgentUserId`, `OauthCode.Code` y `OauthToken.AccessToken`, y valores máximos para textos controlados. No guardar tokens hasheados en esta tarea si el diseño de autenticación todavía no está decidido; documentar la decisión para la Tarea 3.3.

**Entregables:** clases de entidad y pruebas de validación/configuración.

**Criterios de aceptación:**

- Las cuatro entidades están en Shared.
- `Username`, `AgentUserId`, `Code`, tokens y `QueryTexto` no aceptan valores nulos.
- Los identificadores contractuales pueden persistirse sin truncamiento.
- La configuración evita duplicados de usuario, código y access token.

**Pruebas:** probar la configuración del modelo con `ModelBuilder` y casos válidos e inválidos.

**Dependencias:** Tarea 1.1.

## Tarea 1.3: Crear el DbContext compartido

**Agente responsable:** persistencia EF Core.

**Estado:** Terminada.

**Objetivo:** permitir que Frontend y Backend accedan al mismo archivo SQLite.

**Especificación completa:**

- Crear `SmartHomeDbContext` en Shared.
- Exponer `DbSet<Usuario>`, `DbSet<OauthCode>`, `DbSet<OauthToken>` y `DbSet<HistorialReproduccion>`.
- Configurar SQLite usando `ConnectionStrings:SharedDatabase`.
- Registrar el contexto con DI en Frontend y Backend.
- Usar `IDbContextFactory<SmartHomeDbContext>` para operaciones que puedan ejecutarse fuera del ciclo normal de una petición, especialmente servicios hospedados.
- Permitir una ruta configurable para Raspberry Pi, por ejemplo `/var/lib/smarthome-media-bridge/smarthome.db`, y una ruta local de desarrollo.
- Crear el directorio de la base de datos al iniciar mediante una operación segura y documentada, sin sobrescribir un archivo existente.
- Configurar fechas en UTC y relaciones/índices del modelo.

**Entregables:** DbContext, registro DI, configuración de conexión y documentación de configuración.

**Criterios de aceptación:**

- Ambos procesos pueden resolver el mismo tipo de contexto.
- La ruta de conexión se obtiene de configuración, no de una constante de código.
- Una prueba crea una base temporal, inserta y lee una entidad.
- El arranque no elimina ni recrea una base existente.

**Pruebas:** prueba de integración con SQLite temporal y `EnsureCreated` solo en el entorno de prueba.

**Dependencias:** Tarea 1.2.

## Tarea 1.4: Crear la migración inicial y datos mínimos

**Agente responsable:** infraestructura de base de datos.

**Estado:** Terminada.

**Objetivo:** versionar el esquema y dejar disponible el usuario inicial de administración.

**Especificación completa:**

- Instalar o configurar `dotnet-ef` si hace falta.
- Crear la migración `InitialCreate`.
- Aplicar migraciones al arranque solo mediante una opción explícita de configuración, evitando que dos procesos migren simultáneamente sin coordinación.
- Implementar un seeder idempotente para un usuario inicial configurable. Nunca incluir una contraseña real en el repositorio.
- Generar `AgentUserId` como `usr_master_pi_01` por defecto si no se proporciona otro valor.
- Documentar cómo establecer la contraseña inicial mediante variable de entorno o secreto local.
- No insertar tokens OAuth ni códigos de autorización en el seeder.

**Entregables:** carpeta de migraciones, seeder y documentación de instalación.

**Criterios de aceptación:**

- `dotnet ef database update` crea las cuatro tablas.
- Ejecutar el seeder dos veces no duplica el usuario.
- No hay secretos en el control de versiones.
- La migración puede aplicarse a una base vacía y a una base ya migrada.

**Pruebas:** aplicar la migración a una SQLite temporal y verificar tablas, índices y usuario único.

**Dependencias:** Tarea 1.3.

## Tarea 1.5: Definir contratos compartidos de Google Home

**Agente responsable:** contratos API.

**Estado:** Terminada.

**Objetivo:** evitar JSON anónimo duplicado entre controladores y pruebas.

**Especificación completa:** crear DTOs serializables para solicitudes y respuestas de `SYNC`, `QUERY` y `EXECUTE`. Deben soportar:

- Solicitud con `requestId` e `inputs`.
- `inputs[].intent` con `action.devices.SYNC`, `action.devices.QUERY` y `action.devices.EXECUTE`.
- `EXECUTE` con `commands[].devices[].id` y `commands[].execution[]`.
- Comando `action.devices.commands.mediaPlay` con `params.mediaQuery.query`.
- Comando `action.devices.commands.setVolume` con `params.volumeLevel`.
- Respuesta de éxito con `requestId`, `payload.commands[].ids`, `status` y `states`.
- Respuesta de `SYNC` con el dispositivo y metadatos contractuales de este documento.
- Respuestas de error compatibles con el formato del webhook y sin detalles internos.

Configurar `System.Text.Json` para nombres camelCase. Los DTOs deben rechazar o manejar de forma segura campos ausentes, tipos incorrectos y comandos desconocidos.

**Entregables:** DTOs, opciones JSON y pruebas de serialización/deserialización usando los ejemplos de esta especificación.

**Criterios de aceptación:**

- Los tres JSON de entrada de referencia se deserializan correctamente.
- Las respuestas serializadas conservan `requestId` y producen camelCase.
- Un comando desconocido no provoca una excepción no controlada.
- Los DTOs no exponen entidades EF directamente.

**Pruebas:** pruebas unitarias de cada JSON de referencia y de entradas incompletas.

**Dependencias:** Tarea 1.1.

# Fase 2: Frontend administrativo y autorización

## Tarea 2.1: Configurar Blazor SSR y Bootstrap 5

**Agente responsable:** frontend.

**Estado:** Terminada.

**Objetivo:** dejar una interfaz administrativa navegable y responsive.

**Especificación completa:**

- Configurar `SmartHome.Frontend` como Blazor SSR con páginas estáticas por defecto.
- Agregar Bootstrap 5 de forma versionada, preferentemente mediante paquete o assets locales reproducibles.
- Crear layout, navegación y manejo de errores.
- Configurar la URL del Backend mediante opciones (`Backend:BaseUrl`).
- Registrar `SmartHomeDbContext` o `IDbContextFactory` para lecturas administrativas.
- Implementar una página inicial que enlace a autorización y dashboard.
- No presentar tokens completos ni secretos en texto visible.

**Entregables:** aplicación Blazor navegable, layout Bootstrap y configuración documentada.

**Criterios de aceptación:**

- `dotnet run` sirve la aplicación.
- La página se visualiza correctamente en móvil y escritorio.
- No hay dependencia de CDN no documentada para que la interfaz básica funcione.
- Los errores de carga muestran un mensaje útil sin stack trace.

**Pruebas:** build y prueba manual de navegación en las rutas principales.

**Dependencias:** Tarea 1.1 y Tarea 1.3.

## Tarea 2.2: Implementar sesión administrativa local

**Agente responsable:** autenticación frontend.

**Estado:** Terminada.

**Objetivo:** proteger el dashboard y permitir validar credenciales locales.

**Especificación completa:**

- Crear login administrativo con `Username` y contraseña.
- Validar la contraseña contra `Usuario.PasswordHash` usando un verificador seguro.
- Crear una cookie de sesión protegida, HttpOnly, Secure cuando corresponda y con SameSite adecuado.
- Definir expiración y cierre de sesión.
- Proteger el dashboard y cualquier operación administrativa.
- Evitar revelar si el usuario existe; usar un mensaje de credenciales inválidas.
- No usar la sesión administrativa como bearer token para Google.

**Entregables:** página/login, servicio de autenticación, autorización de rutas y logout.

**Criterios de aceptación:**

- Credenciales correctas crean sesión y permiten entrar al dashboard.
- Credenciales incorrectas no crean sesión.
- Un usuario anónimo recibe redirección al login al solicitar el dashboard.
- La cookie no contiene contraseña ni tokens OAuth.
- Logout invalida la sesión.

**Pruebas:** pruebas de servicio para hash/verificación y pruebas de integración para acceso anónimo, válido e inválido.

**Dependencias:** Tareas 1.2, 1.3 y 2.1.

## Tarea 2.3: Implementar `GET /oauth/authorize`

**Agente responsable:** flujo OAuth en Frontend.

**Estado:** Terminada.

**Objetivo:** autenticar al propietario y emitir un código temporal para Google.

**Especificación completa:**

- Exponer `GET /oauth/authorize` con parámetros OAuth relevantes: `client_id`, `redirect_uri`, `state`, `response_type` y el alcance recibido.
- Mostrar formulario de usuario/contraseña cuando no exista una sesión local válida.
- Tras autenticación correcta, generar un código criptográficamente aleatorio, de un solo uso y con expiración breve configurable.
- Asociar el código al `AgentUserId` del usuario autenticado y persistirlo en `OauthCodes`.
- Validar `redirect_uri` contra una allowlist configurada; nunca redirigir a una URL arbitraria.
- Validar `client_id` y `response_type` según la configuración soportada.
- Redirigir al `redirect_uri` con `code` y conservar `state` sin modificar cuando fue enviado.
- Marcar el código como no usado al crearlo. La marca de usado la realizará el endpoint de token dentro de una operación atómica.
- No incluir credenciales, hash ni tokens en la URL o en logs.

**Entregables:** endpoint/página de autorización, opciones OAuth, servicio generador de códigos y validaciones.

**Criterios de aceptación:**

- Usuario no autenticado ve el formulario.
- Usuario autenticado con parámetros válidos recibe una redirección con `code` y `state`.
- `redirect_uri`, `client_id` o `response_type` no válidos generan error sin redirección externa.
- El código expira y solo puede usarse una vez.
- Dos solicitudes concurrentes no pueden emitir un código reutilizado de forma inconsistente.

**Pruebas:** pruebas de parámetros válidos, inválidos, expiración, reutilización, allowlist y preservación de `state`.

**Dependencias:** Tareas 1.2, 1.3, 1.4, 2.2.

## Tarea 2.4: Construir el dashboard administrativo

**Agente responsable:** frontend administrativo.

**Estado:** Terminada.

**Objetivo:** mostrar estado, tokens y actividad de reproducción.

**Especificación completa:** crear un dashboard protegido que muestre:

- Estado de conexión del Backend mediante un endpoint de health check.
- Tokens OAuth actualmente válidos, mostrando solo información segura: `AgentUserId`, expiración y estado; enmascarar access y refresh token.
- Historial reciente de `HistorialReproduccion` ordenado por `Fecha` descendente, con texto de consulta, fecha UTC y resultado.
- Estado actual del volumen de la Raspberry mediante un endpoint del Backend o un valor `No disponible` si el sistema no puede consultarlo.
- Estados de carga, vacío y error.
- Paginación o límite configurable para no cargar un historial ilimitado.

**Entregables:** página dashboard, consultas, modelos de vista y health check usado por la interfaz.

**Criterios de aceptación:**

- Un usuario sin sesión no puede consultar la página.
- El dashboard no revela tokens completos.
- Los registros aparecen ordenados y con fechas consistentes.
- La caída del Backend no rompe toda la página: se muestra el estado de error.
- La UI funciona en viewport móvil sin solapamientos.

**Pruebas:** pruebas de consultas y prueba manual con base vacía, datos válidos y Backend no disponible.

**Dependencias:** Tareas 1.3, 2.2 y al menos el health endpoint de la Tarea 3.1.

# Fase 3: Backend OAuth y webhook de Google

## Tarea 3.1: Configurar la Web API y endpoints operativos

**Agente responsable:** backend .NET.

**Estado:** Terminada.

**Objetivo:** preparar el servidor REST y sus controles básicos.

**Especificación completa:**

- Configurar `SmartHome.Backend` como Web API .NET 10.
- Registrar DbContext, opciones, logging, serialización camelCase y controladores.
- Agregar `GET /health` sin autenticación para indicar disponibilidad y conectividad básica de la aplicación.
- Configurar CORS únicamente para el origen administrativo necesario; no usar `AllowAnyOrigin` con credenciales.
- Habilitar HTTPS en producción y documentar el proxy TLS si la Raspberry se publica detrás de otro servidor.
- Usar respuestas JSON consistentes para errores y códigos HTTP adecuados.
- No exponer detalles de excepción en producción.

**Entregables:** arranque Backend, health check, configuración y manejo de errores.

**Criterios de aceptación:**

- `GET /health` devuelve 200 cuando la aplicación está activa.
- La conexión a SQLite puede comprobarse sin filtrar rutas ni secretos.
- Un error no controlado produce respuesta genérica y queda registrado de forma segura.
- `dotnet build` pasa.

**Pruebas:** pruebas de integración para health, serialización y errores.

**Dependencias:** Tareas 1.1 y 1.3.

## Tarea 3.2: Implementar `POST /oauth/token`

**Agente responsable:** backend OAuth.

**Estado:** Terminada.

**Objetivo:** intercambiar un código autorizado por tokens para Google.

**Especificación completa:**

- Aceptar `application/x-www-form-urlencoded` con `grant_type`, `code`, `client_id`, `client_secret` y, cuando corresponda, `redirect_uri`.
- Soportar `authorization_code` y devolver error OAuth estándar para grants no soportados.
- Buscar el código por valor, comprobar expiración, `IsUsed`, `client_id` y `redirect_uri` asociados/configurados.
- Consumir el código de forma atómica para impedir doble uso concurrente.
- Generar `access_token` y `refresh_token` criptográficamente aleatorios.
- Persistir `OauthToken` con `AgentUserId` y expiración configurable.
- Devolver `token_type: Bearer`, `access_token`, `refresh_token` y `expires_in`.
- Para `grant_type=refresh_token`, validar el refresh token, rotarlo si la política elegida lo requiere y emitir un nuevo access token.
- Usar HTTP 400 para solicitudes OAuth inválidas y no revelar si un código existe.
- Enmascarar tokens en logs.

**Entregables:** endpoint, servicio de intercambio, opciones OAuth y pruebas.

**Criterios de aceptación:**

- Código válido produce respuesta OAuth estructurada.
- Código expirado, inexistente o usado una segunda vez produce error.
- La reutilización concurrente no genera dos intercambios exitosos.
- Refresh token inválido no crea un access token.
- Los tokens emitidos quedan asociados al `AgentUserId` correcto.

**Pruebas:** pruebas unitarias y de integración para authorization code, doble uso, expiración, cliente inválido y refresh.

**Dependencias:** Tareas 1.3, 1.4 y 2.3.

## Tarea 3.3: Implementar autenticación Bearer local

**Agente responsable:** seguridad del Backend.

**Estado:** Terminada.

**Objetivo:** autenticar las peticiones de Google usando tokens almacenados en SQLite.

**Especificación completa:**

- Crear un `AuthenticationHandler` o esquema equivalente para `Authorization: Bearer <token>`.
- Buscar el access token persistido y comprobar expiración en UTC.
- Asociar la identidad autenticada al `AgentUserId`.
- Aplicar `[Authorize]` al webhook y a los endpoints que lo requieran.
- Rechazar ausencia de header, esquema incorrecto, token vacío, token desconocido o token expirado con 401.
- No registrar el valor del header.
- Preferir almacenar una huella criptográfica del token en lugar del valor plano; si se conserva el valor plano para compatibilidad con la entidad dada, documentar el riesgo y restringir permisos del archivo SQLite.

**Entregables:** esquema Bearer, claims mínimos, configuración de autorización y pruebas.

**Criterios de aceptación:**

- Token válido autentica y expone `AgentUserId` al controlador.
- Token expirado o inexistente recibe 401.
- Una petición autenticada no puede hacerse pasar por otro usuario.
- El endpoint protegido no es accesible con cookie administrativa.

**Pruebas:** matriz de casos 200/401 y prueba de expiración.

**Dependencias:** Tarea 3.2.

## Tarea 3.4: Implementar respuesta `SYNC`

**Agente responsable:** webhook Google Home.

**Estado:** Terminada.

**Objetivo:** permitir que Google descubra el parlante.

**Especificación completa:** para `action.devices.SYNC`, devolver exactamente la estructura equivalente a:

```json
{
  "requestId": "4478392110293811",
  "payload": {
    "agentUserId": "usr_master_pi_01",
    "devices": [
      {
        "id": "pi_media_speaker_01",
        "type": "action.devices.types.SPEAKER",
        "traits": [
          "action.devices.traits.MediaState",
          "action.devices.traits.Volume"
        ],
        "name": {
          "name": "Parlante Raspberry",
          "defaultNames": ["Reproductor de la Pi"],
          "nicknames": ["Audio de la Pi"]
        },
        "willReportState": false,
        "deviceInfo": {
          "manufacturer": "Niquelsoft",
          "model": "PiMediaBridgeV1",
          "hwVersion": "Raspberry Pi",
          "swVersion": "1.0.0"
        }
      }
    ]
  }
}
```

El `agentUserId` debe obtenerse de la identidad autenticada o del usuario asociado al token, con `usr_master_pi_01` como dato inicial. No devolver dispositivos de otro usuario.

**Entregables:** rama de procesamiento SYNC y pruebas de contrato.

**Criterios de aceptación:** requestId idéntico al recibido, dispositivo completo, traits correctos, camelCase y 401 sin bearer válido.

**Pruebas:** snapshot o comparación estructural del JSON de referencia.

**Dependencias:** Tareas 1.5, 3.1 y 3.3.

## Tarea 3.5: Implementar `QUERY`

**Agente responsable:** webhook Google Home.

**Estado:** Terminada.

**Objetivo:** responder al estado actual del dispositivo cuando Google lo solicite.

**Especificación completa:**

- Aceptar `action.devices.QUERY` con uno o más dispositivos.
- Validar que cada id pertenece al conjunto soportado.
- Devolver estado de conexión y, si están disponibles, `currentVolume` y `playbackState`.
- Usar el estado `OFFLINE` o el error compatible con Google cuando el dispositivo o servicio no esté disponible.
- No inventar un volumen si `amixer` no puede consultarlo.
- Mantener el `requestId` recibido.

**Entregables:** rama QUERY, proveedor de estado y pruebas.

**Criterios de aceptación:** dispositivo conocido produce estado válido; id desconocido produce error por dispositivo; backend sin audio no se marca falsamente como operativo.

**Pruebas:** estados online, offline, volumen disponible, volumen no disponible e id desconocido.

**Dependencias:** Tareas 1.5, 3.1, 3.3 y 4.3.

## Tarea 3.6: Implementar `EXECUTE` y `mediaPlay`

**Agente responsable:** webhook Google Home.

**Estado:** Terminada.

**Objetivo:** procesar el comando de reproducción y responder a Google.

**Especificación completa:**

- Aceptar `action.devices.EXECUTE`.
- Recorrer `commands`, dispositivos y ejecuciones sin asumir un único elemento.
- Validar `pi_media_speaker_01` y rechazar ids no soportados.
- Para `action.devices.commands.mediaPlay`, extraer `params.mediaQuery.query` y rechazar consultas vacías o excesivamente largas.
- Registrar `HistorialReproduccion` con el texto, UTC y resultado.
- Invocar el servicio de audio sin bloquear innecesariamente el request thread.
- Responder éxito con:

```json
{
  "requestId": "1198273645524312",
  "payload": {
    "commands": [
      {
        "ids": ["pi_media_speaker_01"],
        "status": "SUCCESS",
        "states": {"playbackState": "PLAYING"}
      }
    ]
  }
}
```

- Si la validación o el inicio del proceso falla, registrar `Exitoso=false` y devolver el estado/error compatible, sin filtrar excepción interna.

**Entregables:** procesamiento EXECUTE, integración con el servicio de audio y pruebas.

**Criterios de aceptación:**

- La consulta `Shakira` llega intacta al servicio.
- Se conserva el requestId.
- Un comando válido genera historial y respuesta PLAYING.
- Una consulta vacía no inicia proceso.
- Un fallo del ejecutor queda auditado como no exitoso.

**Pruebas:** payload de referencia, varios comandos, consulta vacía, id desconocido y ejecutor fallido.

**Dependencias:** Tareas 1.5, 3.1, 3.3 y 4.1.

## Tarea 3.7: Implementar `EXECUTE` y `setVolume`

**Agente responsable:** webhook Google Home.

**Estado:** Terminada.

**Objetivo:** cambiar el volumen del sistema y devolver el nivel aplicado.

**Especificación completa:**

- Extraer `params.volumeLevel` como número.
- Validar rango inclusivo 0-100.
- Ejecutar el servicio de volumen con el valor validado.
- Responder éxito con:

```json
{
  "requestId": "9928374615243516",
  "payload": {
    "commands": [
      {
        "ids": ["pi_media_speaker_01"],
        "status": "SUCCESS",
        "states": {"currentVolume": 70}
      }
    ]
  }
}
```

- Rechazar NaN, decimales si la política exige entero, valores negativos y valores mayores que 100.
- No construir comandos shell concatenando entrada sin validación. Pasar argumentos como lista al proceso.
- Si `amixer` falla, responder error y no afirmar que el volumen cambió.

**Entregables:** procesamiento setVolume, validación y pruebas.

**Criterios de aceptación:** 70 produce `currentVolume: 70`; 0 y 100 son válidos; -1 y 101 son rechazados; ningún valor inválido llega al proceso.

**Pruebas:** límites, tipo incorrecto, proceso exitoso y proceso fallido.

**Dependencias:** Tareas 1.5, 3.1, 3.3 y 4.2.

# Fase 4: Integración de audio Linux

## Tarea 4.1: Crear el ejecutor de reproducción con mpv y yt-dlp

**Agente responsable:** integración Linux.

**Estado:** Terminada.

**Objetivo:** reproducir una búsqueda de YouTube en la Raspberry Pi.

**Especificación completa:**

- Crear una interfaz `IAudioPlayer` con una operación asíncrona para reproducir una consulta.
- Implementar el adaptador Linux usando `System.Diagnostics.Process`.
- Ejecutar `mpv --no-video "ytsearch:<Texto>"` con argumentos separados y sin shell.
- Configurar rutas de `mpv`, timeout, directorio de trabajo y variables mediante opciones.
- Capturar exit code, stdout/stderr limitado y cancelación.
- No aceptar opciones de proceso dentro del texto de búsqueda.
- Definir política para una reproducción activa: detener la anterior antes de iniciar una nueva o devolver conflicto; documentar la elección.
- Permitir un fake/in-memory executor para pruebas.

**Entregables:** interfaz, implementación Linux, opciones, control de proceso y pruebas con fake.

**Criterios de aceptación:**

- `Shakira` se transforma en argumentos equivalentes a `mpv`, `--no-video`, `ytsearch:Shakira`.
- El texto no se interpreta como shell.
- Exit code distinto de cero produce resultado fallido.
- Cancelación termina el proceso o lo libera correctamente.
- Las pruebas no requieren mpv instalado.

**Pruebas:** captura de argumentos, consulta con espacios/caracteres especiales, timeout, cancelación y proceso fallido.

**Dependencias:** Tarea 1.1.

## Tarea 4.2: Crear el ejecutor de volumen con amixer

**Agente responsable:** integración Linux.

**Estado:** Terminada.

**Objetivo:** controlar el volumen del sistema ALSA.

**Especificación completa:**

- Crear una interfaz `IVolumeController` para establecer y consultar volumen.
- Implementar el adaptador Linux con `amixer set Master <Porcentaje>%` usando argumentos separados.
- Configurar el nombre del mixer (`Master` por defecto) y ruta del ejecutable.
- Validar nuevamente el rango 0-100 en el servicio, aunque el controlador reciba llamadas internas.
- Capturar exit code y errores sin exponer comandos completos en logs si contienen datos sensibles.
- Implementar consulta del volumen actual para `QUERY` y dashboard cuando `amixer` lo permita.
- Proveer fake para pruebas.

**Entregables:** interfaz, implementación, configuración y pruebas.

**Criterios de aceptación:**

- 70 produce argumentos equivalentes a `set`, `Master`, `70%`.
- Valores fuera de rango no ejecutan `amixer`.
- Fallo de `amixer` se propaga como resultado no disponible/fallido.
- La consulta de volumen puede indicar `No disponible` sin lanzar error de UI.

**Pruebas:** límites, argumentos, ejecutable ausente, exit code fallido y parseo de salida.

**Dependencias:** Tarea 1.1.

## Tarea 4.3: Integrar servicios de audio con DI y ciclo de vida

**Agente responsable:** backend/runtime.

**Estado:** Terminada.

**Objetivo:** conectar reproducción, volumen, historial y estado con el Backend.

**Especificación completa:**

- Registrar `IAudioPlayer` e `IVolumeController` en DI.
- Crear el servicio de aplicación que coordine validación, ejecución e historial.
- Usar `IDbContextFactory` para evitar compartir DbContext entre peticiones concurrentes.
- Si la reproducción se delega a un `BackgroundService`, usar una cola acotada, devolver al webhook un resultado coherente y registrar errores de ejecución.
- Liberar procesos y cancelar trabajo al apagar la aplicación.
- Evitar carreras entre cambios de volumen y reproducciones mediante una política documentada si el hardware lo requiere.
- Exponer estado actual para QUERY y dashboard.

**Entregables:** composición DI, servicio de aplicación, hosted service opcional, configuración y pruebas.

**Criterios de aceptación:**

- El Backend puede usar fakes completos en pruebas.
- Cada reproducción crea un registro de historial exactamente una vez por comando aceptado.
- Un fallo se registra con `Exitoso=false`.
- La aplicación se apaga sin dejar procesos hijos huérfanos.
- El estado no bloquea peticiones concurrentes indefinidamente.

**Pruebas:** integración con fakes, concurrencia básica, cancelación y apagado.

**Dependencias:** Tareas 1.3, 3.6, 3.7, 4.1 y 4.2.

# Fase 5: Integración, seguridad y despliegue

## Tarea 5.1: Pruebas de contrato del webhook completo

**Agente responsable:** QA/API.

**Estado:** Terminada.

**Objetivo:** comprobar que el Backend cumple los contratos Google Home.

**Especificación completa:** crear pruebas de integración que levanten el Backend con SQLite temporal y fakes de audio. Cubrir:

- Bearer ausente, inválido, expirado y válido.
- SYNC con el JSON esperado.
- QUERY con dispositivo conocido y desconocido.
- EXECUTE mediaPlay con `Shakira`.
- EXECUTE setVolume con 70.
- Payload incompleto, intent desconocido, comando desconocido y múltiples dispositivos.
- Conservación de requestId.
- Persistencia de historial y estados de éxito/fallo.
- Ausencia de secretos en respuestas y logs de prueba.

**Entregables:** suite de integración y fixtures reutilizables.

**Criterios de aceptación:** todos los casos definidos tienen aserciones sobre HTTP, JSON y base de datos; las pruebas son deterministas y no requieren Raspberry, mpv, yt-dlp ni amixer.

**Dependencias:** Fases 1, 3 y 4.

## Tarea 5.2: Pruebas del flujo OAuth completo

**Agente responsable:** QA/OAuth.

**Estado:** Terminada.

**Objetivo:** validar el flujo desde autorización hasta llamada autenticada.

**Especificación completa:** probar de extremo a extremo en entorno local:

1. Usuario abre `/oauth/authorize`.
2. Ingresa credenciales válidas.
3. Frontend valida `redirect_uri` y genera código temporal.
4. Google simulado llama a `/oauth/token`.
5. Backend consume el código y devuelve access/refresh token.
6. Google simulado llama a `/api/smarthome` con Bearer.
7. Backend resuelve `AgentUserId` y responde SYNC o EXECUTE.
8. El mismo código ya no puede intercambiarse.
9. Access token expirado deja de autenticar.

Cubrir errores de state, cliente, redirect URI, contraseña y refresh token.

**Entregables:** pruebas de integración OAuth y documentación del procedimiento manual.

**Criterios de aceptación:** el flujo feliz y todos los rechazos críticos están automatizados; no se imprimen secretos durante las pruebas.

**Dependencias:** Tareas 2.3, 3.2 y 3.3.

## Tarea 5.3: Preparar configuración y despliegue en Raspberry Pi

**Agente responsable:** release/operaciones.

**Estado:** Terminada.

**Objetivo:** documentar y automatizar la ejecución en Raspberry Pi.

**Especificación completa:**

- Documentar runtime .NET 10 requerido y arquitectura soportada.
- Documentar instalación/verificación de `mpv`, `yt-dlp` y `amixer`.
- Definir directorio de datos con permisos mínimos y respaldo de SQLite.
- Crear ejemplos de configuración sin secretos.
- Definir variables/secretos para usuario inicial, client secret, allowlist OAuth, rutas de ejecutables y URL pública.
- Publicar Backend y Frontend de forma reproducible.
- Crear servicios systemd separados o una estrategia equivalente, con reinicio, usuario no root, logs y dependencia de red.
- Documentar proxy HTTPS y URL pública necesaria para Google Home.
- Documentar migraciones, rollback de aplicación y copia de seguridad antes de actualizar esquema.

**Entregables:** guía de despliegue, archivos de ejemplo de configuración y unidades systemd si corresponden.

**Criterios de aceptación:** un operador puede instalar dependencias, configurar secretos fuera del repositorio, migrar la base, iniciar ambos procesos y comprobar `/health` sin ejecutar como root.

**Dependencias:** Fases 1 a 4.

## Tarea 5.4: Revisión final de seguridad y mantenibilidad

**Agente responsable:** revisión técnica.

**Estado:** Terminada.

**Objetivo:** detectar problemas antes de considerar terminado el proyecto.

**Especificación completa:** revisar explícitamente:

- Hashing de contraseñas.
- Protección de cookies y CSRF en formularios que cambien estado.
- Allowlist de OAuth y validación de `state`.
- Generación, expiración, rotación y revocación de tokens.
- Permisos del archivo SQLite y backups.
- Inyección de comandos en mpv/amixer.
- Límites de tamaño, tiempo y concurrencia.
- CORS, HTTPS, headers y exposición de errores.
- Logs sin secretos.
- Migraciones y acceso concurrente de dos aplicaciones a SQLite.
- Tests de errores y estados offline.

**Entregables:** informe de hallazgos priorizados y correcciones necesarias dentro del mismo alcance.

**Criterios de aceptación:** no quedan hallazgos críticos o altos sin una decisión documentada; `dotnet build` y toda la suite de pruebas pasan; la documentación refleja la configuración real.

**Dependencias:** todas las tareas anteriores.

# Definición de terminado

La implementación se considera terminada cuando:

- La solución contiene Frontend, Backend y Shared y compila con .NET 10.
- La migración crea las cuatro tablas SQLite y ambos procesos usan la misma configuración de conexión.
- El flujo OAuth autoriza, intercambia códigos de un solo uso y autentica con Bearer.
- `/api/smarthome` responde SYNC, QUERY y EXECUTE para mediaPlay y setVolume.
- `mpv`, `yt-dlp` y `amixer` están detrás de interfaces testeables y no reciben entrada sin validar.
- El dashboard muestra salud, estado de volumen, tokens enmascarados e historial.
- Existen pruebas automatizadas para contratos, OAuth, errores y persistencia.
- Existe documentación de despliegue en Raspberry Pi sin secretos dentro del repositorio.
- Se ejecutaron `dotnet build` y `dotnet test` satisfactoriamente.
