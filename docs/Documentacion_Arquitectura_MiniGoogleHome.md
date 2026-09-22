# Documentación de Arquitectura e Infraestructura: MiniGoogleHome

Este documento consolida la especificación técnica completa, decisiones de diseño, archivos de configuración finales y el catálogo de comandos operativos para el proyecto **MiniGoogleHome**, un puente de software nativo para conectar Google Home (Assistant / Gemini) con servicios locales de una Raspberry Pi utilizando **.NET 10** y **Podman Pods**.

---

## 1. Arquitectura del Sistema y Flujo de Integración

La integración nativa con Google Home exige una arquitectura basada en el estándar **Cloud-to-Cloud Smart Home** de Google. El sistema simula ser un dispositivo físico real (ej. un Altavoz Inteligente `action.devices.types.SPEAKER`) procesando intenciones estructuradas en formato JSON a través de HTTPS.

### Diagrama de Bloques de Infraestructura
### Diagrama de Flujo e Infraestructura

#### Flujo de Tráfico Exterior
1. **Google Cloud (Home/Gemini):** Dispara una petición HTTPS segura desde la nube.
2. **Túnel Seguro (Cloudflare/Ngrok):** Recibe la petición web en internet y la redirige a la red local.
3. **Raspberry Pi OS (ARM64):** Recibe el tráfico en el entorno físico del host.

#### Distribución Interna del Podman Pod (`localhost`)
*   **Contenedor 1: Frontend (Puerto 5011)**
    └── Lógica de Blazor SSR ➔ Procesa la interfaz web y la pantalla de Login OAuth2 (`/oauth/authorize`).
*   **Contenedor 2: Backend (Puerto 5010)**
    └── Lógica de Web API REST ➔ Valida firmas JWT (`/oauth/token`) y recibe el Webhook (`/api/smarthome`).

#### Ejecución Local de Multimedia (Audio Bridge)
*   **Backend (C#)** ➔ `System.Diagnostics.Process` ➔ **Consola Linux** ➔ Ejecuta de forma asíncrona `mpv` / `yt-dlp` ➔ **Salida física de audio (🔊)**.

| Componente | Capa de Infraestructura / Flujo de Datos | Puerto / Ruta de Acceso |
| :--- | :--- | :--- |
| **Origen** | `Google Cloud (Home / Gemini)` | Petición segura desde la nube |
| **Enlace Exterior** | 🌐 `Túnel Seguro (Cloudflare Tunnel o Ngrok)` | Redirección de HTTPS externo a HTTP local |
| **Entorno Host** | 🍓 `Raspberry Pi OS (Arquitectura ARM64)` | Sistema base de ejecución (Usuario `coff`) |
| **Contenedor Red** | 📦 `Podman Pod (minigooglehome-pod)` | Red unificada compartida en `localhost` |
| **Servicio Web** | 💻 `Contenedor 1: Frontend (Blazor SSR)` | **Puerto 5011**<br>• Interfaz Web Administrativa<br>• Login OAuth2 (`/oauth/authorize`) |
| **Servicio API** | ⚙️ `Contenedor 2: Backend (Web API REST)` | **Puerto 5010**<br>• Validación de Sesión JWT (`/oauth/token`)<br>• Webhook Google Smart Home (`/api/smarthome`) |
| **Puente de Audio** | 🔊 `System.Diagnostics.Process` ➔ `Linux OS` | Dispara binarios de consola locales `mpv` y `yt-dlp` hacia parlantes |

#### Persistencia Compartida en el Disco
* **Volumen del Host (Raspberry Pi):** `/home/coff/docker/SmartHome/datos`
* **Punto de Montaje (Dentro del Pod):** `/app/data`
* **Estructura del Directorio Físico:**
  * 📁 `backend_keys/` ➔ Llaves criptográficas de protección de la Web API.
  * 📁 `frontend_keys/` ➔ Llaves de Data Protection de la interfaz Blazor UI.
  * 📄 `smarthome.db` ➔ Base de datos relacional SQLite generada por Entity Framework Core.


### Componentes Clave del Software
1. **Frontend (Blazor SSR + Bootstrap 5):** Interfaz web de renderizado en el servidor optimizada para el bajo consumo de recursos en hardware ARM. Aloja el formulario web para que el usuario ingrese sus credenciales durante el proceso de vinculación de la app Google Home.
2. **Backend (ASP.NET Core Web API):** Motor REST que procesa los tokens de acceso, valida las peticiones seguras mediante atributos `[Authorize]` y parsea los JSON entrantes desde los servidores de Google.
3. **Capa de Persistencia Compartida (SQLite + EF Core):** Una base de datos ligera incrustada en un archivo físico del disco de la Raspberry Pi, accedida en simultáneo por ambos proyectos mediante Entity Framework Core.

---

## 2. Archivos de Configuración Finales

### A. Especificación del Pod de Kubernetes (`pod-minigooglehome.yaml`)
Este archivo declara la coexistencia de ambos contenedores dentro de un mismo espacio de red unificado (`localhost`). 
*Nota crítica: Utiliza **rutas absolutas** para el montaje de volúmenes en entornos Systemd de producción.*

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: minigooglehome-pod
  labels:
    app: minigooglehome
spec:
  volumes:
    - name: datos-sqlite
      hostPath:
        path: /home/coff/docker/SmartHome/datos
        type: DirectoryOrCreate
  containers:
    # 1. El Backend (Web API REST / OAuth) - Puerto 5010
    - name: smarthome-backend
      image: docker.io/damianns1/smarthome-backend:latest
      ports:
        - containerPort: 5010
          hostPort: 5010
      env:
        - name: ASPNETCORE_FORWARDEDHEADERS_ENABLED
          value: "true"
        - name: HTTP_PORTS
          value: "5010"
        - name: ASPNETCORE_URLS
          value: "http://+:5010"
        - name: ConnectionStrings__SharedDatabase
          value: "Data Source=/app/data/smarthome.db"
      volumeMounts:
        - name: datos-sqlite
          mountPath: /app/data
      securityContext:
        allowPrivilegeEscalation: false

    # 2. El Frontend (Blazor SSR) - Puerto 5011
    - name: smarthome-frontend
      image: docker.io/damianns1/smarthome-frontend:latest
      ports:
        - containerPort: 5011
          hostPort: 5011
      env:
        - name: ASPNETCORE_FORWARDEDHEADERS_ENABLED
          value: "true"
        - name: HTTP_PORTS
          value: "5011"
        - name: ASPNETCORE_URLS
          value: "http://+:5011"
        - name: ConnectionStrings__SharedDatabase
          value: "Data Source=/app/data/smarthome.db"
        - name: Backend__BaseUrl
          value: "http://localhost:5010"
      volumeMounts:
        - name: datos-sqlite
          mountPath: /app/data
      securityContext:
        allowPrivilegeEscalation: false
```

### B. Especificación del Quadlet de Systemd (`minigooglehome.kube`)
Este archivo le indica de manera nativa a Systemd cómo administrar el archivo de Kubernetes anterior utilizando el nuevo estándar de Podman 5.x. Debe guardarse estrictamente en `~/.config/containers/systemd/minigooglehome.kube`.

```ini
[Unit]
Description=Pod de Google Home - Blazor SSR y Web API REST (.NET 10)
After=network-online.target

[Kube]
Yaml=/home/coff/docker/SmartHome/pod-minigooglehome.yaml
PublishPort=5010:5010
PublishPort=5011:5011

[Install]
WantedBy=default.target
```

---

## 3. Resolución de Errores Críticos (Permisos y DataProtection)

Durante el despliegue inicial en modo *rootless*, .NET arrojó excepciones del tipo `System.UnauthorizedAccessException: Access to the path '/app/data/dataprotection' is denied`. Esto ocurrió debido al mapeo virtual de IDs de usuario en Podman Rootless y a la competencia de ambos contenedores escribiendo en la misma carpeta interna de llaves.

### Solución en Código C#
Se aislaron los directorios de almacenamiento del anillo de claves de seguridad de ASP.NET Core modificando el archivo `Program.cs` de cada proyecto por separado:

```csharp
// ---- EN EL PROGRAM.CS DEL FRONTEND (BLAZOR) ----
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"/app/data/frontend_keys"));

