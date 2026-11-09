on run argv
    set js to read POSIX file (item 1 of argv) as «class utf8»
    tell application "Safari"
        repeat with w in windows
            repeat with t in tabs of w
                if URL of t contains "appstoreconnect.apple.com" and URL of t contains "6827096867/testflight" and URL of t does not contain "/login" then
                    return do JavaScript js in t
                end if
            end repeat
        end repeat
    end tell
    error "Cascade App Store Connect tab not found"
end run
