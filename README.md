# CognoDB Cloud Benchmark Suite

This repository contains a reproducible benchmark suite that compares **CognoDB Cloud** against other managed and entry-level graph database platforms using the **same dataset**, the **same logical workloads**, and the **smallest available resource tiers**.

The purpose of this benchmark is not to declare a single “winner.” The purpose is to evaluate engineering rigor:

- fair methodology
- reproducible automation
- honest reporting
- clear analysis
- documented caveats
- comparable free-tier or entry-tier resource limits

**Companion article:** https://dev.to/kalbashi09/benchmarking-5-graph-database-platforms-on-the-same-352k-edge-graph-what-free-tiers-hide-39jm

---

## Table of Contents

1. Summary of Results  
2. Platforms Compared  
3. Environment and Instance Specifications  
4. Dataset  
5. Data Loading Method  
6. Indexing Strategy  
7. Benchmark Workloads  
8. Full Results Matrix  
9. Analysis  
10. Methodology and Fairness  
11. Caveats and Limitations  
12. How to Reproduce the Benchmark  
13. Repository Structure  
14. Dependencies  
15. Security and Secrets  
16. Extending the Benchmark Harness  
17. Dataset Citation  

---

## 1. Summary of Results

Five platforms were benchmarked using the same citation graph dataset containing **27,769 nodes** and **352,768 relationships**.

The measured categories were:

- data loading / ingest throughput
- 1-hop, 2-hop, and 3-hop traversal latency
- point lookup latency
- indexed/filtered lookup latency
- aggregation latency
- mixed read/write throughput at 1, 10, and 40 concurrent clients
- observable footprint details

### Key Observations

- **Memgraph** showed the strongest ingest throughput and strong cloud read latency.
- **Neo4j Aura Free** showed strong managed-cloud performance and scaled well under concurrency.
- **Docker Neo4j** showed the lowest local latency, but this includes a localhost advantage and is not directly comparable to cloud network latency.
- **CognoDB** completed all benchmark workloads with zero mixed-workload errors, but showed higher request latency under the tested free-tier conditions and client network path.
- **Neo4j Sandbox** behaved similarly to a managed trial environment with relatively high fixed request latency.

A major architectural observation from the benchmark was that Java-based Neo4j required more process-level memory headroom than its database heap alone. A strict 256 MB container limit caused Docker Neo4j to crash during startup, while CognoDB’s free tier operated within a 256 MB total resource limit.

---

## 2. Platforms Compared

| Platform | Engine / Product | Deployment Type | Role in Benchmark |
|---|---|---:|---|
| CognoDB | CognoDB Cloud | Managed cloud free tier | Primary benchmark subject |
| Neo4j Aura Free | Neo4j AuraDB | Managed cloud free tier | Managed incumbent baseline |
| Neo4j Sandbox | Neo4j | Temporary managed cloud instance | Managed trial baseline |
| Docker Neo4j | Neo4j 5 Community | Self-hosted Docker container, resource-capped | Local engine baseline |
| Memgraph | Memgraph | Managed cloud free/entry tier | In-memory architecture comparison |

### Platform Selection Rationale

The selected platforms cover several useful comparison dimensions:

- **CognoDB**: the target platform, running on its intentionally small free tier.
- **Neo4j Aura Free**: the managed cloud version of the most widely known property-graph database.
- **Neo4j Sandbox**: a temporary managed Neo4j environment, useful for comparing managed trial behavior.
- **Docker Neo4j**: a locally hosted, resource-capped Neo4j instance, useful for isolating engine behavior from cloud network latency.
- **Memgraph**: an in-memory graph database with Cypher/Bolt compatibility, useful for comparing a different storage architecture.

Aura, Sandbox, and Docker all use Neo4j-based engines, but they represent different deployment models:

- managed free-tier cloud
- temporary managed sandbox
- self-hosted capped deployment

This allows the benchmark to separate, to some extent, **engine behavior** from **hosting overhead**.

---

## 3. Environment and Instance Specifications

### Client Environment

All benchmark workloads were executed from the same client machine.

| Item | Value |
|---|---|
| Client machine | Linux laptop |
| Operating system | Ubuntu-based Linux |
| Benchmark language | C# |
| Runtime | .NET 8 |
| Graph driver | Official Neo4j .NET driver |
| Network | Residential internet connection |
| Docker host | Same local machine |