// ---- EN EL PROGRAM.CS DEL BACKEND (WEB API) ----
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"/app/data/backend_keys"));
```

### Solución en la Consola de Linux
Se restauró el propietario del directorio físico hacia el usuario del host (`coff`) y se forzaron permisos de escritura recursiva compatibles con el espacio de nombres de usuario virtual de Podman:

```bash
# Cambiar el propietario de la carpeta de datos secuestrada por IDs viejos
sudo chown -R coff:coff ./datos

# Otorgar accesos totales dentro del mapeo virtual rootless de Podman
podman unshare chmod -R 777 ./datos
```

---

## 4. Catálogo Completo de Comandos Operativos (Cheat Sheet)

### Compilación y Publicación Cruzada (C# a ARM64)
Para compilar nativamente imágenes de contenedores destinadas a la Raspberry Pi, ejecuta desde tu máquina de desarrollo:
```bash
dotnet publish ./SmartHome.Backend/SmartHome.Backend.csproj \
  -c Release \
  /t:PublishContainer \
  -p:ContainerRepository=damianns1/smarthome-backend \
  -p:ContainerRuntimeIdentifier=linux-arm64 \
  -p:ContainerImageTag=latest
```

### Administración del Servicio de Arranque Automático (Systemd + Quadlets)
```bash
# Recargar la configuración del sistema tras modificar el archivo .kube o el .yaml
systemctl --user daemon-reload

# Iniciar el Pod unificado de manera inmediata
systemctl --user start minigooglehome.service

# Apagar / Detener los servicios del Pod por completo
systemctl --user stop minigooglehome.service

# Comprobar si las aplicaciones están vivas (Active: active (running))
systemctl --user status minigooglehome.service
```

### Monitoreo Interno y Auditoría de Contenedores (Podman Native)
```bash
# Listar los pods activos y ver el contenedor infraestructura de red (infra)
podman pod ps

# Ver el listado detallado de los contenedores .NET levantados dentro del Pod
podman ps --pod

# Ver e inspeccionar los logs combinados de ambos proyectos en tiempo real (Follow)
podman logs -f minigooglehome-pod-smarthome-backend minigooglehome-pod-smarthome-frontend
```

### Flujo de Actualización Rápida de la Aplicación (Pipeline Manual)
Ejecuta esta cadena de comandos unificada en la Raspberry Pi cada vez que subas cambios de código a Docker Hub para desplegar la nueva versión de inmediato sin afectar la persistencia de la base de datos de SQLite:
```bash
podman pull docker.io/damianns1/smarthome-backend:latest && \
podman pull docker.io/damianns1/smarthome-frontend:latest && \
systemctl --user restart minigooglehome.service
```