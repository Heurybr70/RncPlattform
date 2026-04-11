# Guia de superusuario de RncPlatform

Esta guia define el alcance real del superusuario de la API, las reglas de operacion recomendadas y las limitaciones actuales del sistema. Su objetivo es que el operador con control total pueda administrar la plataforma sin zonas grises.

## 1. Definicion exacta del superusuario

En la implementacion actual, el superusuario corresponde al rol `Admin`.

`Admin` es el unico rol con control total sobre:

- administracion integral de usuarios,
- cambios de rol y activacion o desactivacion de cuentas,
- ejecucion manual de sincronizaciones,
- reproceso de snapshots,
- acceso completo a los endpoints autenticados de consulta.

Importante: el sistema actual no reserva en exclusiva la creacion de usuarios al rol `Admin`. Tambien permite que `UserManager` cree usuarios con rol `User` y `SyncOperator`. Si usted desea que en la practica solo el superusuario cree usuarios, la norma operativa correcta es no asignar el rol `UserManager` a terceros.

## 2. Alcance tecnico del rol `Admin`

El superusuario hereda todo lo que puede hacer cualquier otro rol:

- capacidades de `User` para consultar la API autenticada,
- capacidades de `SyncOperator` para ejecutar sync y reproceso,
- capacidades de `UserManager` para consultar inventario de usuarios y registrar cuentas,
- capacidades exclusivas de `Admin` para cambiar rol y estado activo de usuarios.

## 3. Roles del sistema y relacion con el superusuario

### `User`

Perfil base de consulta.

Puede:

- iniciar sesion,
- renovar sesion,
- cerrar sesion,
- consultar RNC exacto,
- buscar contribuyentes,
- ver historial de cambios por RNC.

No puede:

- registrar usuarios,
- cambiar roles,
- activar o desactivar cuentas,
- ejecutar sync,
- reprocesar snapshots.

### `SyncOperator`

Perfil operativo de sincronizacion.

Puede:

- todo lo de `User`,
- ejecutar `POST /api/v1/admin/sync/run`,
- ejecutar `POST /api/v1/admin/sync/reprocess/{snapshotId}`.

No puede:

- registrar usuarios,
- cambiar acceso de otros usuarios.

### `UserManager`

Perfil administrativo parcial.

Puede:

- todo lo de `User`,
- registrar usuarios con `POST /api/v1/auth/register`,
- listar usuarios,
- consultar detalle de usuarios.

No puede:

- crear `Admin`,
- crear `UserManager`,
- cambiar rol o estado activo con `PATCH /api/v1/auth/users/{userId}/access`,
- ejecutar sync o reproceso.

### `Admin`

Perfil de control total.

Puede:

- todo lo de `User`, `SyncOperator` y `UserManager`,
- crear usuarios de cualquier rol,
- activar o desactivar usuarios,
- cambiar roles,
- invalidar sesiones indirectamente al cambiar acceso,
- operar el ciclo completo de sincronizacion.

Restriccion tecnica vigente:

- no puede desactivar su propio usuario mediante `PATCH /api/v1/auth/users/{userId}/access`.

## 4. Normativa operativa recomendada para el superusuario

Estas reglas no todas estan forzadas por codigo, pero deben considerarse obligatorias si usted quiere mantener control real y reducir riesgo operativo.

### 4.1 Gobierno de cuentas

- Mantenga al menos una cuenta `Admin` de contingencia fuera del uso diario.
- No comparta credenciales de `Admin` entre personas.
- Si quiere monopolio real sobre el alta de usuarios, no entregue el rol `UserManager`.
- Use cuentas nominales por persona. No opere con un usuario generico compartido.

### 4.2 Uso del bootstrap inicial

- El bootstrap solo crea o promueve un usuario privilegiado cuando no existe ningun `Admin` ni `UserManager`.
- Una vez estabilizado el acceso, conviene desactivar el bootstrap con `Bootstrap__Enabled=false`.
- Si usa bootstrap para el primer acceso, trate esas credenciales como temporales o de contingencia.

### 4.3 Control de privilegios

