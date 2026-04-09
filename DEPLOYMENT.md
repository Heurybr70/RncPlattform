# RncPlatform Deployment Notes

## Required environment variables

Set these values outside source control in the target environment:

- `ConnectionStrings__DefaultConnection`: SQL Server connection string.
- `Jwt__SecretKey`: required in non-development environments.
- `Jwt__Issuer`: token issuer. Default repo value is `RncPlatform`.
- `Jwt__Audience`: token audience. Default repo value is `RncPlatformClients`.
- `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, ...: allowed browser origins for public clients.
- `Bootstrap__Username`: first privileged user name.
- `Bootstrap__Password`: first privileged user password.
- `Bootstrap__Email`: optional bootstrap email.
- `Bootstrap__FullName`: optional bootstrap display name.
- `Bootstrap__Role`: `Admin` or `UserManager`.
- `Bootstrap__Enabled`: optional switch to disable bootstrap in controlled environments such as integration tests.
- `SyncArchive__RootPath`: absolute directory for archived DGII files. Use an absolute path in production.

Optional but recommended:

- `ConnectionStrings__Valkey`: Redis or Valkey endpoint for distributed cache.
- `Worker__Enabled`: `true` to enable scheduled background syncs.
- `Worker__TargetHourUtc`: UTC hour for the background sync job.
- `Security__LoginMaxFailedAttempts`: account lockout threshold.
- `Security__LoginLockoutMinutes`: lockout duration.
- `Security__LoginRateLimitPermitLimit`: login requests allowed per rate-limit window.
- `Security__LoginRateLimitWindowMinutes`: login rate-limit window.

## First deployment

1. Provision SQL Server and, if running multiple instances, Redis or Valkey.
2. Set the environment variables listed above.
3. Apply migrations:

```powershell
dotnet ef database update --project src/RncPlatform.Infrastructure/RncPlatform.Infrastructure.csproj --startup-project src/RncPlatform.Api/RncPlatform.Api.csproj
```

4. Start the API.
5. Verify `GET /health/live` and `GET /health/ready`.
6. Log in with the bootstrap user and create the remaining privileged accounts.

## Important behavior

- The bootstrap user is only created or promoted when no `Admin` or `UserManager` exists.
- `Bootstrap__Enabled=false` disables that bootstrap flow entirely.
- Remote registration is not public: it requires the `CanManageUsers` policy.
- Admin sync and reprocess endpoints require the `CanRunSync` policy.
- Refresh tokens are persisted and revoked on logout or privilege changes.
- Swagger is only exposed in Development.
- `SyncArchive__RootPath` should point to persistent storage because `reprocess` depends on archived source files.
- `GET /api/v1/rncs/{rnc}` remains the exact lookup path. `GET /api/v1/rncs?term=` now treats numeric terms as exact RNC lookup and name terms as prefix search with at least 3 characters, which is intentionally more index-friendly for production.

## Public exposure checklist

- Terminate TLS at the reverse proxy or load balancer.
- Forward `X-Forwarded-For` and `X-Forwarded-Proto` headers.
- Apply edge or WAF rate limiting in addition to the in-app policies.
- Restrict `Cors__AllowedOrigins__*` to known frontends.
- Monitor `health/live` and `health/ready`.
- Back up the SQL Server database and the archive directory used by `SyncArchive__RootPath`.