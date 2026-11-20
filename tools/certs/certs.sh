#!/bin/sh
# Private CA and certificate generation for the MQTT broker, the backend and devices.
# Needs only POSIX sh and openssl. Relative paths are resolved from the repo root.
#
# Usage:
#   certs.sh ca      [--ca-dir DIR] [--days N] [--force]
#   certs.sh server  [--ca-dir DIR] [--out DIR] [--san LIST] [--days N] [--force]
#   certs.sh client  [--ca-dir DIR] [--out DIR] --cn NAME [--days N] [--force]
#   certs.sh devices [--ca-dir DIR] [--out DIR] --count N [--prefix P] [--start N] [--days N] [--force]

set -eu

DEFAULT_CA_DIR=infra/pki/ca
DEFAULT_SERVER_OUT=infra/compose/certs
DEFAULT_DEVICES_OUT=infra/pki/devices
DEFAULT_SAN="DNS:localhost,DNS:mosquitto,IP:127.0.0.1"

die() {
    echo "error: $*" >&2
    exit 1
}

usage() {
    sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'
}

# Resolve relative paths from the repo root, wherever the script is called from.
cd "$(dirname "$0")/../.."

command -v openssl >/dev/null 2>&1 || die "openssl not found"

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT INT TERM

# --- validation helpers ---

valid_cn() {
    case "$1" in
        *'
'*) return 1 ;;
    esac
    printf '%s' "$1" | LC_ALL=C grep -Eq '^[A-Za-z0-9_-]{3,64}$'
}

valid_int() {
    case "$1" in
        '' | *[!0-9]*) return 1 ;;
    esac
}

valid_san() {
    case "$1" in
        *'
'*) return 1 ;;
    esac
    printf '%s' "$1" | LC_ALL=C grep -Eq '^(DNS|IP):[A-Za-z0-9.:*_-]+(,(DNS|IP):[A-Za-z0-9.:*_-]+)*$'
}

# 128-bit positive serial, hex.
random_serial() {
    echo "0x1$(openssl rand -hex 15)"
}

# --- file helpers ---

# Returns 1 (and says so) when a file exists and --force is not set.
check_free() {
    for f in "$@"; do
        if [ -e "$f" ] && [ "$FORCE" != 1 ]; then
            echo "skip: $f exists (use --force to overwrite)"
            return 1
        fi
    done
    return 0
}

need_ca() {
    [ -f "$CA_DIR/ca.key" ] && [ -f "$CA_DIR/ca.crt" ] ||
        die "CA not found in $CA_DIR (run: certs.sh ca --ca-dir $CA_DIR)"
}

# Copy the CA certificate next to the leaf files. Never replaces a different file without --force.
copy_ca_crt() {
    dest="$1/ca.crt"
    if [ -e "$dest" ] && [ "$FORCE" != 1 ] && ! cmp -s "$CA_DIR/ca.crt" "$dest"; then
        echo "skip: $dest exists and differs (use --force to overwrite)"
        return 0
    fi
    cp "$CA_DIR/ca.crt" "$dest"
}

new_key() {
    (
        umask 077
        openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:prime256v1 -out "$1"
    )
}

# gen_leaf CN OUT_DIR EKU SAN_OR_EMPTY [FILE_BASE]  (FILE_BASE defaults to CN)
gen_leaf() {
    cn="$1"
    out="$2"
    eku="$3"
    san="$4"
    base="${5:-$cn}"
    key="$out/$base.key"
    crt="$out/$base.crt"

    check_free "$key" "$crt" || return 0

    mkdir -p "$out"
    new_key "$TMP/leaf.key"
    openssl req -new -key "$TMP/leaf.key" -subj "/CN=$cn" -out "$TMP/leaf.csr"

    {
        echo "basicConstraints=critical,CA:FALSE"
        echo "keyUsage=critical,digitalSignature"
        echo "extendedKeyUsage=$eku"
        echo "subjectKeyIdentifier=hash"
        echo "authorityKeyIdentifier=keyid"
        [ -z "$san" ] || echo "subjectAltName=$san"
    } >"$TMP/leaf.ext"

    openssl x509 -req -in "$TMP/leaf.csr" -CA "$CA_DIR/ca.crt" -CAkey "$CA_DIR/ca.key" \
        -set_serial "$(random_serial)" -days "$DAYS" -sha256 -extfile "$TMP/leaf.ext" \
        -out "$TMP/leaf.crt" 2>/dev/null

    cp "$TMP/leaf.key" "$key"
    chmod 600 "$key"
    cp "$TMP/leaf.crt" "$crt"
    echo "created: $crt"
}