- Otorgue `Admin` solo a quien deba cambiar acceso de otros usuarios.
- Otorgue `UserManager` solo si acepta delegar altas de `User` y `SyncOperator`.
- Use `SyncOperator` para operacion de datos sin exponer administracion de cuentas.

### 4.4 Operacion segura

- Antes de desactivar o cambiar rol a un usuario, confirme si tiene sesiones activas o procesos dependientes.
- No reprocesse snapshots si no conoce el origen del `snapshotId` o el contexto operativo.
- No habilite CORS amplio en produccion si no hay frontends concretos autorizados.

## 5. Inventario completo de endpoints relevantes para el superusuario

## 5.1 Endpoints de acceso y sesiones

### `POST /api/v1/auth/login`

Uso:

- iniciar sesion,
- obtener `token` y `refreshToken`.

Campos requeridos:

- `username`: 3 a 50 caracteres,
- `password`: 8 a 128 caracteres.

Errores habituales:

- `401` por credenciales invalidas,
- `403` por cuenta inactiva,
- `423` por lockout,
- `429` por rate limit.

### `POST /api/v1/auth/refresh`

Uso:

- renovar la sesion con refresh token.

Comportamiento critico:

- rota el refresh token,
- si detecta reuse de un refresh token revocado, invalida todas las sesiones del usuario.

### `POST /api/v1/auth/logout`

Uso:

- cerrar sesion del usuario autenticado.

Comportamiento critico:

- incrementa `TokenVersion`,
- revoca todos los refresh tokens activos de ese usuario,
- no cierra solo una sesion, cierra todas.

## 5.2 Endpoints de administracion de usuarios

### `POST /api/v1/auth/register`

Autorizacion:

- `Admin` y `UserManager`.

Alcance real para `Admin`:

- puede crear `User`, `SyncOperator`, `UserManager` y `Admin`.

Alcance real para `UserManager`:

- puede crear solo `User` y `SyncOperator`.

Campos de entrada:

- `username`: requerido, 3 a 50 caracteres,
- `password`: requerida, 8 a 128 caracteres,
- `email`: opcional, maximo 150 caracteres,
- `fullName`: opcional, maximo 100 caracteres,
- `role`: opcional, por defecto `User`.

Valores de rol validos:

- `User`
- `SyncOperator`
- `UserManager`
- `Admin`

Errores habituales:

- `400` por rol invalido,
- `400` por usuario duplicado,
- `403` cuando un `UserManager` intenta crear un rol fuera de su alcance.

Ejemplo para crear un operador de sync:

```json
{
  "username": "sync-operador-01",
  "password": "Password123!",
  "email": "sync01@empresa.com",
  "fullName": "Operador de Sincronizacion",
  "role": "SyncOperator"
}
```

Ejemplo para crear otro administrador:

```json
{
  "username": "admin-secundario",
  "password": "Password123!",
  "email": "admin2@empresa.com",
  "fullName": "Administrador Secundario",
  "role": "Admin"
}
```

### `GET /api/v1/auth/users`

Autorizacion:

- `Admin` y `UserManager`.

Uso:

- listar el inventario de usuarios visibles para administracion.

Campos devueltos por usuario:

- `id`,
- `username`,
- `email`,
- `fullName`,
- `role`,
- `isActive`,
- `createdAt`,
- `lastLoginAt`,
- `lockoutUntil`.

### `GET /api/v1/auth/users/{userId}`

Autorizacion:

- `Admin` y `UserManager`.

Uso:

- consultar el detalle administrativo de una cuenta puntual.

### `PATCH /api/v1/auth/users/{userId}/access`

Autorizacion:

- solo `Admin`.

Uso:

- cambiar rol,
- activar o desactivar una cuenta.

Reglas tecnicas:

- no puede desactivar su propio usuario,
- si cambia el rol o el estado activo, la API incrementa `TokenVersion`,
- si cambia el acceso, la API revoca todos los refresh tokens activos del usuario afectado,
- si el cambio no altera rol ni estado, no hay invalidacion de sesiones.

