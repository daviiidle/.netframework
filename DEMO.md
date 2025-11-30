# Government Framework Demo Guide

## Quick Start

### Prerequisites
- .NET 10.0 SDK installed
- Docker Desktop running (for RabbitMQ mode)

### Option 1: In-Memory Mode (No Dependencies)

```bash
# Terminal 1: Run Publisher
dotnet run --project src/Publisher

# Terminal 2: Run Integration Service
dotnet run --project src/IntegrationService
```

**What happens:**
- Publisher creates 5 valid messages in memory
- Integration Service processes all messages
- No external dependencies required
- Perfect for quick testing and development

### Option 2: RabbitMQ Mode (Production-like)

```bash
# Step 1: Start RabbitMQ
docker compose up -d

# Step 2: Run Publisher (Terminal 1)
dotnet run --project src/Publisher -- --rabbitmq

# Step 3: Run Integration Service (Terminal 2)
dotnet run --project src/IntegrationService -- --rabbitmq

# Step 4: View RabbitMQ Management UI
# Open browser: http://localhost:15672
# Login: guest / guest
```

**What happens:**
- Publisher sends messages to RabbitMQ queue
- Messages are persisted in RabbitMQ broker
- Integration Service consumes from RabbitMQ
- Full message durability and reliability
- Can monitor queues via web UI

---

## Files Created During Execution

### Databases (SQLite)
- **messages.db** - Processed messages stored by DatabaseService
- **audit.db** - Audit logs with processing times, retry counts, and statistics

### Logs
- **errors.log** - Error details for failed message processing attempts

### Persistence (Future Feature)
- **unprocessed_messages.json** - Backup of unprocessed messages (disaster recovery)

---

## Publisher Output Example

```
Publisher starting...

Using RabbitMQ queue
Connected to RabbitMQ at localhost:5672

Publishing messages...
==================================================
✓ Published message 1: a1b2c3d4-... from System1
✓ Published message 2: e5f6g7h8-... from System2
✓ Published message 3: i9j0k1l2-... from System3
✓ Published message 4: m3n4o5p6-... from System4
✓ Published message 5: q7r8s9t0-... from System5

Attempting to publish invalid message...
✗ Message validation failed: Source and Payload cannot be empty

Attempting to publish duplicate message...
✓ Published message: 00000000-0000-0000-0000-000000000001
✗ Duplicate message rejected: Message ID already exists

==================================================
Queue depth: 6 message(s)
DLQ depth: 0 message(s)

Publisher completed successfully!
```

---

## Integration Service Output Example

```
Integration Service starting...

Using RabbitMQ queue
Connected to RabbitMQ at localhost:5672

Processing queued messages...
==================================================
Messages in queue: 6

✓ Processed message 1
✓ Processed message 2
✓ Processed message 3
✓ Processed message 4
✓ Processed message 5
✓ Processed message 6

==================================================
Processing complete!
  Processed: 6 message(s)
  Failed: 0 message(s)
  Remaining in queue: 0
  Dead letter queue: 0 message(s)

Audit Statistics:
--------------------------------------------------
  Total processed: 6
  Success count: 6
  Failure count: 0
  Success rate: 100.00%
  Avg duration: 15.42 ms
  Min duration: 12.30 ms
  Max duration: 18.50 ms

Recent Audit Logs:
--------------------------------------------------
  [Completed] a1b2c3d4-... - 15.20ms
  [Completed] e5f6g7h8-... - 14.80ms
  [Completed] i9j0k1l2-... - 16.10ms
  [Completed] m3n4o5p6-... - 15.00ms
  [Completed] q7r8s9t0-... - 17.40ms

Integration Service completed successfully!
```

---

## Inspecting the Results

### 1. Check RabbitMQ UI
```bash
# Open browser
http://localhost:15672

# Navigate to Queues tab
# - View queue depth
# - See message rates
# - Monitor DLQ
```

### 2. Query Audit Database
```bash
# Install SQLite CLI tool, or use DB Browser for SQLite
sqlite3 audit.db

# View all audit logs
SELECT * FROM AuditLogs;

# View statistics
SELECT
    COUNT(*) as Total,
    SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END) as Success,
    SUM(CASE WHEN Status = 'Failed' THEN 1 ELSE 0 END) as Failed,
    AVG(DurationMs) as AvgDuration
FROM AuditLogs;
```

### 3. Check Messages Database
```bash
sqlite3 messages.db

# View processed messages
SELECT * FROM ProcessedMessages;
```

### 4. View Error Logs
```bash
# View error log file
cat errors.log

# Or on Windows
type errors.log
```

---

## Testing Features

### Run All Tests
```bash
dotnet test
```

### Run Only RabbitMQ Tests
```bash
# Make sure RabbitMQ is running first
docker compose up -d

# Run tests
dotnet test --filter RabbitMQTests
```

### Run Specific Test Categories
```bash
# Audit tests
dotnet test --filter AuditServiceTests

# Circuit breaker tests
dotnet test --filter CircuitBreakerTests

# Retry policy tests
dotnet test --filter RetryPolicyTests
```

---

## Cleanup

### Stop RabbitMQ
```bash
docker compose down

# Or to remove volumes too
docker compose down -v
```

### Remove Generated Files
```bash
# Windows PowerShell
Remove-Item *.db, *.log, *.json -ErrorAction SilentlyContinue

# Linux/Mac
rm -f *.db *.log *.json
```

---

## Architecture Overview

```
┌─────────────┐
│  Publisher  │
└──────┬──────┘
       │
       ▼
┌─────────────────┐
│  Message Queue  │ ◄─── In-Memory or RabbitMQ
│  - Main Queue   │
│  - DLQ          │
└──────┬──────────┘
       │
       ▼
┌──────────────────────┐
│ IntegrationService   │
│ ┌──────────────────┐ │
│ │ IntegrationWorker│ │
│ │  - Transformer   │ │
│ │  - RetryPolicy   │ │
│ │  - CircuitBreaker│ │
│ │  - ErrorLogger   │ │
│ │  - AuditService  │ │
│ └──────────────────┘ │
└───────┬──────────────┘
        │
        ▼
┌──────────────────┐
│   Persistence    │
│ - messages.db    │
│ - audit.db       │
│ - errors.log     │
└──────────────────┘
```

---

## Features Demonstrated

### Message Processing
- ✅ Message validation
- ✅ Duplicate detection
- ✅ Message transformation
- ✅ Database persistence

### Reliability
- ✅ Retry logic with exponential backoff
- ✅ Circuit breaker pattern
- ✅ Dead letter queue
- ✅ Error logging

### Monitoring
- ✅ Audit logging with timestamps
- ✅ Processing duration tracking
- ✅ Success/failure statistics
- ✅ Queue depth monitoring

### Integration
- ✅ In-Memory queue (testing)
- ✅ RabbitMQ integration (production)
- ✅ SQLite databases
- ✅ File-based logging

---

## Troubleshooting

### RabbitMQ Connection Failed
```
Error: Failed to connect to RabbitMQ
Solution: docker compose up -d
```

### Tests Failing
```
Error: RabbitMQ tests failing
Solution: Ensure RabbitMQ is running before tests
```

### Database Locked
```
Error: Database is locked
Solution: Close all connections, delete .db files, restart
```

### Port Already in Use
```
Error: Port 5672 or 15672 already in use
Solution: Stop other RabbitMQ instances or change ports in docker-compose.yml
```
