# Guia tecnica general de la API

Este documento resume como esta construida la API, como se autentica, como sincroniza datos y que elementos deben considerarse al operarla o integrarla tecnicamente.

## Vision general

RncPlatform expone una API HTTP para:

- autenticar usuarios y administrar sesiones,
- consultar contribuyentes por RNC o por prefijo de nombre,
- ejecutar sincronizaciones desde DGII,
- reprocesar snapshots archivados,
- consultar estado operacional.

La fuente de verdad es SQL Server. El cache distribuido con Valkey/Redis es opcional y se usa para lecturas y versionado de namespaces de cache.

## Arquitectura por proyectos

### `RncPlatform.Api`

Responsabilidades:

- configuracion del host,
- autenticacion JWT,
- autorizacion por politicas,
- rate limiting,
- CORS,
- health checks,
- swagger en desarrollo,
- controllers HTTP.

### `RncPlatform.Application`

Responsabilidades:

- orquestacion de casos de uso,
- logica de consulta de RNC,
- logica de sincronizacion,
- contratos de servicio internos.

### `RncPlatform.Domain`

Responsabilidades:

- entidades del negocio,
- enums como `UserRole` y estados del dominio.

### `RncPlatform.Infrastructure`

Responsabilidades:

- EF Core y `RncDbContext`,
- repositorios,
- parser y downloader de DGII,
- cache service,
- password hashing,
- emision de JWT,
- refresh tokens,
- distributed lock,
- background worker de sync.

### `RncPlatform.Contracts`

Responsabilidades:

- DTOs de requests y responses publicados por la API.

## Pipeline HTTP

El host aplica, en orden funcional, los siguientes componentes relevantes:

1. `ProblemDetails` para errores estandarizados.
2. `ForwardedHeaders` para escenarios detras de proxy.
3. `HttpsRedirection`.
4. `CORS` con politica `DefaultCors`.
5. `Authentication` JWT Bearer.
6. `RateLimiter` por politica.
7. `Authorization` por roles/politicas.
8. `MapControllers` y `MapHealthChecks`.

En `Development` tambien se publica Swagger UI.

## Autenticacion y sesiones

### Access token

- Formato: JWT Bearer.
- Claims clave: `NameIdentifier`, `Name`, `Role`, `token_version`.
- Validaciones: issuer, audience, firma, expiracion y version de token almacenada en BD.

### Refresh token

- Persistido en base de datos por hash.
- Rotacion en cada renovacion exitosa.
- Revocacion global en logout, cambio de acceso o reuse detectado.

### Invalidacion de sesiones

La API invalida sesiones incrementando `TokenVersion` del usuario. Como el pipeline JWT revalida este valor contra base de datos, cualquier access token anterior deja de ser valido aunque no haya expirado aun.

## Roles y politicas

Roles definidos:

- `User`
- `SyncOperator`
- `UserManager`
- `Admin`

Politicas configuradas:

- `AdminOnly`: solo `Admin`.
- `CanManageUsers`: `Admin` y `UserManager`.
- `CanRunSync`: `Admin` y `SyncOperator`.

Consulte [Roles y control de acceso](ROLES_AND_ACCESS.md) para la matriz completa.

## Busqueda de RNC

La consulta publica dos modos principales.

### Consulta exacta

Ruta:

- `GET /api/v1/rncs/{rnc}`

Uso recomendado cuando el cliente ya conoce el RNC exacto.

### Busqueda por `term`

Ruta:

- `GET /api/v1/rncs?term=...`

Comportamiento:

- Si `term` parece un RNC numerico, la consulta es exacta por RNC.
- Si no parece un RNC, debe tener al menos 3 caracteres.
- El filtro textual es por prefijo sobre `NombreORazonSocial` y `NombreComercial`.

### Paginacion

Soporta dos estrategias:

- offset con `page/pageSize`,
- seek/cursor con `cursor`.

En cursor mode la primera pagina se inicia enviando `cursor=`. La respuesta incluye `NextCursor` para pedir la siguiente pagina.