### Cloud Region Notes

All requests originated from the same client machine and client network.

Where the platform allowed region selection, the closest available region was chosen. Where exact region parity was not possible due to free-tier restrictions, this is recorded as a caveat.

### Advertised / Recorded Instance Specifications

| Platform | Tier | vCPU | RAM | Storage / Other Limits | Notes |
|---|---:|---:|---:|---:|---|
| CognoDB | Free `c0` | Burstable 0.5 vCPU | 256 MB | 1 GB disk | Official assignment target |
| Neo4j Aura Free | Free | Not publicly disclosed | Not publicly disclosed | Graph-size limit: approximately 200k nodes / 400k relationships | Managed Neo4j free tier |
| Neo4j Sandbox | Free temporary sandbox | Not publicly disclosed | Not publicly disclosed | Temporary evaluation instance | Internal specs not publicly guaranteed |
| Docker Neo4j | Self-hosted capped | 0.5 CPU | 512 MB container ceiling; 256 MB JVM heap | Local disk | JVM heap tuned to match CognoDB RAM |
| Memgraph | Free/entry cloud project | Provider-managed | Provider-managed entry resources | In-memory engine | Entry project resources fixed by provider plan |

### Docker Memory Configuration

The Docker Neo4j instance was limited to:

- 0.5 CPU
- 512 MB container memory ceiling
- 256 MB Neo4j JVM heap max
- 50 MB Neo4j page cache

This distinction is important:

| Layer | Limit |
|---|---:|
| Docker container ceiling | 512 MB |
| Neo4j database heap | 256 MB |
| Neo4j page cache | 50 MB |
| Remaining headroom | JVM / OS / off-heap overhead |

At a strict 256 MB container limit, Neo4j 5 Community repeatedly crashed during startup with out-of-memory behavior. Therefore, the container ceiling was raised to 512 MB, while the database heap itself remained limited to 256 MB.

This is documented as both a fairness caveat and an architectural observation.

---

## 4. Dataset

### Dataset Source

Dataset: **SNAP `cit-HepTh`**  
Full name: arXiv High Energy Physics Theory citation network  
Source: https://snap.stanford.edu/data/cit-HepTh.html

Original dataset description:

- 27,770 papers
- 352,807 citation edges
- papers from January 1993 to April 2003
- directed edge from paper `i` to paper `j` if paper `i` cites paper `j`

### Final Loaded Dataset

After cleaning self-loops and normalizing the data, the final graph loaded into every platform was:

| Metric | Value |
|---|---:|
| Nodes | 27,769 |
| Relationships | 352,768 |
| Node label | `Paper` |
| Relationship type | `CITES` |

The identical dataset was loaded into every platform.

### Node Properties

| Property | Type | Source |
|---|---|---|
| `id` | integer | paper ID |
| `year` | integer | real submission year from SNAP dates file |
| `field` | string | deterministic synthetic category for aggregation testing |

The `field` property was generated deterministically from the paper ID. This means every platform received exactly the same synthetic field values.

### Why This Dataset Was Chosen

The assignment recommends a graph with roughly **100k to 500k relationships**.

This dataset was chosen because:

- it contains approximately 353k relationships
- it fits within all tested free tiers
- it is public and well documented
- it has meaningful node properties for lookup and filtering
- it is large enough to show traversal and aggregation behavior
- it is small enough to load repeatedly on small free instances

---

## 5. Data Loading Method

The same dataset was loaded into every platform.

### Load Method by Platform

| Platform | Load Method |
|---|---|
| CognoDB | Official Neo4j .NET driver over Bolt |
| Neo4j Aura Free | Official Neo4j .NET driver over Bolt |
| Neo4j Sandbox | Official Neo4j .NET driver over Bolt |
| Docker Neo4j | Official Neo4j .NET driver over local Bolt |
| Memgraph | Neo4j-compatible Bolt driver using Memgraph Cypher dialect |

### Batched Loading Strategy

Data was loaded using Cypher `UNWIND` batching:

- node batch size: 2,000 nodes per request
- relationship batch size: 2,000 relationships per request

Batched loading was used to avoid one network round-trip per node or relationship.

