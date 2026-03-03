#!/bin/bash

BASE="http://localhost:5086"

echo "================================================"
echo " HybridCache Test Suite"
echo " Priority: L1 (memory) → L2 (distributed) → Factory (repo)"
echo "================================================"

echo ""
echo "--- TEST 1: Cache MISS (hits factory/repo) ---"
echo "Expect: ~80ms (50ms repo delay + overhead)"
curl -s -o /dev/null -w "Time: %{time_total}s | Status: %{http_code}\n" $BASE/products

echo ""
echo "--- TEST 2: L1 Cache HIT (in-process memory) ---"
echo "Expect: <5ms (served from L1, no repo call)"
curl -s -o /dev/null -w "Time: %{time_total}s | Status: %{http_code}\n" $BASE/products

echo ""
echo "--- TEST 3: Per-item cache MISS then HIT ---"
curl -s -o /dev/null -w "Product 1 miss:  %{time_total}s\n" $BASE/products/1
curl -s -o /dev/null -w "Product 1 hit:   %{time_total}s\n" $BASE/products/1

echo ""
echo "--- TEST 4: Cache invalidation on CREATE ---"
echo "Creating new product..."
curl -s -X POST $BASE/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Monitor","category":"Electronics","price":399.99}' | python3 -m json.tool
echo ""
echo "GET /products after create (cache invalidated — expect slow again):"
curl -s -o /dev/null -w "Time: %{time_total}s\n" $BASE/products

echo ""
echo "--- TEST 5: Cache invalidation on UPDATE ---"
curl -s -X PUT $BASE/products/1 \
  -H "Content-Type: application/json" \
  -d '{"name":"Gaming Laptop","category":"Electronics","price":1499.99}' | python3 -m json.tool
echo ""
echo "GET /products/1 after update (cache invalidated — expect slow):"
curl -s -o /dev/null -w "Time: %{time_total}s\n" $BASE/products/1
echo "GET /products/1 again (re-cached — expect fast):"
curl -s -o /dev/null -w "Time: %{time_total}s\n" $BASE/products/1

echo ""
echo "--- TEST 6: DELETE + 404 ---"
curl -s -X DELETE $BASE/products/2 -w "Delete status: %{http_code}\n"
curl -s -o /dev/null -w "GET deleted item: %{http_code}\n" $BASE/products/2

echo ""
echo "================================================"
echo " Done"
echo "================================================"
