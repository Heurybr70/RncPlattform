# Quickstart para clientes

Este documento esta pensado para clientes externos, equipos comerciales, integradores y desarrolladores que necesitan hacer su primera integracion en poco tiempo.

## Que resuelve la API

RncPlatform permite:

- autenticar usuarios de forma segura,
- consultar contribuyentes por RNC exacto,
- buscar por nombre o nombre comercial,
- navegar resultados grandes con cursor,
- consultar historial de cambios por RNC,
- obtener visibilidad del ultimo estado de sincronizacion.

## Tiempo a primera llamada exitosa

Con credenciales y una URL base valida, el flujo minimo es:

1. Hacer `login`.
2. Copiar el `token`.
3. Consultar un RNC o ejecutar una busqueda.

## URL base

Ejemplos de ambientes:

- Local: `https://localhost:7207`
- Produccion: `https://api.su-dominio.com`

## Variables recomendadas para probar con cURL

```bash
export BASE_URL="https://localhost:7207"
export USERNAME="admin"
export PASSWORD="RncPlatformAdmin2024!"
```

## 1. Iniciar sesion

```bash
curl -X POST "$BASE_URL/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "'"$USERNAME"'",
    "password": "'"$PASSWORD"'"
  }'
```

Respuesta esperada:

```json
{
  "userId": "4f719fad-6a36-4d60-a1a4-c2d80da1af00",
  "token": "<jwt>",
  "refreshToken": "<refresh-token>",
  "username": "admin",
  "role": "Admin",
  "expiresAt": "2026-04-09T18:00:00Z",
  "refreshTokenExpiresAt": "2026-04-23T17:00:00Z"
}
```

Guarde `token` y `refreshToken`.

```bash
export ACCESS_TOKEN="<jwt>"
export REFRESH_TOKEN="<refresh-token>"
```

## 2. Consultar un RNC exacto

```bash
curl "$BASE_URL/api/v1/rncs/101010101" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

Ideal cuando ya conoce el RNC exacto y quiere el detalle completo del contribuyente.

## 3. Buscar por nombre o nombre comercial

```bash
curl "$BASE_URL/api/v1/rncs?term=ALF&page=1&pageSize=20" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

Reglas utiles:

- Si `term` parece un RNC, la API hace busqueda exacta.
- Si `term` es texto, debe tener al menos 3 caracteres.
- La busqueda textual es por prefijo sobre nombre legal y nombre comercial.

## 4. Buscar con paginacion por cursor

Primera pagina:

```bash
curl "$BASE_URL/api/v1/rncs?term=ALF&pageSize=20&cursor=" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

Si la respuesta trae `nextCursor`, pida la siguiente pagina asi:

```bash
curl "$BASE_URL/api/v1/rncs?term=ALF&pageSize=20&cursor=202020202" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

Use cursor para listados grandes o scroll continuo. Para pantallas tradicionales con numeracion, `page/pageSize` sigue disponible.

## 5. Consultar historial de cambios

```bash
curl "$BASE_URL/api/v1/rncs/101010101/changes" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

Esto devuelve los cambios historicos detectados por snapshot para ese RNC.

## 6. Renovar sesion sin pedir login otra vez

```bash
curl -X POST "$BASE_URL/api/v1/auth/refresh" \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "'"$REFRESH_TOKEN"'"
  }'
```

Si la renovacion es exitosa, reemplace ambos valores: `ACCESS_TOKEN` y `REFRESH_TOKEN`.

## 7. Cerrar sesion

```bash
curl -X POST "$BASE_URL/api/v1/auth/logout" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

## Endpoints mas usados por clientes

| Objetivo | Metodo | Ruta |
|---|---|---|
| Login | `POST` | `/api/v1/auth/login` |
| Refresh | `POST` | `/api/v1/auth/refresh` |
| Logout | `POST` | `/api/v1/auth/logout` |
| Consulta exacta por RNC | `GET` | `/api/v1/rncs/{rnc}` |
| Busqueda por texto | `GET` | `/api/v1/rncs?term=...` |
| Historial por RNC | `GET` | `/api/v1/rncs/{rnc}/changes` |
| Estado de sincronizacion | `GET` | `/api/v1/system/sync-status` |

## Endpoints de administracion

Disponibles segun rol:

- `POST /api/v1/admin/sync/run`
- `POST /api/v1/admin/sync/reprocess/{snapshotId}`
- `POST /api/v1/auth/register`
- `GET /api/v1/auth/users`
- `GET /api/v1/auth/users/{userId}`
- `PATCH /api/v1/auth/users/{userId}/access`

## Errores comunes

| Codigo | Significado practico |
|---|---|
| `400` | Request invalido, termino de busqueda corto o datos inconsistentes |
| `401` | Token invalido, credenciales incorrectas o refresh token no valido |
| `403` | Cuenta inactiva o rol insuficiente |
| `404` | RNC, usuario o snapshot no encontrado |
| `409` | No se pudo reprocesar porque falta el archivo archivado del snapshot |
| `423` | Cuenta bloqueada temporalmente por intentos fallidos |
| `429` | Se excedio el limite de solicitudes |

## Opcion Postman

Puede importar directamente esta coleccion:

- [RncPlatform.postman_collection.json](postman/RncPlatform.postman_collection.json)

La coleccion ya incluye:

- variables de `baseUrl`, `username`, `password`, `accessToken` y `refreshToken`,
- login con script para guardar tokens automaticamente,
- refresh con script para rotar tokens guardados,
- requests listas para consultas, sync y administracion.

## Que compartir con su equipo de desarrollo

- URL base del ambiente.
- Usuario y contrasena asignados.
- Rol asignado.
- Lista de endpoints que realmente usaran.
- Politica de rotacion o resguardo de tokens.

Para detalle funcional completo, consulte [CLIENT_API.md](CLIENT_API.md). Para reglas tecnicas y operativas, consulte [API_TECHNICAL_GUIDE.md](API_TECHNICAL_GUIDE.md).