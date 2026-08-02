#!/usr/bin/env bash

set -euo pipefail

version="${1:-}"
configuration="${2:-Release}"

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Version must use major.minor.patch format, for example 0.1.0." >&2
    exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_dir/.." && pwd)"
macos_root="$repository_root/macos"
artifacts_directory="$macos_root/artifacts"
product_name="${SUB2BAR_MACOS_PRODUCT_NAME:-Sub2Bar}"
scheme="${SUB2BAR_MACOS_SCHEME:-Sub2Bar}"
container="${SUB2BAR_MACOS_CONTAINER:-}"

if ! command -v xcodebuild >/dev/null 2>&1; then
    echo "xcodebuild was not found. Run this script on macOS with Xcode installed." >&2
    exit 1
fi

if ! command -v hdiutil >/dev/null 2>&1; then
    echo "hdiutil was not found. Run this script on macOS." >&2
    exit 1
fi

if [[ -n "$container" && "$container" != /* ]]; then
    container="$macos_root/$container"
fi

if [[ -z "$container" ]]; then
    if [[ -d "$macos_root/Sub2Bar.xcworkspace" ]]; then
        container="$macos_root/Sub2Bar.xcworkspace"
    elif [[ -d "$macos_root/Sub2Bar.xcodeproj" ]]; then
        container="$macos_root/Sub2Bar.xcodeproj"
    else
        echo "No Xcode workspace or project was found under $macos_root." >&2
        exit 1
    fi
fi

case "$container" in
    *.xcworkspace)
        container_arguments=(-workspace "$container")
        ;;
    *.xcodeproj)
        container_arguments=(-project "$container")
        ;;
    *)
        echo "SUB2BAR_MACOS_CONTAINER must point to an .xcworkspace or .xcodeproj." >&2
        exit 1
        ;;
esac

if [[ ! -d "$container" ]]; then
    echo "Xcode container does not exist: $container" >&2
    exit 1
fi

IFS=. read -r version_major version_minor version_patch <<< "$version"
build_number=$((10#$version_major * 1000000 + 10#$version_minor * 1000 + 10#$version_patch))
temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/sub2bar-package.XXXXXX")"
trap 'rm -rf "$temporary_directory"' EXIT

archive_path="$temporary_directory/$product_name.xcarchive"
staging_directory="$temporary_directory/dmg"
application_path="$archive_path/Products/Applications/$product_name.app"
disk_image="$artifacts_directory/$product_name-$version-macos.dmg"

xcodebuild \
    "${container_arguments[@]}" \
    -scheme "$scheme" \
    -configuration "$configuration" \
    -archivePath "$archive_path" \
    MARKETING_VERSION="$version" \
    CURRENT_PROJECT_VERSION="$build_number" \
    archive

if [[ ! -d "$application_path" ]]; then
    echo "Archived application was not found: $application_path" >&2
    exit 1
fi

mkdir -p "$artifacts_directory" "$staging_directory"
ditto "$application_path" "$staging_directory/$product_name.app"
ln -s /Applications "$staging_directory/Applications"
hdiutil create \
    -volname "$product_name" \
    -srcfolder "$staging_directory" \
    -format UDZO \
    -ov \
    "$disk_image"

echo "Disk image is ready at $disk_image"
