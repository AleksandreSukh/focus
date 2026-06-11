# Senior .NET Backend Interview Questionnaires

Source index: `C:\Users\aleks\Downloads\Refresh knowledge.txt`

Use this as a focused refresh workbook. Do one part at a time. For each question, answer out loud or write 3-6 bullets before checking notes.

Scoring:

- `0` - I cannot answer without rereading.
- `1` - I know the idea, but the answer is vague or misses tradeoffs.
- `2` - I can explain it clearly, include tradeoffs, and connect it to production experience.

Weak spot rule:

- Any `0` is a learning target.
- Any repeated `1` in the same topic means drill that topic before moving on.
- A senior-level answer should usually include tradeoffs, failure modes, and how to verify the solution in production.

## Progress ToDo

- [ ] Part 1 - Pagination, filtering, and data-access performance
- [ ] Part 2 - HTTP lifecycle, REST, and API design
- [ ] Part 3 - ASP.NET Core request pipeline, middleware, and authentication
- [ ] Part 4 - C#, .NET runtime, async, memory, and threading
- [ ] Part 5 - SQL, indexes, transactions, isolation, and locking
- [ ] Part 6 - EF Core and ORM tradeoffs
- [ ] Part 7 - Caching, Redis, cache invalidation, and consistency
- [ ] Part 8 - System design interview workflow
- [ ] Part 9 - Scalability, availability, replication, sharding, and CAP
- [ ] Part 10 - Messaging, event-driven architecture, and background jobs
- [ ] Part 11 - Web app building blocks: load balancers, CDNs, API gateways, reverse proxies
- [ ] Part 12 - CI/CD and deployment strategies
- [ ] Part 13 - Containers, Kubernetes, and cloud infrastructure
- [ ] Part 14 - Observability: logging, metrics, tracing, alerting, and incident response
- [ ] Part 15 - Security: OAuth, JWT, cookies, OWASP, secrets, and data protection
- [ ] Part 16 - Search systems and Elasticsearch
- [ ] Part 17 - Design patterns, SOLID, architecture, and DDD basics
- [ ] Part 18 - Testing strategy: unit, integration, contract, load, and resiliency tests
- [ ] Part 19 - Senior behavioral topics: ownership, tradeoffs, mentoring, and delivery

## Part 1 - Pagination, Filtering, And Data-Access Performance

Goal: Check whether you can discuss pagination as an API, database, product, and scalability concern. This topic often reveals whether someone thinks beyond `Skip()` and `Take()`.

### 1. Baseline Concepts

Score each question: `0 / 1 / 2`

1. Explain offset-based pagination. What does `page=10&pageSize=50` usually translate to in SQL?
2. What are the main benefits of offset-based pagination?
3. What are the main performance problems with large offsets?
4. Explain cursor-based pagination in plain language.
5. Explain keyset pagination. How is it different from generic cursor pagination?
6. Explain time-based pagination. When is it useful and when is it dangerous?
7. What is the difference between `WHERE Id > @lastId ORDER BY Id` and `OFFSET @n ROWS FETCH NEXT @m ROWS ONLY`?
8. Why does pagination require deterministic ordering?
9. What can go wrong if you paginate by a non-unique column like `CreatedAt` only?
10. How would you make pagination stable when multiple rows have the same `CreatedAt` value?

### 2. Senior-Level Tradeoffs

1. A product manager asks for "go to page 582" in a table with 100 million rows. How do you evaluate whether to support it?
2. When is offset pagination acceptable even if it is not the most scalable option?
3. What user experience tradeoffs come with cursor/keyset pagination?
4. Why can records be skipped or duplicated while paginating through a changing dataset?
5. How do inserts and deletes affect offset pagination?
6. How do inserts and deletes affect keyset pagination?
7. What information would you encode in an API cursor?
8. Should cursors be opaque to clients? Why?
9. What security or correctness risks exist if clients can edit cursor contents?
10. How would you handle reverse pagination, such as "previous page", with keyset pagination?

### 3. SQL And Indexing

