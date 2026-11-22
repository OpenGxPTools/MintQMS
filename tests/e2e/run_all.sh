#!/bin/bash
# Run every e2e suite; exit non-zero if any fails. Requires E2E_DB (see README.md).
cd "$(dirname "$0")"
rc=0
for t in test_masterdata_edit test_xss_supplier_items test_low_priority test_sitetime test_file_integrity test_release_prep test_pv_workflow test_settings_ap_maint test_detail_reports test_exports test_issued_documents test_record_language test_workflows_language test_regional_neutral test_language test_workflow_baseline test_four_eyes test_maintenance_reopen test_assignment test_assignment_planning test_work_lists test_email test_libraries test_module_walks crawl; do
    out=$(python3 "$t.py" 2>&1); r=$?
    echo "$t: $(echo "$out" | tail -1)"
    [ $r -ne 0 ] && { echo "$out" | grep '^FAIL'; rc=1; }
done
exit $rc
