# Production readiness

This repository is designed to run with Docker Compose. The GitHub workflows build, test, scan, and publish the application images; they do not create cloud infrastructure.

## Release flow

1. Merge a pull request into `main`.
2. Confirm CI and image publishing pass.
3. Create a version tag, for example `v1.1.0`.
4. Push the tag to GitHub.
5. Deploy the same image tag to staging, validate it, and then request production approval.

Never deploy the moving `main` tag to production when a version tag is available.

## Required production controls

- Use separate staging and production values for SQL Server, RabbitMQ, Redis, and JWT.
- Store credentials only as GitHub environment secrets or in an external secret manager.
- Restrict production deployments to `main` and require an environment approval.
- Back up both application databases before migrations and test restoring a backup.
- Keep the previous image tag available for rollback.
- Monitor API health endpoints, container restarts, database connectivity, RabbitMQ queue depth, and Redis availability.

## Rollback

Deploy the last known-good image tag through the production workflow, for example `v1.0.0`. Do not rebuild an old commit during an incident; deploy the existing immutable image.

## RabbitMQ failures

The inventory consumer retries broker connection startup. Messages that fail during processing are rejected without requeue and routed to:

```text
inventory.order-created.dead-letter
```

Before replaying a dead-letter message, identify and fix the cause, then re-publish it deliberately using an approved operational procedure. A production system should add authenticated DLQ inspection and replay tooling before relying on this queue operationally.

## Remaining infrastructure work

Actual staging/production hosting, firewall rules, TLS, backups, secret storage, monitoring alerts, and restore drills require a deployment target and cannot be completed by GitHub configuration alone.