### Cache

- Las consultas exactas y las busquedas paginadas usan cache namespace versionado.
- Un sync o reproceso invalida los namespaces `taxpayer` y `taxpayer-search`.
- Si no hay Redis/Valkey configurado, el sistema usa `DistributedMemoryCache`.

## Sincronizacion con DGII

La sincronizacion sigue este flujo:

1. Adquirir lock distribuido `RncPlatform.Sync`.
2. Crear snapshot inicial en estado `Running`.
3. Descargar archivo fuente.
4. Calcular hash del archivo.
5. Archivar el archivo fuente en disco persistente.
6. Si el hash coincide con el ultimo exito y el flujo lo permite, devolver `NoChanges`.
7. Parsear el archivo a staging por lotes.
8. Hacer merge desde staging a `Taxpayers`.
9. Guardar conteos de inserts, updates y desactivaciones.
10. Invalidar cache y actualizar estado del job.
11. Liberar lock.

### Reproceso

`POST /api/v1/admin/sync/reprocess/{snapshotId}` reutiliza el archivo archivado del snapshot origen. Si el archivo ya no existe en el almacenamiento configurado, el endpoint responde conflicto.

### Estados utiles de sync

- `Success`
- `NoChanges`
- `Reprocessed`
- `Skipped - Already Running`
- `SnapshotNotFound`
- `SnapshotArchiveMissing`
- `Failed`

## Estado operativo

### Health checks

- `/health/live`: valida que la aplicacion responda.
- `/health/ready`: valida dependencias de lectura/escritura, como SQL Server y cache si aplica.

### Estado de sincronizacion

- `/api/v1/system/sync-status`: expone el ultimo estado persistido del job `DailySync`.

## Configuracion importante

Variables y secciones relevantes:

- `ConnectionStrings:DefaultConnection`
- `ConnectionStrings:Valkey`
- `Jwt:SecretKey`
- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:ExpiryMinutes`
- `Jwt:RefreshTokenExpiryDays`
- `Security:LoginMaxFailedAttempts`
- `Security:LoginLockoutMinutes`
- `Security:LoginRateLimitPermitLimit`
- `Security:LoginRateLimitWindowMinutes`
- `Cors:AllowedOrigins`
- `Bootstrap:*`
- `SyncArchive:RootPath`
- `Worker:Enabled`
- `Worker:TargetHourUtc`

Consulte [DEPLOYMENT.md](../DEPLOYMENT.md) para el detalle operativo por ambiente.

## Ejecucion local

Puertos por defecto definidos en `launchSettings.json`:

- HTTP: `5092`
- HTTPS: `7207`

Comando comun:

```powershell
dotnet run --project src/RncPlatform.Api/RncPlatform.Api.csproj --launch-profile https
```

Swagger se habilita solo en `Development`.

## Persistencia y migraciones

- EF Core usa SQL Server.
- La asamblea de migraciones vive en `RncPlatform.Infrastructure`.
- La migracion mas reciente agrega el indice `IX_Taxpayers_CommercialName_Rnc` para acelerar busqueda por `NombreComercial`.

Aplicacion de migraciones:

```powershell
dotnet ef database update --project src/RncPlatform.Infrastructure/RncPlatform.Infrastructure.csproj --startup-project src/RncPlatform.Api/RncPlatform.Api.csproj
```

## Seguridad operativa

- No publique secretos en `appsettings.json` de produccion.
- Restrinja `Cors:AllowedOrigins` a clientes conocidos.
- Exponga TLS en proxy o balanceador.
- Reenvie `X-Forwarded-For` y `X-Forwarded-Proto`.
- Considere WAF o rate limiting perimetral ademas del in-app.
- Use almacenamiento persistente para `SyncArchive:RootPath`.

## Endpoints no contractuales

La ruta `/WeatherForecast` sigue presente como artefacto del template base de ASP.NET Core. No debe ser usada por clientes ni considerada parte de la especificacion publica.