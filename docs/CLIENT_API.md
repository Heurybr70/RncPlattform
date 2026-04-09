# Guia para clientes de la API

Esta guia esta pensada para clientes, integradores y frontends que consumen la API de RncPlatform.

Si necesita un onboarding mas breve y orientado a negocio, use primero [CLIENT_QUICKSTART.md](CLIENT_QUICKSTART.md). Si prefiere importar requests listos, use la [coleccion Postman](postman/RncPlatform.postman_collection.json).

## Base URL

Use la URL base que corresponda al ambiente:

- Desarrollo local: `https://localhost:7207`
- Produccion: la URL publicada por el operador de la plataforma

Todos los endpoints funcionales estan bajo el prefijo `api/v1`.

## Autenticacion

La API usa JWT Bearer para los access tokens.

### Encabezado requerido

```http
Authorization: Bearer <access-token>
```

### Flujo recomendado

1. Hacer login con usuario y contrasena.
2. Guardar `token` y `refreshToken`.
3. Enviar `token` en cada request autenticado.
4. Cuando el access token expire, usar `refreshToken` para pedir un nuevo par de tokens.
5. Al cerrar sesion, llamar `logout`.

Importante:

- El refresh token rota en cada renovacion.
- `logout` invalida todas las sesiones activas del usuario, no solo la actual.
- Si un refresh token revocado se reutiliza, la API invalida todas las sesiones del usuario.

## Formato de errores

La mayoria de errores de negocio y seguridad se devuelven como `application/problem+json` con campos como:

- `status`
- `title`
- `detail`
- `traceId`

Hay validaciones puntuales que pueden responder con `400` y un mensaje simple en texto o JSON corto.

## Endpoints disponibles para clientes autenticados

### 1. Login

- Metodo: `POST`
- Ruta: `/api/v1/auth/login`
- Autenticacion: no
- Uso: iniciar sesion y obtener access token y refresh token

Request:

```json
{
  "username": "cliente-app",
  "password": "Password123!"
}
```

Response `200 OK`:

```json
{
  "userId": "4f719fad-6a36-4d60-a1a4-c2d80da1af00",
  "token": "<jwt>",
  "refreshToken": "<refresh-token>",
  "username": "cliente-app",
  "role": "User",
  "expiresAt": "2026-04-09T18:00:00Z",
  "refreshTokenExpiresAt": "2026-04-23T17:00:00Z"
}
```

Errores frecuentes:

- `401`: credenciales invalidas.
- `403`: cuenta inactiva.
- `423`: cuenta bloqueada temporalmente.
- `429`: exceso de intentos.

### 2. Refresh de sesion

- Metodo: `POST`
- Ruta: `/api/v1/auth/refresh`
- Autenticacion: no, requiere `refreshToken`

Request:

```json
{
  "refreshToken": "<refresh-token>"
}
```

Response `200 OK`: mismo contrato que `login`.

Errores frecuentes:

- `401`: refresh token invalido, expirado o reutilizado.
- `403`: usuario inactivo.

### 3. Logout

- Metodo: `POST`
- Ruta: `/api/v1/auth/logout`
- Autenticacion: si

Response:

- `204 No Content`

### 4. Consulta exacta por RNC

- Metodo: `GET`
- Ruta: `/api/v1/rncs/{rnc}`
- Autenticacion: si

Ejemplo:

```http
GET /api/v1/rncs/101010101
```

Response `200 OK`:

```json
{
  "rnc": "101010101",
  "cedula": null,
  "nombreORazonSocial": "ALFA INDUSTRIES",
  "nombreComercial": "ALFA",
  "categoria": "SOCIEDAD",
  "regimenPago": "NORMAL",
  "estado": "ACTIVO",
  "actividadEconomica": "SERVICIOS",
  "fechaConstitucion": "2012-06-01",
  "isActive": true,
  "removedAt": null
}
```

Errores frecuentes:

- `404`: RNC no encontrado.

### 5. Busqueda de contribuyentes

- Metodo: `GET`
- Ruta: `/api/v1/rncs`
- Autenticacion: si

Parametros:

- `term`: obligatorio.
- `page`: opcional, modo offset. Valor minimo efectivo: `1`.
- `pageSize`: opcional. Rango efectivo: `1..100`. Fuera de rango vuelve a `20`.
- `cursor`: opcional. Si el parametro existe, la API activa paginacion por cursor.

Reglas de busqueda:

- Si `term` es numerico con formato de RNC, la busqueda es exacta por RNC.
- Si `term` no es RNC, debe tener al menos 3 caracteres.
- La busqueda por texto usa prefijo sobre `NombreORazonSocial` y `NombreComercial`.

#### Modo offset

Ejemplo:

```http
GET /api/v1/rncs?term=ALF&page=1&pageSize=20
```

Response:

```json
{
  "items": [
    {
      "rnc": "101010101",
      "nombreORazonSocial": "ALFA INDUSTRIES",
      "nombreComercial": "ALFA",
      "estado": "ACTIVO",
      "isActive": true
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20,
  "nextCursor": null
}
```

#### Modo cursor

