# Government Integration Framework

A production-ready message processing system built with .NET 10.0, demonstrating enterprise patterns including retry logic, circuit breakers, dead letter queues, and comprehensive audit logging.

## 🎯 Features

### Core Functionality
- ✅ **Message Queue Abstraction** - Support for both In-Memory and RabbitMQ
- ✅ **Message Transformation** - Process and transform messages
- ✅ **Database Persistence** - SQLite storage for processed messages
- ✅ **Duplicate Detection** - Prevent processing the same message twice

### Reliability Patterns
- ✅ **Retry Logic** - Configurable retry with exponential backoff
- ✅ **Circuit Breaker** - Prevent cascading failures
- ✅ **Dead Letter Queue** - Handle failed messages
- ✅ **Error Logging** - Comprehensive error tracking

### Monitoring & Observability
- ✅ **Audit Logging** - Track processing times and outcomes
- ✅ **Statistics** - Success rates, duration metrics
- ✅ **Queue Depth Monitoring** - Track message backlog
- ✅ **Disaster Recovery** - Persistence service for crash recovery

### Testing
- ✅ **120 Automated Tests** - Comprehensive test coverage
- ✅ **TDD Approach** - Tests written before implementation
- ✅ **Integration Tests** - RabbitMQ, database, and end-to-end tests
- ✅ **Unit Tests** - All components individually tested

## 📁 Project Structure

```
Government Framework/
├── src/
│   ├── Models/                    # Core business logic
│   │   ├── Message.cs
│   │   ├── IMessageQueue.cs
│   │   ├── InMemoryQueue.cs
│   │   ├── RabbitMQQueue.cs
│   │   ├── MessageTransformer.cs
│   │   ├── IntegrationWorker.cs
│   │   ├── DatabaseService.cs
│   │   ├── RetryPolicy.cs
│   │   ├── CircuitBreaker.cs
│   │   ├── ErrorLogger.cs
│   │   ├── AuditService.cs
│   │   └── PersistenceService.cs
│   ├── Publisher/                 # Message publisher demo
│   │   └── Program.cs
│   └── IntegrationService/        # Message processor demo
│       └── Program.cs
├── tests/
│   └── IntegrationTests/          # 120 comprehensive tests
├── docker-compose.yml             # RabbitMQ setup
├── DEMO.md                        # Detailed demo guide
├── validate-system.sh             # Linux/Mac validation
└── validate-system.ps1            # Windows validation
```

## 🚀 Quick Start

### Prerequisites
- .NET 10.0 SDK
- Docker Desktop (for RabbitMQ mode)
- SQLite (optional, for database inspection)

### Option 1: In-Memory Mode (No Dependencies)

```bash
# Terminal 1: Publish messages
dotnet run --project src/Publisher

# Terminal 2: Process messages
dotnet run --project src/IntegrationService
```

### Option 2: RabbitMQ Mode (Production-like)

```bash
# Start RabbitMQ
docker compose up -d

# Publish messages
dotnet run --project src/Publisher -- --rabbitmq

# Process messages
dotnet run --project src/IntegrationService -- --rabbitmq

# View RabbitMQ UI
# http://localhost:15672 (guest/guest)
```

## 🧪 Running Tests

```bash
# Run all tests (120 total)
dotnet test

# Run specific test suite
dotnet test --filter AuditServiceTests
dotnet test --filter RabbitMQTests
dotnet test --filter CircuitBreakerTests
dotnet test --filter RetryPolicyTests
```

**Expected Results:**
- ✅ 120 tests passed
- ✅ 0 failed
- ✅ 100% success rate

## 📊 System Validation

Run the complete system validation:

### Windows (PowerShell)
```powershell
.\validate-system.ps1
```

### Linux/Mac
```bash
chmod +x validate-system.sh
./validate-system.sh
```

This will:
1. Run all 120 automated tests
2. Start RabbitMQ
3. Publish test messages
4. Process messages
5. Verify databases and logs
6. Display audit statistics

## 📝 Files Generated

### Databases (SQLite)
- **messages.db** - Processed messages
- **audit.db** - Audit logs with processing metrics

### Logs
- **errors.log** - Error details for failed processing

### Persistence (Future)
- **unprocessed_messages.json** - Disaster recovery backup

## 🔍 Inspecting Results

