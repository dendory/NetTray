:: Build a .NET app
@%SYSTEMROOT%\Microsoft.NET\Framework\v4.0.30319\csc.exe /win32icon:src\nettray.ico /res:src\nettray.ico,res.notify_icon /target:winexe /out:%~dp0..\nettray.exe %~dp0nettray.cs
@call "C:\Program Files (x86)\Windows Kits\8.0\bin\x64\signtool.exe" sign /n "Patrick Lambert" /t http://timestamp.verisign.com/scripts/timstamp.dll %~dp0..\nettray.exe
