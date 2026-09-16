# Especificación Técnica Completa: Smart Home Media Bridge (.NET 10)

Este documento contiene el plan completo, especificaciones de arquitectura, esquemas JSON detallados de Google Home y la estructura de base de datos para la generación del proyecto por parte de Antigrabity.

---

## 1. Arquitectura de la Solución (.NET 10)

El proyecto se divide en dos aplicaciones independientes compartiendo una base de datos local SQLite mediante Entity Framework Core.

*   **Proyecto 1: Frontend (Blazor SSR + Bootstrap 5)**
    *   Interfaz web administrativa corriendo en la Raspberry Pi.
    *   Formulario visual para la vinculación inicial de OAuth 2.0 (`/oauth/authorize`).
    *   Panel de control (Dashboard) para ver los tokens emitidos, el historial de audios reproducidos y el estado actual del volumen de la Pi.
*   **Proyecto 2: Backend (Web API REST)**
    *   Servidor API sin estado que procesa las peticiones de Google (`/api/smarthome`) y los endpoints de intercambio de tokens (`/oauth/token`).
    *   Ejecutor en segundo plano asíncrono para interactuar con el sistema operativo Linux (`mpv` y `yt-dlp`).
*   **Base de Datos Compartida (SQLite + EF Core)**
    *   Archivo único `.db` accedido por ambos proyectos para persistir credenciales de usuario, códigos de autorización temporales, tokens activos e historial.

---

## 2. Estructura de la Base de Datos (SQLite + EF Core)

Se definen 4 tablas principales necesarias para el control de identidad OAuth y la auditoría del dispositivo multimedia.

### Tabla: `Usuarios`
Almacena las credenciales locales de acceso al hogar para validar la pantalla de Blazor SSR.
```csharp
public class Usuario
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // Validado en Login
    public string AgentUserId { get; set; } = string.Empty; // ID único enviado a Google
}
```

### Tabla: `OauthCodes`
Almacena de forma temporal los códigos intermedios generados tras el inicio de sesión en Blazor previos al intercambio por parte de Google.
```csharp
public class OauthCode
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string AgentUserId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
}
```

### Tabla: `OauthTokens`
Guarda las llaves de acceso activas otorgadas a los servidores de Google para autenticar cada comando de voz.
```csharp
public class OauthToken
{
    public int Id { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string AgentUserId { get; set; } = string.Empty;
    public DateTime AccessExpiresAt { get; set; }
}
```

### Tabla: `HistorialReproduccion`
Registra cada comando enviado desde el asistente de Google para auditoría y visualización en el panel Blazor.
```csharp
public class HistorialReproduccion
{
    public int Id { get; set; }
    public string QueryTexto { get; set; } = string.Empty; // Ej: "Shakira"
    public DateTime Fecha { get; set; }
    public bool Exitoso { get; set; }
}
```

---

## 3. Profundización en los Esquemas JSON de Google Smart Home

El backend REST API debe interpretar e intercambiar las siguientes estructuras JSON puras obligatorias en el endpoint `/api/smarthome`.

### A. Intent: SYNC
Google lo invoca inmediatamente después de que el usuario vincula su cuenta para descubrir qué dispositivos expone tu servidor.

#### Solicitud Inbound de Google:
```json
{
  "requestId": "4478392110293811",
  "inputs": [
    {
      "intent": "action.devices.SYNC"
    }
  ]
}
```

#### Respuesta Outbound Esperada de tu Web API:
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

### B. Intent: EXECUTE (Comando de Reproducción)
Llega cuando dices *"Ok Google, reproduce Shakira en el Parlante Raspberry"*. Google Assistant extrae el texto clave y lo inyecta estructurado.

#### Solicitud Inbound de Google:
```json
{
  "requestId": "1198273645524312",
  "inputs": [
    {
      "intent": "action.devices.EXECUTE",
      "payload": {
        "commands": [
          {
            "devices": [
              {
                "id": "pi_media_speaker_01"
              }
            ],
            "execution": [
              {
                "command": "action.devices.commands.mediaPlay",
                "params": {
                  "mediaQuery": {
                    "query": "Shakira"
                  }
                }
              }
            ]
          }
        ]
      }
    }
  ]
}
```

