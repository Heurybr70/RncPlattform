# RncPlatform

RncPlatform es una API ASP.NET Core para consulta de contribuyentes por RNC y para sincronizacion del padron RNC desde la fuente oficial de la DGII hacia SQL Server.

El proyecto esta organizado como un monolito modular con capas separadas para API, Application, Domain, Infrastructure y Contracts. Incluye autenticacion JWT, refresh tokens, control de roles, cache distribuido opcional con Valkey/Redis, snapshots de sincronizacion, changelog historico y reproceso desde archivos archivados.

## Capacidades principales

- Consulta exacta por RNC.
- Busqueda por prefijo sobre `NombreORazonSocial` y `NombreComercial`.
- Paginacion tradicional por `page/pageSize` y paginacion aditiva por `cursor`.
- Autenticacion con access token y refresh token rotativo.
- Administracion de usuarios con roles.
- Ejecucion manual de sincronizacion y reproceso de snapshots.
- Endpoints de salud y estado del ultimo sync.

## Estructura de la solucion

- `src/RncPlatform.Api`: host HTTP, configuracion, middleware, controllers y politicas.
- `src/RncPlatform.Application`: casos de uso y orquestacion de negocio.
- `src/RncPlatform.Domain`: entidades y enums del dominio.
- `src/RncPlatform.Infrastructure`: EF Core, repositorios, cache, identidad, worker y conectores externos.
- `src/RncPlatform.Contracts`: request/response DTOs publicados por la API.
- `tests/RncPlatform.Application.Tests`: pruebas unitarias.
- `tests/RncPlatform.Api.Tests`: pruebas HTTP de integracion con LocalDB.

## Requisitos

- .NET SDK 10.
- SQL Server accesible desde la API.
- Valkey o Redis opcional para cache distribuido.

## Ejecucion local rapida

1. Configure las variables de entorno necesarias o use `appsettings.Development.json` segun su entorno.
2. Aplique migraciones:

```powershell
dotnet ef database update --project src/RncPlatform.Infrastructure/RncPlatform.Infrastructure.csproj --startup-project src/RncPlatform.Api/RncPlatform.Api.csproj
```

3. Ejecute la API:

```powershell
dotnet run --project src/RncPlatform.Api/RncPlatform.Api.csproj --launch-profile https
```

4. URLs locales por defecto:
   - `https://localhost:7207`
   - `http://localhost:5092`

Swagger se expone en `Development` y tambien puede habilitarse en otros ambientes con `Swagger__Enabled=true`.

## Documentacion disponible

- [Quickstart comercial para clientes](docs/CLIENT_QUICKSTART.md)
- [Guia para clientes](docs/CLIENT_API.md)
- [Guia tecnica general](docs/API_TECHNICAL_GUIDE.md)
- [Guia de superusuario](docs/SUPERUSER_GUIDE.md)
- [Roles y control de acceso](docs/ROLES_AND_ACCESS.md)
- [Coleccion Postman](docs/postman/RncPlatform.postman_collection.json)
- [Notas de despliegue](DEPLOYMENT.md)
- [Historial del proyecto](PROJECT_HISTORY.md)

## Pruebas

```powershell
dotnet test RncPlatform.slnx
```

## Alcance del contrato HTTP

Los endpoints documentados bajo `/api/v1` y `/health/*` son los que deben considerarse para integraciones. La ruta `/WeatherForecast` proviene del template base de ASP.NET Core y no debe considerarse parte del contrato funcional de la plataforma.