1. Given `WHERE TenantId = @tenantId AND CreatedAt < @createdAt ORDER BY CreatedAt DESC, Id DESC LIMIT 50`, what index would you consider?
2. Why should the index match the filter and sort pattern?
3. What is a covering index?
4. When can a covering index hurt more than it helps?
5. How would you diagnose a slow paginated query in production?
6. What does an execution plan tell you?
7. What is the difference between an index seek and an index scan?
8. How can filtering after pagination produce incorrect results?
9. How should filtering, sorting, and pagination be ordered logically?
10. What is the risk of allowing clients to sort by arbitrary columns?

### 4. .NET And EF Core Practical Questions

1. How would you implement offset pagination in EF Core?
2. How would you implement keyset pagination in EF Core?
3. Why is `AsNoTracking()` often useful for read-only paginated endpoints?
4. When should you avoid returning EF entities directly from API endpoints?
5. What is the N+1 query problem?
6. How can pagination hide or worsen an N+1 problem?
7. How do projection queries with `Select(...)` help performance?
8. What is the danger of calling `.ToList()` too early before applying filters and pagination?
9. How do `IQueryable<T>` and `IEnumerable<T>` differ in this context?
10. How would you test that an EF Core endpoint does not accidentally load too much data?

### 5. API Design

1. Design a REST endpoint for listing orders with filtering, sorting, and pagination.
2. What response metadata would you include for offset pagination?
3. What response metadata would you include for cursor pagination?
4. Would you include total count? Why or why not?
5. Why can total count be expensive?
6. How would you cap `pageSize`, and what default would you choose?
7. How should the API respond to invalid page size, invalid cursor, or unsupported sort?
8. How do you keep pagination contracts backward compatible?
9. How would you document pagination behavior for frontend or mobile clients?
10. What metrics would you track for paginated endpoints?

### 6. System Design Scenarios

1. Design pagination for a news feed with frequent inserts.
2. Design pagination for an admin audit log where accuracy matters more than real-time freshness.
3. Design pagination for search results backed by Elasticsearch.
4. Design pagination for a multi-tenant SaaS table where each tenant has millions of records.
5. Design pagination for a public API consumed by third-party integrations.
6. How would caching interact with paginated list endpoints?
7. What happens to pagination when data is sharded?
8. How would you paginate across multiple shards?
9. How does eventual consistency affect paginated reads?
10. What would you do if clients need export-all behavior instead of interactive pagination?

### 7. Mini Exercises

Exercise A - Explain:

You see this code in a production endpoint:

```csharp
var orders = await db.Orders
    .Where(x => x.CustomerId == customerId)
    .OrderByDescending(x => x.CreatedAt)
    .Skip(page * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

What questions would you ask before changing it?

Exercise B - Redesign:

The endpoint above becomes slow when customers have more than 500,000 orders. Propose a new API contract and query shape.

Exercise C - Debug:

Users report that items sometimes appear twice between pages. List the most likely causes and how you would prove each one.

Exercise D - Production readiness:

Define logs, metrics, alerts, tests, and database checks you would add before shipping the new pagination implementation.

### 8. Strong Answer Checklist

For this part, a strong senior answer should mention most of these:

- Stable ordering with a unique tie-breaker, for example `CreatedAt DESC, Id DESC`.
- Offset pagination is simple and supports page numbers, but gets slower and less stable at large offsets.
- Keyset pagination is efficient for next/previous navigation but does not naturally support arbitrary page jumps.
- Cursor values should usually be opaque, validated, and versioned.
- Indexes should match tenant/filter/sort patterns.
- Total counts can be expensive and are not always needed.
- EF Core queries should filter, order, project, and paginate before materializing.
- `AsNoTracking()` and DTO projections are useful for read-heavy endpoints.
- Production diagnosis should include execution plans, query duration, rows scanned, index usage, and endpoint metrics.
- Changing datasets can cause duplicates or missing records unless the design accounts for ordering and consistency.

### 9. Self-Assessment Notes

Write your scores and weak spots here:

```text
Baseline Concepts:
Senior-Level Tradeoffs:
SQL And Indexing:
.NET And EF Core:
API Design:
System Design:
Mini Exercises:

Top 3 weak spots:
1.
2.
3.

Next repair actions:
1.
2.
3.
```

Next part to generate: Part 2 - HTTP lifecycle, REST, and API design.
