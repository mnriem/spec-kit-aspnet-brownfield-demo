#!/usr/bin/env bash
# test-api.sh — Manual smoke tests for the CarrotCakeCMS Headless REST API
#
# Usage:
#   BASE_URL=http://localhost:5000 \
#   CLIENT_ID=dev-client \
#   CLIENT_SECRET=my-super-secret \
#   SITE_ID=00000000-0000-0000-0000-000000000000 \
#     ./test-api.sh
#
# Dependencies: curl, jq
# All tests print PASS / FAIL with a brief description.

set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5000}"
CLIENT_ID="${CLIENT_ID:-dev-client}"
CLIENT_SECRET="${CLIENT_SECRET:-my-super-secret}"
SITE_ID="${SITE_ID:-}"
TEST_SLUG="${TEST_SLUG:-/about-us}"
TEST_POST_SLUG="${TEST_POST_SLUG:-/blog/2025/01/hello-world}"
TEST_CATEGORY="${TEST_CATEGORY:-news}"
TEST_TAG="${TEST_TAG:-featured}"
TEST_SNIPPET="${TEST_SNIPPET:-promo-banner}"
TEST_ZONE_PAGE="${TEST_ZONE_PAGE:-/home}"
TEST_ZONE_NAME="${TEST_ZONE_NAME:-sidebar}"

PASS=0
FAIL=0

# ---- helpers ----------------------------------------------------------------

check() {
  local desc="$1"
  local expected_status="$2"
  local actual_status="$3"
  if [[ "$actual_status" == "$expected_status" ]]; then
    echo "  PASS  [$actual_status] $desc"
    PASS=$((PASS + 1))
  else
    echo "  FAIL  [got $actual_status, want $expected_status] $desc"
    FAIL=$((FAIL + 1))
  fi
}

site_param() {
  if [[ -n "$SITE_ID" ]]; then
    echo "siteId=${SITE_ID}"
  else
    echo ""
  fi
}

qs() {
  # Build query string from optional args
  local parts=()
  local sp
  sp=$(site_param)
  [[ -n "$sp" ]] && parts+=("$sp")
  for arg in "$@"; do
    [[ -n "$arg" ]] && parts+=("$arg")
  done
  if [[ ${#parts[@]} -gt 0 ]]; then
    local IFS='&'
    echo "?${parts[*]}"
  fi
}

# ---- 0. Health check --------------------------------------------------------

echo ""
echo "=== 0. Health Check ==="
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "${BASE_URL}/health")
check "Health endpoint returns 200" "200" "$STATUS"

# ---- 1. Token Endpoint -------------------------------------------------------

echo ""
echo "=== 1. Token Endpoint ==="

# 1a. Valid credentials
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/headless/token" \
  -H "Content-Type: application/json" \
  -d "{\"clientId\":\"${CLIENT_ID}\",\"clientSecret\":\"${CLIENT_SECRET}\"}")
BODY=$(echo "$RESPONSE" | sed '$d')
STATUS=$(echo "$RESPONSE" | tail -n 1)
check "Valid credentials return 200" "200" "$STATUS"

TOKEN=$(echo "$BODY" | jq -r '.data.token // empty' 2>/dev/null)
if [[ -z "$TOKEN" ]]; then
  echo "  FAIL  [no token in response] Could not extract token — remaining tests will fail"
  FAIL=$((FAIL+1))
  exit 1
fi
echo "       Token obtained: ${TOKEN:0:40}..."

# 1b. Invalid credentials → 401
STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST "${BASE_URL}/api/headless/token" \
  -H "Content-Type: application/json" \
  -d "{\"clientId\":\"${CLIENT_ID}\",\"clientSecret\":\"WRONG_SECRET\"}")
check "Invalid credentials return 401" "401" "$STATUS"

# 1c. Missing clientId → 400
STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST "${BASE_URL}/api/headless/token" \
  -H "Content-Type: application/json" \
  -d "{\"clientSecret\":\"${CLIENT_SECRET}\"}")
check "Missing clientId returns 400" "400" "$STATUS"

# ---- 2. Pages ----------------------------------------------------------------

echo ""
echo "=== 2. Pages ==="

# 2a. List pages
QS=$(qs)
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/pages${QS}")
check "List pages returns 200" "200" "$STATUS"

# 2b. List pages — pagination
QS=$(qs "page=1" "pageSize=5")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/pages${QS}")
check "List pages page=1 pageSize=5 returns 200" "200" "$STATUS"

# 2c. Get page by slug (published)
SLUG_ENC=$(python3 -c "import urllib.parse; print(urllib.parse.quote('${TEST_SLUG}'))" 2>/dev/null || echo "${TEST_SLUG}")
QS=$(qs "slug=${SLUG_ENC}")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/pages${QS}")
check "Get page by slug '${TEST_SLUG}' returns 200" "200" "$STATUS"

# 2d. Non-existent slug → 404
QS=$(qs "slug=%2Fthis-slug-does-not-exist-xyz")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/pages${QS}")
check "Non-existent slug returns 404" "404" "$STATUS"

# 2e. No token → 401
QS=$(qs "slug=%2Fabout-us")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  "${BASE_URL}/api/headless/pages${QS}")
check "Request without token returns 401" "401" "$STATUS"

# ---- 3. Posts ----------------------------------------------------------------

echo ""
echo "=== 3. Posts ==="