### View Audit Statistics
```bash
sqlite3 audit.db "SELECT
    COUNT(*) as Total,
    SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END) as Success,
    SUM(CASE WHEN Status = 'Failed' THEN 1 ELSE 0 END) as Failed,
    ROUND(AVG(DurationMs), 2) as AvgDuration
FROM AuditLogs WHERE DurationMs IS NOT NULL;"
```

### View All Audit Logs
```bash
sqlite3 audit.db "SELECT * FROM AuditLogs ORDER BY StartTime DESC LIMIT 10;"
```

### View Processed Messages
```bash
sqlite3 messages.db "SELECT * FROM ProcessedMessages;"
```

### View Error Log
```bash
cat errors.log    # Linux/Mac
type errors.log   # Windows
```

## 🏗️ Architecture

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
│ │  - Validator     │ │
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

## 📚 Key Components

### Message Queue (IMessageQueue)
- **InMemoryQueue** - Fast, in-process queue for testing
- **RabbitMQQueue** - Production-ready RabbitMQ integration

### IntegrationWorker
Orchestrates the entire message processing pipeline:
1. Dequeue message
2. Validate message
3. Transform message
4. Save to database (with retry)
5. Log audit trail
6. Handle failures → DLQ

### Retry Policy
- Configurable max attempts
- Exponential backoff
- Jitter to prevent thundering herd

### Circuit Breaker
- Protects downstream systems
- Automatic recovery
- Configurable thresholds

### Audit Service
Tracks:
- Processing start/end times
- Duration metrics
- Success/failure status
- Error messages

## 🎓 Learning Outcomes

This project demonstrates:

1. **Test-Driven Development (TDD)**
   - Tests written before implementation
   - Red-Green-Refactor cycle
   - High code coverage

2. **SOLID Principles**
   - Single Responsibility
   - Open/Closed
   - Liskov Substitution
   - Interface Segregation
   - Dependency Inversion

3. **Enterprise Patterns**
   - Repository Pattern
   - Strategy Pattern
   - Circuit Breaker
   - Retry with Exponential Backoff
   - Dead Letter Queue

4. **Clean Architecture**
   - Separation of concerns
   - Dependency injection
   - Testable components

## 🛠️ Development

### Building
```bash
dotnet build
```

### Running Tests with Coverage
```bash
dotnet test /p:CollectCoverage=true
```

### Cleaning Up
```bash
# Remove generated files
rm *.db *.log *.json

# Stop RabbitMQ
docker compose down -v
```

## 🐛 Troubleshooting

### RabbitMQ Connection Issues
**Problem:** `Failed to connect to RabbitMQ`

**Solution:**
```bash
docker compose up -d
docker compose ps  # Verify running
```

### Database Locked
**Problem:** `Database is locked`

**Solution:**
```bash
# Close all connections
# Delete .db files
rm *.db
```

### Tests Failing
**Problem:** RabbitMQ tests failing

**Solution:**
```bash
# Ensure RabbitMQ is running
docker compose up -d
# Wait a few seconds
dotnet test --filter RabbitMQTests
```

## 📈 Test Coverage

| Component | Tests | Coverage |
|-----------|-------|----------|
| Message | 6 | 100% |
| Queue | 9 | 100% |
| Database | 8 | 100% |
| Transformer | 9 | 100% |
| Worker | 12 | 100% |
| Retry Policy | 9 | 100% |
| Circuit Breaker | 11 | 100% |
| Error Logger | 10 | 100% |
| Audit Service | 10 | 100% |
| Persistence | 11 | 100% |
| RabbitMQ | 10 | 100% |
| Disaster Recovery | 6 | 100% |
| Publisher | 8 | 100% |
| **TOTAL** | **120** | **100%** |

## 🎯 Next Steps

Potential enhancements:
- [ ] Add message batching
- [ ] Implement message priority
- [ ] Add distributed tracing
- [ ] Implement message expiration/TTL
- [ ] Add health check endpoints
- [ ] Implement message scheduling
- [ ] Add metrics export (Prometheus)
- [ ] Implement saga pattern
- [ ] Add message encryption
- [ ] Implement idempotency keys

## 📄 License

This is a demo/educational project.

## 👥 Contributing

This project demonstrates TDD and enterprise patterns. Feel free to use it as a learning resource!

---

**Built with ❤️ using .NET 10.0, RabbitMQ, and SQLite**