# --- subcommands ---

cmd_ca() {
    check_free "$CA_DIR/ca.key" "$CA_DIR/ca.crt" || return 0
    mkdir -p "$CA_DIR"
    new_key "$TMP/ca.key"
    openssl req -new -x509 -key "$TMP/ca.key" -subj "/CN=Laundry IoT Private CA" \
        -days "$DAYS" -sha256 -set_serial "$(random_serial)" \
        -addext "basicConstraints=critical,CA:TRUE,pathlen:0" \
        -addext "keyUsage=critical,keyCertSign,cRLSign" \
        -addext "subjectKeyIdentifier=hash" \
        -out "$TMP/ca.crt"
    cp "$TMP/ca.key" "$CA_DIR/ca.key"
    chmod 600 "$CA_DIR/ca.key"
    cp "$TMP/ca.crt" "$CA_DIR/ca.crt"
    echo "created: $CA_DIR/ca.crt"
}

cmd_server() {
    valid_san "$SAN" || die "invalid --san (expected DNS:name or IP:addr entries separated by commas)"
    need_ca
    gen_leaf broker "$OUT" serverAuth "$SAN" server
    mkdir -p "$OUT"
    copy_ca_crt "$OUT"
}

cmd_client() {
    [ -n "$CN" ] || die "--cn is required"
    valid_cn "$CN" || die "invalid CN '$CN' (allowed: [A-Za-z0-9_-], 3 to 64 characters)"
    need_ca
    gen_leaf "$CN" "$OUT" clientAuth ""
}

cmd_devices() {
    [ -n "$COUNT" ] || die "--count is required"
    valid_int "$COUNT" && [ "$COUNT" -ge 1 ] || die "--count must be a positive integer"
    valid_int "$START" && [ "$START" -ge 1 ] || die "--start must be a positive integer"
    last=$((START + COUNT - 1))
    [ "$last" -le 9999 ] || die "device numbers must stay within 0001..9999 (last would be $last)"
    need_ca
    mkdir -p "$OUT"

    i=$START
    while [ "$i" -le "$last" ]; do
        cn=$(printf '%s-%04d' "$PREFIX" "$i")
        valid_cn "$cn" || die "invalid device id '$cn'"
        [ "$cn" != backend ] || die "the CN 'backend' is reserved"
        gen_leaf "$cn" "$OUT" clientAuth ""
        i=$((i + 1))
    done
    copy_ca_crt "$OUT"
}

# --- argument parsing ---

[ $# -ge 1 ] || {
    usage
    exit 1
}
SUB="$1"
shift

CA_DIR=$DEFAULT_CA_DIR
OUT=""
SAN=$DEFAULT_SAN
CN=""
COUNT=""
PREFIX=sim
START=1
DAYS=""
FORCE=0

while [ $# -gt 0 ]; do
    case "$1" in
        --force)
            FORCE=1
            shift
            continue
            ;;
        --ca-dir | --out | --san | --cn | --count | --prefix | --start | --days)
            [ $# -ge 2 ] || die "option $1 needs a value"
            val="$2"
            case "$1" in
                --ca-dir) CA_DIR="$val" ;;
                --out) OUT="$val" ;;
                --san) SAN="$val" ;;
                --cn) CN="$val" ;;
                --count) COUNT="$val" ;;
                --prefix) PREFIX="$val" ;;
                --start) START="$val" ;;
                --days) DAYS="$val" ;;
            esac
            shift 2
            ;;
        -h | --help)
            usage
            exit 0
            ;;
        *) die "unknown argument: $1" ;;
    esac
done

if [ -z "$DAYS" ]; then
    if [ "$SUB" = ca ]; then DAYS=3650; else DAYS=365; fi
fi
valid_int "$DAYS" && [ "$DAYS" -ge 1 ] || die "--days must be a positive integer"

case "$SUB" in
    ca) cmd_ca ;;
    server)
        [ -n "$OUT" ] || OUT=$DEFAULT_SERVER_OUT
        cmd_server
        ;;
    client)
        [ -n "$OUT" ] || OUT=$DEFAULT_SERVER_OUT
        cmd_client
        ;;
    devices)
        [ -n "$OUT" ] || OUT=$DEFAULT_DEVICES_OUT
        cmd_devices
        ;;
    -h | --help | help) usage ;;
    *) die "unknown subcommand: $SUB (expected ca, server, client or devices)" ;;
esac