### Node Load Query

The logical node load query was:

```cypher
UNWIND $rows AS r
CREATE (p:Paper {id: r.id, year: r.year, field: r.field});
```

### Relationship Load Query

The logical relationship load query was:

```cypher
UNWIND $rows AS r
MATCH (a:Paper {id: r.src})
MATCH (b:Paper {id: r.dst})
CREATE (a)-[:CITES]->(b);
```

---

## 6. Indexing Strategy

To make lookup and filtered lookup tests fair, equivalent indexes were created on all platforms.

### Indexed Properties

| Property | Purpose |
|---|---|
| `Paper.id` | point lookup |
| `Paper.year` | filtered/indexed lookup |

### Index Creation by Platform

| Platform | `Paper.id` Indexing | `Paper.year` Indexing | Notes |
|---|---|---|---|
| CognoDB | Unique constraint/index | Secondary index | `db.awaitIndexes` procedure unavailable; fixed wait used |
| Neo4j Aura Free | Unique constraint/index | Secondary index | Standard Neo4j index creation |
| Neo4j Sandbox | Unique constraint/index | Secondary index | Standard Neo4j index creation |
| Docker Neo4j | Unique constraint/index | Secondary index | Standard Neo4j index creation |
| Memgraph | Index | Index | Index creation required implicit/auto-commit transaction |

---

## 7. Benchmark Workloads

All platforms executed the same logical workloads. Small Cypher dialect differences were handled by the harness and documented in the caveats section.

### Point Lookup

Purpose: measure indexed lookup of one node by unique ID.

```cypher
MATCH (p:Paper {id: $id})
RETURN p.year AS year;
```

### Filtered / Indexed Lookup

Purpose: measure filtered lookup using the indexed `year` property.

```cypher
MATCH (p:Paper)
WHERE p.year >= $y1 AND p.year <= $y2
RETURN count(p) AS c;
```

Example parameters:

| Parameter | Value |
|---|---:|
| `y1` | 1998 |
| `y2` | 2001 |

### Traversal Workloads

Traversal start nodes were selected randomly using a fixed seed. This ensured that every platform was tested against the same set of start nodes.

One-hop traversal:

```cypher
MATCH (p:Paper {id: $id})-[:CITES]->(q)
RETURN count(q) AS c;
```

Two-hop traversal:

```cypher
MATCH (p:Paper {id: $id})-[:CITES]->()-[:CITES]->(q)
RETURN count(q) AS c;
```

Three-hop traversal:

```cypher
MATCH (p:Paper {id: $id})-[:CITES]->()-[:CITES]->()-[:CITES]->(q)
RETURN count(q) AS c;
```

### Aggregation Workload

Purpose: measure group-by aggregation over a node property.

```cypher
MATCH (p:Paper)
RETURN p.field AS field, count(*) AS c
ORDER BY c DESC;
```

### Mixed Read/Write Workload

The mixed workload used:

- 80% reads
- 20% writes
- concurrency levels: 1, 10, and 40 clients
- 30 seconds per concurrency level

Read operation:

```cypher
MATCH (p:Paper {id: $id})
RETURN p.year AS year;
```

Write operation:

```cypher
MATCH (p:Paper {id: $id})
SET p.seen = true;
```

---

## 8. Full Results Matrix

All reported latency numbers are in **milliseconds**.

Read workloads were measured after warm-up. Each measured read workload used **100 iterations**. Percentiles reported are **p50** and **p95**.

---

### 8.1 Data Loading / Ingest Throughput

| Platform | Nodes/sec | Relationships/sec | Total Load Time (s) |
|---|---:|---:|---:|
| cognodb | 1064 | 1209 | 317.9 |
| aura | 9920.9 | 7865.6 | 47.6 |
| docker | 2341.4 | 8144.5 | 55.2 |
| memgraph | 12882.3 | 14966.4 | 25.7 |
| sandbox | 1621.9 | 2339 | 167.9 |

#### Relationship Ingest Throughput Visual

```text
memgraph   14966 rels/sec  ████████████████████████████████
docker      8144 rels/sec  █████████████████
aura        7865 rels/sec  ████████████████
sandbox     2339 rels/sec  █████
cognodb     1209 rels/sec  ██
```

---

### 8.2 Traversal Latency