Ejemplo:

```json
{
  "role": "UserManager",
  "isActive": true
}
```

Consecuencia importante:

- hoy no existe un endpoint dedicado para forzar logout de otro usuario sin cambiar su acceso. Si necesita invalidar sus sesiones, debe provocar un cambio real de rol o de estado activo.

## 5.3 Endpoints operativos de la plataforma

### `POST /api/v1/admin/sync/run`

Autorizacion:

- `Admin` y `SyncOperator`.

Uso:

- lanzar una sincronizacion manual contra la fuente DGII.

Devuelve:

- `snapshotId`,
- `insertedCount`,
- `updatedCount`,
- `deactivatedCount`,
- `status`,
- `errorMessage` cuando aplica.

Estados posibles mas relevantes:

- `Success`,
- `NoChanges`,
- `Reprocessed`,
- `Failed`,
- `Skipped - Already Running`.

### `POST /api/v1/admin/sync/reprocess/{snapshotId}`

Autorizacion:

- `Admin` y `SyncOperator`.

Uso:

- reprocesar un snapshot ya archivado.

Respuestas especiales:

- `404` si el snapshot no existe,
- `409` si el snapshot existe pero falta el archivo archivado.

Limitacion actual importante:

- la API no expone un endpoint para listar snapshots o descubrir `snapshotId` historicos. Para reprocesar snapshots anteriores debe obtener ese identificador desde respuestas de sync previas, base de datos, logs operativos o herramientas administrativas externas.

### `GET /api/v1/system/sync-status`

Autorizacion:

- publica.

Uso:

- consultar el ultimo estado persistido del job de sincronizacion.

Nota:

- aunque es util para usted como superusuario, no es un endpoint exclusivo de `Admin`.

## 5.4 Endpoints de consulta autenticada

Como `Admin`, tambien puede usar todos los endpoints funcionales de lectura:

- `GET /api/v1/rncs/{rnc}`
- `GET /api/v1/rncs?term=...`
- `GET /api/v1/rncs/{rnc}/changes`

Reglas relevantes:

- la busqueda por texto exige al menos 3 caracteres,
- la busqueda numerica se trata como RNC exacto,
- la paginacion soporta `page/pageSize` o `cursor`,
- el modo cursor se inicia con `cursor=` vacio.

## 5.5 Endpoints publicos y de soporte

- `GET /`: informacion resumida del servicio.
- `GET /health`: redireccion a `health/live`.
- `GET /health/live`: disponibilidad basica de la aplicacion.
- `GET /health/ready`: disponibilidad de dependencias.
- `GET /swagger`: documentacion Swagger cuando `Swagger__Enabled=true`.

## 6. Reglas de sesion, seguridad y bloqueo

## 6.1 Claims y validacion

Los access tokens JWT se validan por:

- issuer,
- audience,
- expiracion,
- firma,
- coincidencia de `token_version` con base de datos.

Eso significa que un token no solo deja de servir cuando expira; tambien deja de servir cuando el usuario cierra sesion, cambia de acceso o su `TokenVersion` cambia por otra razon operativa.

## 6.2 Lockout por intentos fallidos

Valores actuales por defecto:

- maximo de intentos fallidos: `5`,
- ventana de bloqueo: `15` minutos.

Si una cuenta supera el umbral, la API responde `423 Locked` hasta que pase el tiempo configurado o un cambio de acceso limpie el lockout.

## 6.3 Rate limiting actual

Valores actuales por defecto:

- login: `20` solicitudes por `5` minutos por IP,
- refresh: `15` solicitudes por `5` minutos por usuario o IP,
- gestion de usuarios: `20` solicitudes por `10` minutos por usuario o IP,
- lecturas RNC: `120` solicitudes por minuto por usuario o IP,
- sync administrativo: `4` solicitudes por `10` minutos por usuario o IP.

## 7. Lo que el superusuario SI puede hacer

