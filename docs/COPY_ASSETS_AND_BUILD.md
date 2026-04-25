# Copy Assets and Build

From the extracted v0.3.2.5 folder:

```powershell
cd C:\\\\meditate-soundbox-v0.3.2.5\\\\meditative-reader-starter

Copy-Item -Recurse -Force C:\\\\meditate-soundbox-v0.3.2.5\\\\meditative-reader-starter\\\\tools .\\\\
Copy-Item -Recurse -Force C:\\\\meditate-soundbox-v0.3.2.5\\\\meditative-reader-starter\\\\voices .\\\\
Copy-Item -Recurse -Force C:\\\\meditate-soundbox-v0.3.2.5\\\\meditative-reader-starter\\\\data .\\\\

dotnet build .\\\\src\\\\MeditativeReader.Windows
```

If the same source and destination are used by accident, PowerShell may warn. Use a previous known-good folder as the source when setting up a fresh extraction.

