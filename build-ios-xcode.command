#!/bin/bash
# Двойной клик в Finder: экспорт V4 и открытие Xcode.
cd "$(dirname "$0")"
exec bash ./Updates/V4/Tools/build-ios-xcode.sh "$@"