# 3a. List posts — no filters
QS=$(qs)
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/posts${QS}")
check "List all posts returns 200" "200" "$STATUS"

# 3b. Filter by category
QS=$(qs "category=${TEST_CATEGORY}")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/posts${QS}")
check "List posts by category '${TEST_CATEGORY}' returns 200" "200" "$STATUS"

# 3c. Filter by tag
QS=$(qs "tag=${TEST_TAG}")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/posts${QS}")
check "List posts by tag '${TEST_TAG}' returns 200" "200" "$STATUS"

# 3d. Filter by date range
QS=$(qs "dateFrom=2025-01-01" "dateTo=2025-12-31")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/posts${QS}")
check "List posts by date range returns 200" "200" "$STATUS"

# 3e. Combined filters (AND logic)
QS=$(qs "category=${TEST_CATEGORY}" "tag=${TEST_TAG}" "dateFrom=2025-01-01" "dateTo=2025-12-31")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/posts${QS}")
check "Combined category+tag+date filter returns 200" "200" "$STATUS"

# 3f. Out-of-range page → 200 with empty data
QS=$(qs "page=9999")
RESPONSE=$(curl -s -w "\n%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/posts${QS}")
BODY=$(echo "$RESPONSE" | sed '$d')
STATUS=$(echo "$RESPONSE" | tail -n 1)
check "Out-of-range page returns 200" "200" "$STATUS"
ITEM_COUNT=$(echo "$BODY" | jq '.data | length' 2>/dev/null || echo "?")
echo "       Items in response: ${ITEM_COUNT} (expect 0)"

# 3g. Invalid dateFrom format → 400
QS=$(qs "dateFrom=not-a-date")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/posts${QS}")
check "Invalid dateFrom format returns 400" "400" "$STATUS"

# 3h. Get single post by slug
SLUG_ENC=$(python3 -c "import urllib.parse; print(urllib.parse.quote('${TEST_POST_SLUG}'))" 2>/dev/null || echo "${TEST_POST_SLUG}")
QS=$(qs "slug=${SLUG_ENC}")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/posts${QS}")
check "Get post by slug '${TEST_POST_SLUG}' returns 200 or 404" "200" "$STATUS" 2>/dev/null || \
check "Get post by slug '${TEST_POST_SLUG}' (no post in DB)" "404" "$STATUS"

# ---- 4. Navigation -----------------------------------------------------------

echo ""
echo "=== 4. Navigation ==="

# 4a. Navigation tree
QS=$(qs)
RESPONSE=$(curl -s -w "\n%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/navigation${QS}")
BODY=$(echo "$RESPONSE" | sed '$d')
STATUS=$(echo "$RESPONSE" | tail -n 1)
check "Navigation tree returns 200" "200" "$STATUS"
NODE_COUNT=$(echo "$BODY" | jq '.data | length' 2>/dev/null || echo "?")
echo "       Top-level nodes: ${NODE_COUNT}"

# 4b. Non-existent siteId → 404
QS="?siteId=ffffffff-ffff-ffff-ffff-ffffffffffff"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/navigation${QS}")
check "Non-existent siteId returns 404" "404" "$STATUS"

# ---- 5. Snippets -------------------------------------------------------------

echo ""
echo "=== 5. Snippets ==="

# 5a. Get snippet by name
QS=$(qs "name=${TEST_SNIPPET}")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/snippets${QS}")
check "Get snippet '${TEST_SNIPPET}' returns 200 or 404" "200" "$STATUS" 2>/dev/null || \
check "Get snippet '${TEST_SNIPPET}' (no snippet in DB)" "404" "$STATUS"

# 5b. Non-existent snippet → 404
QS=$(qs "name=does-not-exist-xyz")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/snippets${QS}")
check "Non-existent snippet returns 404" "404" "$STATUS"

# 5c. Missing name parameter → 400
QS=$(qs)
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/snippets${QS}")
check "Missing snippet name returns 400" "400" "$STATUS"

# ---- 6. Widget Zones ---------------------------------------------------------

echo ""
echo "=== 6. Widget Zones ==="

# 6a. Get widget zone
PAGE_ENC=$(python3 -c "import urllib.parse; print(urllib.parse.quote('${TEST_ZONE_PAGE}'))" 2>/dev/null || echo "${TEST_ZONE_PAGE}")
QS=$(qs "pageSlug=${PAGE_ENC}" "zone=${TEST_ZONE_NAME}")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/widgetzones${QS}")
check "Get widget zone '${TEST_ZONE_NAME}' on '${TEST_ZONE_PAGE}' returns 200 or 404" "200" "$STATUS" 2>/dev/null || \
check "Get widget zone (no widgets in DB)" "404" "$STATUS"

# 6b. Non-existent page → 404
QS=$(qs "pageSlug=%2Fnonexistent-xyz" "zone=sidebar")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/widgetzones${QS}")
check "Non-existent page slug returns 404" "404" "$STATUS"

# 6c. Missing zone parameter → 400
QS=$(qs "pageSlug=${PAGE_ENC}")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${TOKEN}" \
  "${BASE_URL}/api/headless/widgetzones${QS}")
check "Missing zone parameter returns 400" "400" "$STATUS"

# ---- Summary ----------------------------------------------------------------

echo ""
echo "==================================================="
echo "  Results: ${PASS} passed, ${FAIL} failed"
echo "==================================================="
[[ $FAIL -eq 0 ]] && exit 0 || exit 1
