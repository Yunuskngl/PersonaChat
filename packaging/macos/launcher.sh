#!/bin/sh
# Finder starts apps without a terminal, so the server's output goes to a log file.
log_folder="$HOME/Library/Logs/PersonaChat"
mkdir -p "$log_folder"
exec "$(dirname "$0")/../Resources/app/PersonaChat.Web" >>"$log_folder/server.log" 2>&1
