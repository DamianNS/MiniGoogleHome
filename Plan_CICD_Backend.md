# Plan de Migración CI/CD para SmartHome Backend

## Objetivo
Cambiar el mecanismo de creación de la imagen Docker del **Backend** de `dotnet publish /t:PublishContainer` a un clásico `Dockerfile` usando `docker buildx` para soportar la arquitectura **arm64** de la Raspberry Pi, dejando el **Frontend** intacto.

## Paso 1: Crear el `Dockerfile` para el Backend
Vamos a crear un archivo `SmartHome.Backend/Dockerfile`. 

Para evitar la lentitud extrema de emular procesadores arm64 en el CI de GitHub durante la compilación, aplicaremos un patrón de Docker multiplataforma muy eficiente para .NET:
1. Se utiliza el SDK de la plataforma nativa del runner (`$BUILDPLATFORM`, en este caso x64) para compilar y publicar.
2. Se le indica a `dotnet publish` que compile apuntando a la arquitectura `arm64`.
3. Finalmente, se copian los binarios compilados a una imagen de runtime ligera que será exclusivamente de la plataforma destino (`$TARGETPLATFORM`, o sea, linux/arm64).

*No requerirá librerías adicionales del sistema operativo por el momento, será estándar para ASP.NET Core.*

## Paso 2: Modificar `.github/workflows/deploy.yml`
Modificaremos el workflow existente de GitHub Actions:

1. **Agregar soporte para Buildx y QEMU:** Agregaremos los actions `docker/setup-qemu-action` y `docker/setup-buildx-action` antes de iniciar sesión en Docker. Esto habilita las herramientas avanzadas de compilación multiplataforma de Docker en el runner de GitHub.
2. **Reemplazar el build del Backend:** 
   Cambiaremos esto:
   ```bash
   dotnet publish ./SmartHome.Backend/SmartHome.Backend.csproj -c Release /t:PublishContainer ...
   docker tag ...
   docker push ...
   ```
   Por esto:
   ```bash
   docker buildx build \
     --platform linux/arm64 \
     -f ./SmartHome.Backend/Dockerfile \
     -t ${{ secrets.DOCKER_USERNAME }}/smarthome-backend:latest \
     --push .
   ```
3. **Frontend sin cambios:** La etapa `Publicar y subir Web Frontend` y la etapa de `deploy` por SSH hacia la Raspberry (DonWeb/Podman) seguirán tal cual como están.

## Próximos pasos
Una vez que me des el ok:
1. Crearé el `Dockerfile` optimizado para el backend.
2. Actualizaré el archivo `.github/workflows/deploy.yml`.
