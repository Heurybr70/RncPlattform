# Historial del Proyecto: RncPlatform

Este documento sirve como registro oficial de todo lo que se analizó, planificó y ejecutó durante el proceso inicial de construcción de este proyecto, para futura referencia.

## 1. El Objetivo Inicial (Requerimientos Fundamentales)
Se estableció el objetivo de construir una Web API de forma autónoma llamada **RncPlatform** que:
- Sincronice diariamente el padrón RNC desde la fuente oficial de la DGII (un archivo ZIP/TXT desde la web oficial).
- Permita la consulta por RNC exacto, así como búsqueda por Nombre o Razón Social.
- Opere usando **SQL Server** como fuente de verdad y **Valkey (Redis)** como caché distribuido para lecturas rápidas.
- Genere y almacene un registro histórico de cambios a través de **Snapshots** y **ChangeLogs**.

## 2. Principios de Arquitectura Tomados
Se solicitó y se respetó estrictamente:
- **Clean Architecture Ligera / Modular Monolith:** No microservicios prematuros.
- **División Estricta:** Separación entre API, Application, Domain, Infrastructure y Contracts.
- Lógica de negocio orquestada en **Application Services**, dejando los **Controllers** delgados.
- Operación tolerante a fallos, idempotente y escalable en torno al worker encargado de la descarga y staging.
- Endpoint stateless.

## 3. Tecnologías Core Escogidas e Instaladas
- **.NET 10** (Vía EF Core, Minimal Controllers).
- **SQL Server** para Persistencia Relacional.
- **Valkey** vía `StackExchange.Redis` para Cache-Aside (`rnc:{numero}`).
- **OpenTelemetry** listo en `Program.cs` y paquetes instalados para trazabilidad a futuro.
- **Serilog** configurado en `Program.cs` y `appsettings.json` para logs estructurados.

## 4. Estructura de Entidades Creadas (Domain Layer)
Se crearon todas las entidades mandatorias:
- `Taxpayer` (RNC principal, versión y control de visibilidad en el último snapshot).
- `RncSnapshot` (Log del job de ejecución, archivo, hashes y conteos crudos).
- `RncChangeLog` (Seguimiento de auditoría de qué RNC fue agregado, acualizado o removido).
- `SyncJobState` (Seguimiento temporal para dashboard de jobs asíncronos).
- `RncStaging` (Tabla puramente de trabajo en ráfagas para asimilar el `.txt` crudo y hacer Merge SQL).
- `DistributedLock` (Candado sobre SQL Server para garantizar que el Sync asíncrono no colisione con ejecuciones manuales concurrentes).

## 5. El Proceso de Sincronización Orquestado (Infrastructure & Application)
Puesto en las interfaces `IRncSyncService`, implementado de la siguiente manera:
1. Adquirir candado `AcquireLockAsync` para "RncPlatform.Sync".
2. Registrar un Snapshot con estado "Running".
3. Consumidor HTTP (Downloader) extrae ZIP temporal, comprueba contenido y devuelve Path local al archivo de texto puro.
4. Si el Hash del archivo coincide con el del último éxito, aborta ahorrando recursos (NoChanges).
5. Un parser asíncrono en lotes (Stream) itera línea a línea leyendo ANSI, e inyecta miles de records hacia el Staging DB.
6. [Pre-Merge SQL] Lógica base lista para hacer Upserts en el Taxpayer original actualizando `UpdatedAt`.
7. Actualizar el Dashboard de Jobs. Borro staging previo. Libero el Candado.
8. BackgroundWorker de infraestructura (.NET HostedService) corre oculto mapeado a levantar esto a las 4 AM UTC cada día.

Todo esto está acoplado de extremo a extremo sin que rompa Clean Architecture.

## 6. Las Fases Ejecutadas Durante la Creación
- **Fase 1**: Scaffolding (`dotnet new sln`, proyectos, paquetes nuget, links).
- **Fase 2**: Modelado C# de entidades de EF según esquema, armado de Contratos y abstracciones.
- **Fase 3**: Infrastructure implementada (DbContext, FluentAPI map, Repositorios, Downloaders y Lock Service).
- **Fase 4**: Casos de uso asambleados (`RncSyncService`, `RncQueryService`).
- **Fase 5**: Armado de Controllers en la API, Inyección de Dependencias centralizada, y Archivo general de inicio.

## Siguientes Pasos Pendientes para Continuar el Desarrollo:
En un futuro, basado en las guías técnicas del requerimiento:
- Terminar la optimización real de Bulk SQL Copy (MERGE Statement directo a base de datos de EF).
- Ampliar el módulo Auth / API Key (Fase Segura).
- Levantar los endpoints hacia un panel Admin o de Reportería de Fallos.
- Ejecutar migraciones iniciales (`update-database`).
