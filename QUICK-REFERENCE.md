# 🚀 Quick Reference Card

## One-Command Validation
```powershell
# Windows
.\validate-system.ps1

# Linux/Mac
./validate-system.sh
```

---

## Common Commands

### Testing
```bash
dotnet test                              # Run all 120 tests
dotnet test --filter RabbitMQTests       # RabbitMQ tests only
dotnet test --filter AuditServiceTests   # Audit tests only
```

### In-Memory Mode
```bash
dotnet run --project src/Publisher
dotnet run --project src/IntegrationService
```

### RabbitMQ Mode
```bash
docker compose up -d
dotnet run --project src/Publisher -- --rabbitmq
dotnet run --project src/IntegrationService -- --rabbitmq
```

### RabbitMQ Management
```bash
docker compose up -d       # Start RabbitMQ
docker compose ps          # Check status
docker compose logs        # View logs
docker compose down        # Stop RabbitMQ
docker compose down -v     # Stop and remove volumes
```

### Database Queries
```bash
# Count processed messages
sqlite3 messages.db "SELECT COUNT(*) FROM ProcessedMessages;"

# View audit statistics
sqlite3 audit.db "SELECT
    COUNT(*) as Total,
    SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END) as Success,
    ROUND(AVG(DurationMs), 2) as AvgDuration
FROM AuditLogs WHERE DurationMs IS NOT NULL;"

# View recent audit logs
sqlite3 audit.db "SELECT * FROM AuditLogs ORDER BY StartTime DESC LIMIT 10;"

# View all messages
sqlite3 messages.db "SELECT * FROM ProcessedMessages;"
```

### Log Files
```bash
cat errors.log     # Linux/Mac
type errors.log    # Windows
```

---

## URLs

- **RabbitMQ UI:** http://localhost:15672
  - Username: `guest`
  - Password: `guest`

---

## File Locations

```
messages.db                    # Processed messages
audit.db                       # Audit logs with metrics
errors.log                     # Error details
unprocessed_messages.json      # (Future) Disaster recovery
```

---

## Test Coverage

| Total Tests | Passing | Failing | Coverage |
|-------------|---------|---------|----------|
| 120         | 120     | 0       | 100%     |

---

## Project Stats

- **Lines of Code:** ~2,500
- **Test Files:** 14
- **Components:** 13
- **Patterns:** 8+
- **Technologies:** .NET 10, RabbitMQ, SQLite

---

## Architecture (ASCII)

```
Publisher → Queue → IntegrationService → Database
                ↓
              DLQ (Failed Messages)
                ↓
           Error Logs + Audit
```

---

## Troubleshooting Quick Fixes

| Problem | Solution |
|---------|----------|
| RabbitMQ connection failed | `docker compose up -d` |
| Tests failing | Ensure RabbitMQ running |
| Database locked | Close connections, delete .db |
| Port 5672 in use | Stop other RabbitMQ instances |
| Port 15672 in use | Change in docker-compose.yml |

---

## Cleanup Commands

```bash
# Remove databases and logs
rm *.db *.log *.json             # Linux/Mac
Remove-Item *.db, *.log, *.json  # PowerShell

# Stop and remove RabbitMQ
docker compose down -v
```

---

## Build & Run

```bash
dotnet build                     # Build solution
dotnet clean                     # Clean build artifacts
dotnet restore                   # Restore packages
```

---

## Key Features Checklist

- [x] Message Queue (In-Memory + RabbitMQ)
- [x] Retry with Exponential Backoff
- [x] Circuit Breaker
- [x] Dead Letter Queue
- [x] Error Logging
- [x] Audit Logging with Metrics
- [x] Database Persistence
- [x] 120 Automated Tests
- [x] Docker Compose Setup
- [x] Demo Applications

---

## Package Dependencies

```xml
<PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.0" />
<PackageReference Include="RabbitMQ.Client" Version="6.8.1" />
<PackageReference Include="xunit" Version="2.9.0" />
```

---

## Environment Variables (Optional)

```bash
# RabbitMQ
RABBITMQ_HOST=localhost
RABBITMQ_PORT=5672
RABBITMQ_USER=guest
RABBITMQ_PASS=guest

# Database
DB_CONNECTION_STRING=Data Source=messages.db

# Logging
LOG_FILE_PATH=errors.log
```

---

*For detailed information, see README.md, DEMO.md, or SUMMARY.md*
