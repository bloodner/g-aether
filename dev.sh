#!/bin/bash
# G-Helper WPF Dev Helper
# Usage: ./dev.sh [command]
#   start | s   - Kill existing, build, and run
#   kill  | k   - Kill running instance
#   build | b   - Kill existing and build only
#   clean | c   - Clean build artifacts
#   run   | r   - Run without rebuilding

PROJECT="app/GHelper.WPF/GHelper.WPF.csproj"
EXE_NAME="GHelper.WPF.exe"

kill_app() {
    taskkill //f //im "$EXE_NAME" 2>/dev/null && echo "Killed $EXE_NAME" || echo "Not running"
}

case "${1:-start}" in
    start|s)
        kill_app
        dotnet build "$PROJECT" && dotnet run --project "$PROJECT" &
        echo "Started. PID: $!"
        ;;
    kill|k)
        kill_app
        ;;
    build|b)
        kill_app
        dotnet build "$PROJECT"
        ;;
    clean|c)
        kill_app
        dotnet clean "$PROJECT"
        ;;
    run|r)
        kill_app
        dotnet run --project "$PROJECT" &
        echo "Started. PID: $!"
        ;;
    *)
        echo "Usage: ./dev.sh [start|kill|build|clean|run]"
        ;;
esac