| Platform | 1-hop p50 | 1-hop p95 | 2-hop p50 | 2-hop p95 | 3-hop p50 | 3-hop p95 |
|---|---:|---:|---:|---:|---:|---:|
| cognodb | 644.157 | 651.676 | 652.323 | 660.809 | 654.205 | 738.483 |
| aura | 146.208 | 148.944 | 146.512 | 148.599 | 146.818 | 158.65 |
| docker | 3.085 | 80.673 | 3.167 | 80.582 | 3.271 | 82.082 |
| memgraph | 95.268 | 97.131 | 95.833 | 127.686 | 96.22 | 101.359 |
| sandbox | 697.842 | 713.301 | 697.673 | 712.924 | 698.012 | 720.312 |

#### 1-hop p50 Latency Visual

```text
docker       3.1 ms  █
memgraph    95.3 ms  ███
aura       146.2 ms  ████
cognodb    644.2 ms  ███████████████
sandbox    697.8 ms  ████████████████
```

Lower is better.

---

### 8.3 Lookup Latency

| Platform | Point p50 | Point p95 | Filtered p50 | Filtered p95 |
|---|---:|---:|---:|---:|
| cognodb | 643.942 | 647.03 | 671.096 | 679.942 |
| aura | 146.112 | 148.509 | 147.483 | 150.359 |
| docker | 3.346 | 82.04 | 4.156 | 79.694 |
| memgraph | 95.149 | 99.077 | 97.687 | 99.646 |
| sandbox | 698.836 | 711.07 | 700.404 | 945.051 |

#### Point Lookup p50 Latency Visual

```text
docker       3.3 ms  █
memgraph    95.1 ms  ███
aura       146.1 ms  ████
cognodb    643.9 ms  ███████████████
sandbox    698.8 ms  ████████████████
```

Lower is better.

---

### 8.4 Aggregation Latency

| Platform | Group-by p50 | Group-by p95 |
|---|---:|---:|
| cognodb | 683.653 | 691.267 |
| aura | 156.838 | 162.862 |
| docker | 13.859 | 87.791 |
| memgraph | 105.982 | 117.626 |
| sandbox | 704.6 | 723.185 |

#### Aggregation p50 Latency Visual

```text
docker      13.9 ms  █
memgraph   106.0 ms  ███
aura       156.8 ms  ████
cognodb    683.7 ms  ███████████████
sandbox    704.6 ms  ████████████████
```

Lower is better.

---

### 8.5 Mixed Read/Write Workload

Read/write mix: **80% reads / 20% writes**

Each concurrency level ran for **30 seconds**.

| Platform | 1 client QPS | 10 clients QPS | 40 clients QPS | 40-client errors |
|---|---:|---:|---:|---:|
| cognodb | 1.5 | 15.2 | 61.2 | 0 |
| aura | 6.6 | 72.8 | 287.7 | 0 |
| docker | 182.4 | 309.2 | 378.4 | 0 |
| memgraph | 10 | 88.3 | 374.4 | 5 |
| sandbox | 1.4 | 13.6 | 51.3 | 0 |

#### 40-client Mixed QPS Visual

```text
docker     378.4 QPS  ████████████████████████████████
memgraph   374.4 QPS  ███████████████████████████████
aura       287.7 QPS  ████████████████████████
cognodb     61.2 QPS  █████
sandbox     51.3 QPS  ████
```

Higher is better.

Memgraph produced **5 errors** during the 40-client mixed workload. These errors are reported honestly and are discussed in the caveats section.

---

### 8.6 Resource Footprint

| Platform | Node Count | Relationship Count | Observable Resource Notes |
|---|---:|---:|---|
| cognodb | 27769 | 352768 | Cloud memory/storage metrics not exposed by free tier |
| aura | 27769 | 352768 | Cloud memory/storage metrics not exposed by free tier |
| docker | 27769 | 352768 | Container capped at 0.5 CPU, 512 MB container memory, 256 MB JVM heap |
| memgraph | 27769 | 352768 | In-memory engine; detailed memory metrics not captured by harness |
| sandbox | 27769 | 352768 | Sandbox internals not publicly observable |

Where a platform did not expose memory usage, storage usage, or instance internals, the value is reported as **not observable**.

---

