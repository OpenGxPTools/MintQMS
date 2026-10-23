#!/usr/bin/env bash
# -----------------------------------------------------------------------------
# backup_mintqms.sh — MintQMS backup script (SQLite database + uploaded files)
#
# Usage:
#   ./backup_mintqms.sh
#
# Configuration (edit the variables below or pass as environment variables):
#   DB_SOURCE       — path to the live OpenQMS.db file
#   UPLOADS_SOURCE  — path to the uploaded-files store (wwwroot/uploads): record
#                     attachments, controlled-document files and the logo
#   BACKUP_DIR      — directory where backup copies are stored
#   STATUS_FILE     — path where the JSON status file is written (read by the app)
#   RETAIN_COPIES   — how many recent backups to keep (older ones are deleted)
#
# Each run produces a matching pair with the same timestamp:
#   OpenQMS_<ts>.db               — consistent copy of the database
#   OpenQMS_uploads_<ts>.tar.gz   — archive of the uploaded-files store
# The database is copied first: uploaded files are never removed by MintQMS (deletion
# is logical), so the archive taken just after always contains every file the
# database copy refers to. A run succeeds only if BOTH parts succeed.
#
# Cron example (run daily at 02:00):
#   0 2 * * * /opt/mintqms/Scripts/backup_mintqms.sh >> /var/log/mintqms_backup.log 2>&1
# -----------------------------------------------------------------------------

set -euo pipefail

# ── Configuration ─────────────────────────────────────────────────────────────
DB_SOURCE="${DB_SOURCE:-/opt/mintqms/OpenQMS.db}"
UPLOADS_SOURCE="${UPLOADS_SOURCE:-/opt/mintqms/wwwroot/uploads}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/mintqms}"
STATUS_FILE="${STATUS_FILE:-/var/backups/mintqms/backup_status.json}"
RETAIN_COPIES="${RETAIN_COPIES:-7}"

# ── Helpers ───────────────────────────────────────────────────────────────────
TIMESTAMP=$(date -u +"%Y%m%dT%H%M%SZ")
BACKUP_FILENAME="OpenQMS_${TIMESTAMP}.db"
BACKUP_PATH="${BACKUP_DIR}/${BACKUP_FILENAME}"
UPLOADS_FILENAME="OpenQMS_uploads_${TIMESTAMP}.tar.gz"
UPLOADS_PATH="${BACKUP_DIR}/${UPLOADS_FILENAME}"

write_status() {
    local status="$1"
    local filename="$2"
    local size_bytes="$3"
    local error_msg="$4"
    local uploads_filename="${5:-}"
    local uploads_size="${6:-0}"
    local ts
    ts=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

    mkdir -p "$(dirname "$STATUS_FILE")"

    # Escape any double-quotes in the error message
    error_msg_escaped="${error_msg//\"/\\\"}"

    cat > "$STATUS_FILE" <<EOF
{
  "status": "${status}",
  "timestampUtc": "${ts}",
  "fileName": "${filename}",
  "fileSizeBytes": ${size_bytes},
  "uploadsFileName": "${uploads_filename}",
  "uploadsFileSizeBytes": ${uploads_size},
  "errorMessage": "${error_msg_escaped}"
}
EOF
}

fail() {
    local msg="$1"
    echo "[ERROR] ${msg}" >&2
    write_status "failure" "" "0" "${msg}"
    exit 1
}

file_size() {
    stat -c%s "$1" 2>/dev/null || stat -f%z "$1"
}

# ── Pre-flight checks ─────────────────────────────────────────────────────────
if [[ ! -f "$DB_SOURCE" ]]; then
    fail "Database file not found: ${DB_SOURCE}"
fi
if [[ ! -d "$UPLOADS_SOURCE" ]]; then
    fail "Uploaded-files directory not found: ${UPLOADS_SOURCE}"
fi

mkdir -p "$BACKUP_DIR" || fail "Cannot create backup directory: ${BACKUP_DIR}"

# ── 1. Database — SQLite's online backup (safe with live DB) ──────────────────
echo "[INFO]  Starting backup at ${TIMESTAMP}"
echo "[INFO]  Database source : ${DB_SOURCE}"
echo "[INFO]  Database target : ${BACKUP_PATH}"

if command -v sqlite3 &>/dev/null; then
    # Preferred: sqlite3 .backup guarantees a consistent copy even if DB is open
    sqlite3 "$DB_SOURCE" ".backup '${BACKUP_PATH}'" \
        || fail "sqlite3 .backup command failed"
else
    # Fallback: cp (acceptable when app is stopped, or DB is small and low-write)
    echo "[WARN]  sqlite3 not found — falling back to cp (ensure the app is idle)"
    cp "$DB_SOURCE" "$BACKUP_PATH" \
        || fail "cp fallback failed"
fi

if [[ ! -s "$BACKUP_PATH" ]]; then
    fail "Backup file is missing or empty after copy: ${BACKUP_PATH}"
fi
FILE_SIZE=$(file_size "$BACKUP_PATH")
echo "[INFO]  Database backup complete. Size: ${FILE_SIZE} bytes"

# ── 2. Uploaded files — compressed archive, verified by reading it back ───────
echo "[INFO]  Uploads source  : ${UPLOADS_SOURCE}"
echo "[INFO]  Uploads target  : ${UPLOADS_PATH}"

tar -czf "$UPLOADS_PATH" -C "$(dirname "$UPLOADS_SOURCE")" "$(basename "$UPLOADS_SOURCE")" \
    || fail "Archiving uploaded files failed: ${UPLOADS_SOURCE}"
tar -tzf "$UPLOADS_PATH" > /dev/null \
    || fail "Uploaded-files archive failed verification: ${UPLOADS_PATH}"

UPLOADS_SIZE=$(file_size "$UPLOADS_PATH")
UPLOADS_COUNT=$(find "$UPLOADS_SOURCE" -type f | wc -l)
echo "[INFO]  Uploads archive complete. ${UPLOADS_COUNT} files, ${UPLOADS_SIZE} bytes"

# ── Write success status ───────────────────────────────────────────────────────
write_status "success" "$BACKUP_FILENAME" "$FILE_SIZE" "" "$UPLOADS_FILENAME" "$UPLOADS_SIZE"
echo "[INFO]  Status file written: ${STATUS_FILE}"

# ── Prune old backups (keep the N most recent of each kind) ───────────────────
echo "[INFO]  Pruning old backups (keeping last ${RETAIN_COPIES})..."
for pattern in "OpenQMS_*.db" "OpenQMS_uploads_*.tar.gz"; do
    # shellcheck disable=SC2086
    ls -1t "${BACKUP_DIR}"/$pattern 2>/dev/null \
        | tail -n +"$((RETAIN_COPIES + 1))" \
        | xargs -r rm --
done
echo "[INFO]  Done."
