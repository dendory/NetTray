:: Build a .NET app
@echo off
%SYSTEMROOT%\Microsoft.NET\Framework\v4.0.30319\csc.exe /win32icon:src\nettray.ico /res:src\nettray.ico,res.notify_icon /target:winexe /out:%~dp0..\nettray.exe %~dp0nettray.cs