Para iniciar el modo cursor en la primera pagina debe enviar el parametro vacio:

```http
GET /api/v1/rncs?term=ALF&pageSize=20&cursor=
```

Si hay mas resultados, `nextCursor` vendra con el ultimo `rnc` de la pagina actual.

Siguiente pagina:

```http
GET /api/v1/rncs?term=ALF&pageSize=20&cursor=202020202
```

Notas:

- En modo cursor, la API responde `page = 1` por compatibilidad.
- Si usa cursor, ignore `page`.
- Si no hay mas resultados, `nextCursor` sera `null`.

Errores frecuentes:

- `400`: termino vacio o texto de menos de 3 caracteres.
- `401`: token ausente o invalido.

### 6. Historial de cambios por RNC

- Metodo: `GET`
- Ruta: `/api/v1/rncs/{rnc}/changes`
- Autenticacion: si

Response `200 OK`:

```json
[
  {
    "changeId": "0d0737fd-193c-43dd-8194-41210a6ecf28",
    "snapshotId": "c30a6166-80bc-4d7f-8d65-4bf96f3205d4",
    "changeType": "Updated",
    "detectedAt": "2026-04-09T16:00:00Z",
    "oldValuesJson": "{...}",
    "newValuesJson": "{...}"
  }
]
```

## Endpoints operativos o administrativos

Estos endpoints no suelen usarse desde aplicaciones cliente finales, pero si pueden ser consumidos por consolas administrativas o procesos internos.

### 7. Estado del ultimo sync

- Metodo: `GET`
- Ruta: `/api/v1/system/sync-status`
- Autenticacion: no

Response `200 OK`:

```json
{
  "lastRunAt": "2026-04-09T16:00:00Z",
  "lastSuccessAt": "2026-04-09T16:01:00Z",
  "lastFailureAt": null,
  "lastStatus": "Success",
  "lastMessage": null
}
```

### 8. Ejecutar sync manual

- Metodo: `POST`
- Ruta: `/api/v1/admin/sync/run`
- Autenticacion: si
- Roles: `Admin`, `SyncOperator`

Response `200 OK`:

```json
{
  "snapshotId": "b276d8ff-1efd-4eb5-8558-c6cad91d6f4f",
  "insertedCount": 10,
  "updatedCount": 5,
  "deactivatedCount": 1,
  "status": "Success",
  "errorMessage": null
}
```

Posibles `status`:

- `Success`
- `NoChanges`
- `Reprocessed`
- `Skipped - Already Running`
- `SnapshotNotFound`
- `SnapshotArchiveMissing`

### 9. Reprocesar snapshot archivado

- Metodo: `POST`
- Ruta: `/api/v1/admin/sync/reprocess/{snapshotId}`
- Autenticacion: si
- Roles: `Admin`, `SyncOperator`

Respuesta:

- `200`: reproceso completado.
- `404`: snapshot no encontrado.
- `409`: archivo archivado no disponible.

### 10. Registro de usuarios

- Metodo: `POST`
- Ruta: `/api/v1/auth/register`
- Autenticacion: si
- Roles: `Admin`, `UserManager`

Request:

```json
{
  "username": "operador-sync",
  "password": "Password123!",
  "email": "operador@cliente.com",
  "fullName": "Operador Sync",
  "role": "SyncOperator"
}
```

Notas:

- `Admin` puede crear cualquier rol.
- `UserManager` solo puede crear `User` y `SyncOperator`.

### 11. Listado de usuarios

- Metodo: `GET`
- Ruta: `/api/v1/auth/users`
- Autenticacion: si
- Roles: `Admin`, `UserManager`

### 12. Detalle de usuario

- Metodo: `GET`
- Ruta: `/api/v1/auth/users/{userId}`
- Autenticacion: si
- Roles: `Admin`, `UserManager`

### 13. Actualizar acceso de usuario

- Metodo: `PATCH`
- Ruta: `/api/v1/auth/users/{userId}/access`
- Autenticacion: si
- Roles: `Admin`

Request:

```json
{
  "role": "UserManager",
  "isActive": true
}
```

Efectos:

- Cambia rol y estado activo.
- Revoca refresh tokens del usuario afectado.
- Invalida access tokens por aumento de `token_version`.

## Endpoints de salud

### Liveness

- Metodo: `GET`
- Ruta: `/health/live`
- Autenticacion: no

### Readiness

- Metodo: `GET`
- Ruta: `/health/ready`
- Autenticacion: no

`/health/ready` valida SQL Server y, si esta configurado, tambien el cache Valkey/Redis.

## Rate limiting

La API aplica limites por IP o por usuario segun el endpoint. Los valores pueden ajustarse por ambiente. Si recibe `429 Too Many Requests`, reintente luego del periodo de ventana configurado.

## Recomendaciones para integracion

- Trate `token` y `refreshToken` como secretos.
- Use `refresh` antes de volver a pedir credenciales al usuario.
- Para listados grandes, prefiera `cursor` sobre `page`.
- Use `GET /api/v1/rncs/{rnc}` cuando ya tenga el RNC exacto.
- No consuma `/WeatherForecast`; no forma parte del contrato del producto.