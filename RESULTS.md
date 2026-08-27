# 📊 Benchmark Results Matrix

_Auto-generated on 2026-08-27 02:53 UTC from 5 platform result files._

## 1. Data Loading (Ingest Throughput)

| Platform | Nodes/sec | Relationships/sec | Total Load Time (s) |
|---|---:|---:|---:|
| cognodb | 1064 | 1209 | 317.9 |
| aura | 9920.9 | 7865.6 | 47.6 |
| docker | 2341.4 | 8144.5 | 55.2 |
| memgraph | 12882.3 | 14966.4 | 25.7 |
| sandbox | 1621.9 | 2339 | 167.9 |

## 2. Traversal Latency (ms)

| Platform | 1-hop p50 | 1-hop p95 | 2-hop p50 | 2-hop p95 | 3-hop p50 | 3-hop p95 |
|---|---:|---:|---:|---:|---:|---:|
| cognodb | 644.157 | 651.676 | 652.323 | 660.809 | 654.205 | 738.483 |
| aura | 146.208 | 148.944 | 146.512 | 148.599 | 146.818 | 158.65 |
| docker | 3.085 | 80.673 | 3.167 | 80.582 | 3.271 | 82.082 |
| memgraph | 95.268 | 97.131 | 95.833 | 127.686 | 96.22 | 101.359 |
| sandbox | 697.842 | 713.301 | 697.673 | 712.924 | 698.012 | 720.312 |

## 3. Lookup Latency (ms)

| Platform | Point p50 | Point p95 | Filtered p50 | Filtered p95 |
|---|---:|---:|---:|---:|
| cognodb | 643.942 | 647.03 | 671.096 | 679.942 |
| aura | 146.112 | 148.509 | 147.483 | 150.359 |
| docker | 3.346 | 82.04 | 4.156 | 79.694 |
| memgraph | 95.149 | 99.077 | 97.687 | 99.646 |
| sandbox | 698.836 | 711.07 | 700.404 | 945.051 |

## 4. Aggregation Latency (ms)

| Platform | Group-by p50 | Group-by p95 |
|---|---:|---:|
| cognodb | 683.653 | 691.267 |
| aura | 156.838 | 162.862 |
| docker | 13.859 | 87.791 |
| memgraph | 105.982 | 117.626 |
| sandbox | 704.6 | 723.185 |

## 5. Mixed Read/Write Workload (Concurrency Sweep)

| Platform | 1 client QPS | 10 clients QPS | 40 clients QPS | 40-client errors |
|---|---:|---:|---:|---:|
| cognodb | 1.5 | 15.2 | 61.2 | 0 |
| aura | 6.6 | 72.8 | 287.7 | 0 |
| docker | 182.4 | 309.2 | 378.4 | 0 |
| memgraph | 10 | 88.3 | 374.4 | 5 |
| sandbox | 1.4 | 13.6 | 51.3 | 0 |

_Read/write mix: 80% reads / 20% writes. QPS = total completed operations per second._

## 6. Resource Footprint

| Platform | Node Count | Relationship Count | Notes |
|---|---:|---:|---|
| cognodb | 27769 | 352768 | — |
| aura | 27769 | 352768 | — |
| docker | 27769 | 352768 | — |
| memgraph | 27769 | 352768 | — |
| sandbox | 27769 | 352768 | — |