## 9. Analysis

### 9.1 Ingest Throughput

Memgraph produced the strongest ingest performance:

| Platform | Relationships/sec |
|---|---:|
| memgraph | 14,966 |
| docker | 8,144 |
| aura | 7,865 |
| sandbox | 2,339 |
| cognodb | 1,209 |

Memgraph’s in-memory architecture gives it a significant advantage for batched write workloads. Docker Neo4j also performed well because it avoided cloud network latency and wrote to a local engine. Aura showed strong managed-cloud ingest throughput.

CognoDB and Sandbox were slower in this benchmark. CognoDB’s free tier is intentionally small: burstable 0.5 vCPU, 256 MB RAM, and 1 GB disk. Ingest performance appears constrained by this small resource envelope, network round-trip overhead, and free-tier scheduling behavior.

### 9.2 Traversal Latency

Docker Neo4j produced the lowest p50 traversal latency:

| Platform | 1-hop p50 |
|---|---:|
| docker | 3.085 ms |
| memgraph | 95.268 ms |
| aura | 146.208 ms |
| cognodb | 644.157 ms |
| sandbox | 697.842 ms |

Docker’s low latency is expected because the client and database ran on the same machine. This removes internet round-trip latency.

Among cloud platforms, Memgraph was fastest, followed by Aura. CognoDB and Sandbox showed much higher latency in this test.

One important observation is that traversal latency barely increased as hop depth increased.

Example:

| Platform | 1-hop p50 | 3-hop p50 | Difference |
|---|---:|---:|---:|
| cognodb | 644.157 ms | 654.205 ms | about 10 ms |
| aura | 146.208 ms | 146.818 ms | less than 1 ms |
| memgraph | 95.268 ms | 96.22 ms | about 1 ms |
| sandbox | 697.842 ms | 698.012 ms | about 0.2 ms |

This suggests that, at this dataset size, the measured latency is dominated by fixed overhead rather than graph traversal cost.

Possible fixed overhead sources include:

- network round-trip time
- request serialization/deserialization
- query parsing and planning
- connection handling
- free-tier CPU scheduling
- shared-instance throttling

### 9.3 Lookup and Filtered Lookup Latency

Lookup latency followed the same broad pattern as traversal latency.

Docker Neo4j was fastest at p50, while Memgraph and Aura were the strongest cloud platforms.

CognoDB’s point lookup p50 was 643.942 ms, and its filtered lookup p50 was 671.096 ms. These values are close to its traversal latency, again suggesting that fixed request overhead dominated the measurement.

Sandbox’s filtered lookup p95 reached 945.051 ms, showing noticeable tail latency under filtered queries.

### 9.4 Aggregation Latency

Aggregation requires more work than point lookup because the database must scan and group matching records.

Docker Neo4j again had the lowest p50 latency:

| Platform | Aggregation p50 |
|---|---:|
| docker | 13.859 ms |
| memgraph | 105.982 ms |
| aura | 156.838 ms |
| cognodb | 683.653 ms |
| sandbox | 704.6 ms |

The relative ordering matches the lookup and traversal results. This consistency suggests that the benchmark is measuring stable platform behavior rather than random noise.

### 9.5 Mixed Workload and Concurrency

The mixed workload used 80% reads and 20% writes.

At 40 concurrent clients:

| Platform | QPS | Errors |
|---|---:|---:|
| docker | 378.4 | 0 |
| memgraph | 374.4 | 5 |
| aura | 287.7 | 0 |
| cognodb | 61.2 | 0 |
| sandbox | 51.3 | 0 |

Docker and Memgraph achieved the highest throughput. Aura also scaled well.

CognoDB and Sandbox showed lower absolute throughput, but both completed the mixed workload with zero recorded errors.

Memgraph produced five errors under the 40-client workload. This was retained in the results instead of being hidden. The likely explanation is resource pressure on the entry-tier instance under concurrent writes.

### 9.6 Cloud-Only Comparison

Docker should be interpreted carefully because it runs locally and avoids internet latency.

If we compare only cloud platforms:

#### Traversal p50

| Platform | 1-hop p50 |
|---|---:|
| memgraph | 95.268 ms |
| aura | 146.208 ms |
| cognodb | 644.157 ms |
| sandbox | 697.842 ms |

