#!/usr/bin/env bash

echo "--- ODA File Converter Check ---"

for command_name in ODAFileConverter ODAFileConverter.exe; do
    if command -v "$command_name" >/dev/null 2>&1; then
        echo "[OK] Found in PATH: $(command -v "$command_name")"
        exit 0
    fi
done

if [ "$(uname -s)" = "Darwin" ]; then
    for path in \
        "/Applications/ODAFileConverter.app/Contents/MacOS/ODAFileConverter" \
        "/Applications/ODAFileConverter 24.11.0.app/Contents/MacOS/ODAFileConverter"; do
        if [ -f "$path" ]; then
            echo "[OK] Found at: $path"
            exit 0
        fi
    done
fi

echo "[FAIL] ODA File Converter was not found."
echo "Install it, add it to PATH, or select its executable from the Metraj settings window."
exit 1