#### Respuesta Outbound Esperada de tu Web API:
```json
{
  "requestId": "1198273645524312",
  "payload": {
    "commands": [
      {
        "ids": [
          "pi_media_speaker_01"
        ],
        "status": "SUCCESS",
        "states": {
          "playbackState": "PLAYING"
        }
      }
    ]
  }
}
```

### C. Intent: EXECUTE (Comando de Volumen)
Llega cuando el usuario dice *"Ok Google, pon el volumen del Parlante Raspberry al 70%"*.

#### Solicitud Inbound de Google:
```json
{
  "requestId": "9928374615243516",
  "inputs": [
    {
      "intent": "action.devices.EXECUTE",
      "payload": {
        "commands": [
          {
            "devices": [
              {
                "id": "pi_media_speaker_01"
              }
            ],
            "execution": [
              {
                "command": "action.devices.commands.setVolume",
                "params": {
                  "volumeLevel": 70
                }
              }
            ]
          }
        ]
      }
    }
  ]
}
```

#### Respuesta Outbound Esperada de tu Web API:
```json
{
  "requestId": "9928374615243516",
  "payload": {
    "commands": [
      {
        "ids": [
          "pi_media_speaker_01"
        ],
        "status": "SUCCESS",
        "states": {
          "currentVolume": 70
        }
      }
    ]
  }
}
```

---

## 4. Listado de Tareas Detalladas para Antigrabity

### Fase 1: Configuración de la Base de Datos e Infraestructura Compartida
- [ ] **Crear solución en blanco .NET 10** que contenga tres proyectos: `SmartHome.Frontend`, `SmartHome.Backend` y `SmartHome.Shared`.
- [ ] En `SmartHome.Shared`, definir las clases de entidad de EF Core (`Usuario`, `OauthCode`, `OauthToken`, `HistorialReproduccion`).
- [ ] Configurar el DbContext para apuntar a un archivo SQLite unificado ubicado en un directorio compartido accesible en la Raspberry Pi.
- [ ] Ejecutar la migración inicial de Entity Framework (`dotnet ef migrations add InitialCreate`).

### Fase 2: Desarrollo del Frontend Administrativo (Blazor SSR)
- [ ] Configurar el proyecto Blazor con renderizado estático del lado del servidor (**Blazor SSR**) y compilar los estilos responsivos de **Bootstrap 5**.
- [ ] Desarrollar la vista interactiva para el flujo de autorización OAuth 2.0 (`/oauth/authorize`). Debe solicitar usuario y contraseña, validar contra SQLite mediante EF Core, y emitir la redirección HTTP requerida por Google adjuntando el código generado.
- [ ] Diseñar un Dashboard principal protegido por sesión que muestre:
    * El estado de conexión del backend.
    * Lista de tokens OAuth de Google actualmente válidos en base de datos.
    * Tabla con el historial reciente de pistas solicitadas (`HistorialReproduccion`).

### Fase 3: Web API REST y Webhook de Google (Backend)
- [ ] Configurar `SmartHome.Backend` como una Web API REST en .NET 10.
- [ ] Desarrollar el endpoint `POST /oauth/token` encargado de validar el código de Blazor SSR de la base de datos y retornar el Token estructurado a los servidores de Google.
- [ ] Habilitar el Middleware de autenticación Bearer que valide de forma asíncrona la firma local del Access Token guardado en SQLite.
- [ ] Desarrollar el controlador `POST /api/smarthome` decorado con el atributo `[Authorize]`.
- [ ] Implementar los bloques switch para responder nativamente a los esquemas JSON de `SYNC`, `QUERY` y `EXECUTE` detallados en la sección 3.

### Fase 4: Integración del Sistema de Audio Linux en la Pi
- [ ] Programar un servicio en segundo plano (`IHostedService` o clase asíncrona) que capture la propiedad `params.mediaQuery.query` del comando `mediaPlay`.
- [ ] Insertar un registro en la tabla `HistorialReproduccion` usando el DbContext compartido por cada petición de reproducción.
- [ ] Implementar el llamado asíncrono a comandos del sistema operativo mediante `System.Diagnostics.Process` para iniciar `mpv --no-video "ytsearch:<Texto>"`.
- [ ] Mapear el comando `setVolume` para que intercepte el número de `volumeLevel` y ejecute en la shell de la Raspberry Pi la instrucción nativa de ajuste de audio (`amixer set Master <Porcentaje>%`).