#### 40-client Mixed QPS

| Platform | QPS |
|---|---:|
| memgraph | 374.4 |
| aura | 287.7 |
| cognodb | 61.2 |
| sandbox | 51.3 |

Under the tested conditions, Memgraph and Aura showed the strongest cloud performance. CognoDB remained stable and error-free but showed higher latency and lower throughput from this client network and free-tier instance.

---

## 10. Methodology and Fairness

### Same Dataset

The exact same dataset was loaded into every platform.

### Same Logical Queries

All platforms ran the same logical workloads. Minor Cypher dialect differences were handled by the harness and documented in the caveats section.

### Same Client Machine

All benchmark commands were executed from the same laptop.

### Free / Entry Tiers Only

No paid production-tier instance was used.

### Warm-up

Each measured read workload performed warm-up iterations before measurement. Warm-up results were discarded.

All reported numbers are warm-run numbers.

### Iterations

Each measured read workload used **100 iterations**.

### Percentile Reporting

The benchmark reports:

- p50 latency
- p95 latency

Averages alone can hide tail latency. p95 gives a better view of the experience during slower requests.

### Concurrency Sweep

The mixed workload was executed using:

- 1 client
- 10 clients
- 40 clients

This satisfies the assignment’s suggestion to perform a concurrency sweep.

### Automation

The benchmark is automated end-to-end.

For each platform, one command:

1. prepares the dataset if needed
2. connects to the database
3. wipes previous benchmark data
4. creates indexes/constraints
5. loads the dataset
6. runs warm-up queries
7. runs measured workloads
8. runs mixed concurrency workloads
9. writes JSON results to the `results/` directory

A separate report command converts JSON results into Markdown tables.

---

## 11. Caveats and Limitations

This section records known caveats honestly.

### 11.1 Docker localhost advantage

Docker Neo4j ran on the same machine as the benchmark client. This removes internet latency.

Therefore, Docker results should be treated as a **local engine baseline**, not as a direct cloud-to-cloud comparison.

### 11.2 Docker memory ceiling vs database heap

The Docker container used a 512 MB memory ceiling, but the Neo4j JVM heap was limited to 256 MB.

This was necessary because Neo4j crashed at a strict 256 MB container limit.

This is both a caveat and an architectural finding: Java-based databases may require more total process headroom than their logical database heap.

### 11.3 Free-tier throttling

Free-tier instances may be shared, burstable, or throttled. Results may vary depending on time of day, region, and platform load.

### 11.4 Network variance

Cloud platforms were accessed over a residential internet connection. Network jitter and routing differences can affect latency.

### 11.5 Region parity

Exact cloud region parity was not always possible on free tiers. Where possible, closest/default regions were used. This is recorded as a caveat.

### 11.6 Not all specs are public

Neo4j Aura Free and Neo4j Sandbox do not publicly expose full vCPU/RAM details. Memgraph entry project resources depend on the provider’s current free/entry offering.

Where specs were not observable, this README says so instead of guessing.

### 11.7 Query dialect differences

All tested platforms accepted Cypher-style graph queries, but some dialect differences appeared:

- Memgraph required implicit/auto-commit transactions for index creation.
- CognoDB did not expose `db.awaitIndexes`.
- Multi-hop traversal queries required intermediate anonymous nodes.
- Some platforms handled constraint creation differently from index creation.

These differences were handled in the harness and preserved as honest caveats.

### 11.8 Warm numbers only

Cold-start numbers were not separately reported. All reported latency numbers are warm-run numbers after warm-up.

### 11.9 Single full measurement pass

Each read workload used 100 iterations. However, due to the 48-hour assignment window, the full benchmark suite was not repeated across multiple days.

Repeated full-suite runs would further strengthen variance analysis.

### 11.10 Memgraph mixed-workload errors

Memgraph produced five errors during the 40-client mixed workload. These errors are included in the results and were not hidden.

### 11.11 Engine diversity caveat

Aura, Sandbox, and Docker all use Neo4j-based engines. They were included to compare different deployment models: managed cloud, temporary sandbox, and self-hosted capped deployment.

Memgraph provides a different in-memory architecture. Additional distinct engines could be added in future work.

---

## 12. How to Reproduce the Benchmark

