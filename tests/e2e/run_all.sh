#!/bin/bash
# Run every e2e suite; exit non-zero if any fails. Requires E2E_DB (see README.md).
cd "$(dirname "$0")"
rc=0
for t in test_masterdata_edit test_xss_supplier_items test_low_priority test_sitetime test_file_integrity test_release_prep test_detail_reports test_exports crawl; do
    out=$(python3 "$t.py" 2>&1); r=$?
    echo "$t: $(echo "$out" | tail -1)"
    [ $r -ne 0 ] && { echo "$out" | grep '^FAIL'; rc=1; }
done
exit $rc