- crear cuentas de cualquier rol,
- activar o desactivar usuarios,
- cambiar roles,
- operar consultas funcionales,
- ejecutar sync manual,
- reprocesar snapshots existentes,
- revisar estado de sincronizacion,
- usar Swagger y health checks,
- revocar sesiones propias con logout,
- invalidar sesiones ajenas cuando altera realmente el acceso del usuario.

## 8. Lo que el superusuario NO puede hacer hoy por API

- eliminar usuarios fisicamente,
- resetear contrasenas por endpoint,
- cambiar la contrasena de otro usuario por endpoint,
- listar snapshots historicos por endpoint,
- forzar logout de otro usuario sin cambiar su rol o su estado activo,
- desactivar su propio usuario con el endpoint de acceso.

## 9. Procedimiento recomendado de administracion de usuarios

### Alta de una cuenta nueva

1. Defina el rol minimo necesario.
2. Cree la cuenta con `POST /api/v1/auth/register`.
3. Verifique la cuenta con `GET /api/v1/auth/users` o `GET /api/v1/auth/users/{userId}`.
4. Entregue credenciales iniciales por un canal seguro.

### Cambio de acceso

1. Identifique el `userId` del usuario.
2. Aplique `PATCH /api/v1/auth/users/{userId}/access`.
3. Considere que las sesiones previas quedaran invalidadas solo si hubo cambio efectivo.
4. Si desactiva una cuenta, documente el motivo operativo fuera de la API.

### Retiro operativo de una cuenta

Como no existe borrado logico dedicado ni delete fisico por API, la practica correcta es:

1. cambiar `isActive` a `false`,
2. conservar el registro para trazabilidad,
3. no reutilizar usernames de cuentas retiradas.

## 10. Procedimiento recomendado de operacion de datos

### Sync manual

Use `POST /api/v1/admin/sync/run` cuando:

- necesite adelantar una carga fuera del horario del worker,
- quiera verificar disponibilidad de DGII,
- necesite regenerar cache e indices funcionales despues de una incidencia.

### Reproceso

Use `POST /api/v1/admin/sync/reprocess/{snapshotId}` cuando:

- tenga un snapshot previo valido,
- necesite reconstruir el estado desde un archivo archivado,
- quiera repetir un procesamiento sin redescargar la fuente.

No use reproceso si:

- no conoce el origen del `snapshotId`,
- no tiene garantia de que el archivo archivado exista,
- el problema real es de configuracion, no de datos.

## 11. Configuracion y secretos que debe conocer

Como superusuario tecnico u operador principal, debe saber que la API depende de estas configuraciones criticas:

- `ConnectionStrings__DefaultConnection`
- `JWT__SECRETKEY`
- `ConnectionStrings__Valkey` si se usa cache distribuido
- `Bootstrap__*` para inicializacion privilegiada
- `Swagger__Enabled` para exponer Swagger fuera de Development
- `SyncArchive__RootPath` para asegurar reproceso durable
- `Cors__AllowedOrigins__*` para frontends web

Si falta `ConnectionStrings__DefaultConnection` o `JWT__SECRETKEY`, la API no debe considerarse desplegable en produccion.

## 12. Checklist minimo del superusuario

Antes de declarar la plataforma operativa, confirme:

- existe al menos un `Admin` valido,
- el bootstrap ya no es la unica via de acceso,
- los secrets de produccion estan configurados,
- `/health/live` y `/health/ready` responden,
- `/swagger` esta disponible si desea documentacion en linea,
- las cuentas de terceros tienen el minimo privilegio posible,
- conoce como obtener `snapshotId` si necesita reproceso,
- no hay `UserManager` activos si usted desea monopolio total sobre el alta de usuarios.

## 13. Conclusiones operativas clave

- El superusuario real del sistema es `Admin`.
- `Admin` tiene control total, pero no es el unico rol que puede registrar usuarios en la implementacion actual.
- Si desea exclusividad total sobre altas, la medida correcta hoy es no delegar `UserManager`.
- La API ofrece control fuerte de sesiones por `TokenVersion` y refresh tokens revocables.
- El sistema aun tiene limitaciones administrativas relevantes, especialmente en reseteo de contrasenas y descubrimiento de snapshots.