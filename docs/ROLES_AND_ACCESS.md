# Roles y control de acceso

Este documento deja constancia de los roles del sistema, las politicas que los usan y el alcance operativo de cada uno.

## Roles disponibles

### `User`

Perfil base para consulta autenticada.

Capacidades:

- iniciar sesion,
- renovar sesion,
- cerrar sesion,
- consultar contribuyentes,
- consultar historial de cambios por RNC.

No puede:

- crear usuarios,
- cambiar accesos,
- ejecutar sync,
- reprocesar snapshots.

### `SyncOperator`

Perfil operativo para cargas y reprocesos.

Capacidades:

- todo lo de `User`,
- ejecutar `POST /api/v1/admin/sync/run`,
- ejecutar `POST /api/v1/admin/sync/reprocess/{snapshotId}`.

No puede:

- administrar usuarios.

### `UserManager`

Perfil administrativo de cuentas, sin control total del sistema.

Capacidades:

- todo lo de `User`,
- crear usuarios con `POST /api/v1/auth/register`,
- listar usuarios,
- consultar detalle de usuarios.

Restricciones importantes:

- puede crear solo roles `User` y `SyncOperator`,
- no puede crear `Admin`,
- no puede crear `UserManager`,
- no puede actualizar acceso por `PATCH /api/v1/auth/users/{userId}/access`.

### `Admin`

Perfil con control total.

Capacidades:

- todo lo de `User`, `SyncOperator` y `UserManager`,
- crear usuarios de cualquier rol,
- activar o desactivar usuarios,
- cambiar roles mediante `PATCH /api/v1/auth/users/{userId}/access`.

Restriccion especifica:

- no puede desactivar su propio usuario desde `PATCH /access`.

## Politicas configuradas

### `AdminOnly`

Permite acceso solo a `Admin`.

Uso actual:

- `PATCH /api/v1/auth/users/{userId}/access`

### `CanManageUsers`

Permite acceso a `Admin` y `UserManager`.

Uso actual:

- `POST /api/v1/auth/register`
- `GET /api/v1/auth/users`
- `GET /api/v1/auth/users/{userId}`

### `CanRunSync`

Permite acceso a `Admin` y `SyncOperator`.

Uso actual:

- `POST /api/v1/admin/sync/run`
- `POST /api/v1/admin/sync/reprocess/{snapshotId}`

## Matriz resumida

| Endpoint | User | SyncOperator | UserManager | Admin |
|---|---|---|---|---|
| `POST /api/v1/auth/login` | Si | Si | Si | Si |
| `POST /api/v1/auth/refresh` | Si | Si | Si | Si |
| `POST /api/v1/auth/logout` | Si | Si | Si | Si |
| `GET /api/v1/rncs/{rnc}` | Si | Si | Si | Si |
| `GET /api/v1/rncs?term=...` | Si | Si | Si | Si |
| `GET /api/v1/rncs/{rnc}/changes` | Si | Si | Si | Si |
| `GET /api/v1/system/sync-status` | Publico | Publico | Publico | Publico |
| `POST /api/v1/admin/sync/run` | No | Si | No | Si |
| `POST /api/v1/admin/sync/reprocess/{snapshotId}` | No | Si | No | Si |
| `POST /api/v1/auth/register` | No | No | Si | Si |
| `GET /api/v1/auth/users` | No | No | Si | Si |
| `GET /api/v1/auth/users/{userId}` | No | No | Si | Si |
| `PATCH /api/v1/auth/users/{userId}/access` | No | No | No | Si |

## Como se asignan los roles

### Bootstrap inicial

La plataforma puede crear o promover un usuario privilegiado desde configuracion `Bootstrap:*` cuando no existe ningun `Admin` o `UserManager`.

Valores relevantes:

- `Bootstrap__Username`
- `Bootstrap__Password`
- `Bootstrap__Email`
- `Bootstrap__FullName`
- `Bootstrap__Role`
- `Bootstrap__Enabled`

`Bootstrap__Role` debe ser `Admin` o `UserManager`.

### Registro desde la API

`POST /api/v1/auth/register` permite altas remotas controladas por politica.

Reglas:

- `Admin` puede registrar cualquier rol.
- `UserManager` solo puede registrar `User` y `SyncOperator`.

### Cambio de acceso

`PATCH /api/v1/auth/users/{userId}/access` es el endpoint formal para cambiar rol y estado activo de un usuario existente.

Efectos colaterales cuando hay cambios reales:

- incremento de `TokenVersion`,
- reseteo de lockout,
- revocacion de refresh tokens activos.

## Impacto de seguridad por cambios de acceso

Cuando un usuario cambia de rol o es desactivado:

- los refresh tokens activos se revocan,
- los access tokens previos quedan invalidos en la siguiente validacion JWT,
- el usuario debe volver a autenticarse con el nuevo estado de acceso.