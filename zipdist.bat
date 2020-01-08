@echo off

echo "Make sure to update readme / ini file before doing this"
set /p id="Enter Custom Camera V version: "

mkdir "C:\tmp\Custom Camera V %id%"
robocopy "dist" "C:\tmp\Custom Camera V %id%"

"C:\Program Files\7-Zip\7z" a -tzip "%USERPROFILE%\desktop\Custom Camera V %id%.zip" "C:\tmp\Custom Camera V %id%"
RD /S /Q "C:\tmp\Custom Camera V %id%"
explorer /select,"%USERPROFILE%\desktop\Custom Camera V %id%.zip"
echo "Done."
pause