Anyone with free-tier accounts should be able to reproduce this benchmark from the README alone.

### Prerequisites

Required:

- .NET 8 SDK
- Git
- Free accounts for the tested platforms

Optional:

- Docker, only if rerunning the local Docker Neo4j benchmark

### Step 1: Clone the Repository

```bash
git clone https://github.com/kalbashi09/Benchmark.git
cd Benchmark
```

### Step 2: Create Environment File

```bash
cp .env.example .env
```

Fill in the credentials for each platform.

Do not commit the `.env` file.

### Step 3: Restore Dependencies

```bash
dotnet restore
```

### Step 4: Run Each Benchmark

```bash
dotnet run -- cognodb
dotnet run -- aura
dotnet run -- sandbox
dotnet run -- memgraph
dotnet run -- docker
```

Each command writes results to:

```text
results/<platform>.json
```

### Step 5: Regenerate the Results Report

```bash
dotnet run -- report
```

This generates or updates the results tables from the JSON files.

### Docker Neo4j Setup

To recreate the local capped Neo4j instance, run:

```bash
sudo docker rm -f neo4j-bench

sudo docker run -d --name neo4j-bench \
  -p 7687:7687 \
  -e NEO4J_AUTH=neo4j/benchmark123 \
  -e NEO4J_server_memory_heap_initial__size=256m \
  -e NEO4J_server_memory_heap_max__size=256m \
  -e NEO4J_server_memory_pagecache_size=50m \
  --cpus="0.5" \
  --memory="512m" \
  neo4j:5-community
```

Wait approximately 60 seconds for Neo4j to start, then run:

```bash
dotnet run -- docker
```

---

## 13. Repository Structure

```text
.
├── Program.cs              # Benchmark orchestrator
├── Dataset.cs              # Downloads and prepares the SNAP cit-HepTh dataset
├── BoltAdapter.cs          # Adapter for Bolt/Cypher-compatible platforms
├── Bench.cs                # Timing, percentile, and mixed workload helpers
├── ReportGenerator.cs      # Generates Markdown tables from JSON results
├── Benchmark.csproj        # Project file with pinned dependencies
├── results/                # JSON benchmark outputs
├── .env.example            # Environment variable template
├── .gitignore              # Prevents secrets and build artifacts from being committed
├── RESULTS.md              # Automated Benchmark Result
└── README.md               # This document
```

---

## 14. Dependencies

Dependencies are pinned in `Benchmark.csproj`.

Primary dependencies:

| Dependency | Purpose |
|---|---|
| Neo4j.Driver | Official .NET driver for Bolt/Cypher-compatible graph databases |
| DotNetEnv | Loads local environment variables from `.env` |

To verify installed dependency versions:

```bash
dotnet list package
```

---

## 15. Security and Secrets

No passwords or connection URIs are committed to the repository.

All secrets are loaded from environment variables.

The local `.env` file is ignored by Git.

Only `.env.example` is committed. It contains variable names but no secrets.

---

## 16. Extending the Benchmark Harness

The harness is designed to be extended.

To add another platform:

1. Add environment variables for the new platform.
2. Add the platform to the orchestrator switch statement in `Program.cs`.
3. Reuse `BoltAdapter` if the platform supports Bolt/Cypher.
4. If the platform uses a different wire protocol or query language, create a new adapter implementing the same benchmark operations.
5. Run the benchmark with `dotnet run -- <platform>`.
6. Regenerate the report with `dotnet run -- report`.

Future extensions could include:

- FalkorDB
- ArangoDB
- NebulaGraph
- TigerGraph
- Kùzu
- cold-start benchmarking
- repeated full-suite runs
- charts generated from JSON results
- cloud-provider metric collection where exposed

---

## 17. Dataset Citation

J. Leskovec, J. Kleinberg and C. Faloutsos.  
**Graphs over Time: Densification Laws, Shrinking Diameters and Possible Explanations.**  
ACM SIGKDD International Conference on Knowledge Discovery and Data Mining, KDD 2005.

J. Gehrke, P. Ginsparg, J. M. Kleinberg.  
**Overview of the 2003 KDD Cup.**  
SIGKDD Explorations 5(2): 149-151, 2003.

Dataset source:

https://snap.stanford.edu/data/cit-HepTh.html