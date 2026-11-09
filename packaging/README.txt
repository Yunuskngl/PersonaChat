PersonaChat - English practice with fictional AI characters

BEFORE THE FIRST START
1. Install Ollama from https://ollama.com and open it.
2. Download the default model in a terminal:
   ollama pull gemma3:4b

START
macOS:   Move PersonaChat.app to Applications and double-click it.
         If macOS blocks it, open System Settings > Privacy & Security
         and choose "Open Anyway".
Windows: Open the PersonaChat folder and double-click PersonaChat.Web.exe.

The app opens in your browser. It stops by itself a few minutes after you
close the browser tab.

YOUR DATA
Conversations, memory notes and reports stay on your computer:
macOS:   ~/Library/Application Support/PersonaChat
Windows: %LOCALAPPDATA%\PersonaChat

USE ANOTHER OLLAMA MODEL
Create a file named settings.json in the folder above:
{ "Llm": { "Ollama": { "Model": "your-model-name" } } }
Then restart the app.
