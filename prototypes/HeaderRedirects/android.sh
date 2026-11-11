#!/usr/bin/env bash
# Runs inside android-emulator-runner: installs and starts the app, then collects the results.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
apk=$(find "$here/App/bin/Debug/net10.0-android" -name '*-Signed.apk' | head -n 1)
echo "APK: $apk"
adb install -r "$apk"
adb logcat -c
adb shell am start -n com.mafesoft.headerredirects/com.mafesoft.headerredirects.MainActivity
"$here/run-scenarios.sh" "Android emulator" "$here/out"
result=$?
adb logcat -d > "$here/out/logcat.txt" || true
exit $result
