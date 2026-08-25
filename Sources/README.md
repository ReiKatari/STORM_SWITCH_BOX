<div align="center">

# ⚡ STORM SWITCH BOX

<p align="center">
  <b>Комплексный менеджер и конвертер игр Nintendo Switch (NSP/XCI/NSZ), управление установкой и резервным копированием.</b>
</p>

[![Version](https://img.shields.io/badge/version-4.7.7-00D2FF.svg?style=for-the-badge)](https://github.com/ReiKatari/STORM_SWITCH_BOX)
[![License](https://img.shields.io/badge/license-MIT-green.svg?style=for-the-badge)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-blue.svg?style=for-the-badge)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg?style=for-the-badge)](https://dotnet.microsoft.com/download/dotnet/8.0)

<p align="center">
  <a href="#возможности">Возможности</a> •
  <a href="#установка">Установка</a> •
  <a href="#требования">Требования</a> •
  <a href="#использование">Использование</a> •
  <a href="#структура-проекта">Структура</a> •
  <a href="#лицензия">Лицензия</a>
</p>

</div>

---

## 📖 О проекте

**STORM SWITCH BOX** — это современное, быстрое и многофункциональное приложение для Windows, предназначенное для работы с игровыми файлами **Nintendo Switch** и **Nintendo 3DS**, а также интерактивная энциклопедия всех 19 поколений игровых систем Nintendo (от Color TV-Game до Nintendo Switch 2). 

Программа построена на базе **.NET 8** и **Windows App SDK (WinUI 3)** с использованием современных принципов дизайна Windows 11 (Mica, Acrylic, темная и светлая темы) и строгого стандарта **STORM ALL PROJECTS FORMAT**.

---

## ⚡ Ключевые возможности

* 🔄 **Мульти-контент**: Сшивание базовой игры, обновлений, дополнений (DLC) и модификаций (RomFS/ExeFS) в один монолитный файл без дублирования данных.
* 📦 **Поддержка форматов**: NSP, NSZ, XCI, XCZ, 3DS, CIA, CXI.
* 🚀 **Высокая производительность**: Нативные C# алгоритмы и поддержка многопоточности.

---

## 📥 Установка

1. Запустите файл `STORM_SWITCH_BOX_4.7.7_Setup.exe`.
2. Выберите режим:
   * **Стандартная установка** — установка в C:\Program Files\STORM SWITCH BOX с созданием ярлыков и регистрацией в системе.
   * **Портативная версия** — распаковка в любую выбранную папку без изменения реестра.
3. Опция автоматической регистрации доверенного сертификата STORM TEAM позволяет навсегда исключить предупреждения SmartScreen и Smart App Control.

---

## 🛡️ Безопасность и Цифровая подпись / Code Signing

Все исполняемые файлы и инсталляторы подписаны сертификатом **STORM TEAM** с использованием хэширования SHA-256 и RFC 3161 Timestamping.

* Для ручной установки сертификата в хранилище доверенных корневых центров запустите:
  Files\Разблокировать_И_Установить_Сертификат.bat от имени Администратора.

---

## 📁 Структура репозитория / Structure

* Assembling/ — скомпилированные релизные бинарные файлы и зависимости программы.
* Files/ — инсталлятор, сертификат STORM_Certificate.cer и сервисные скрипты.
* Sources/ — исходный код решения.

---

## 👥 Авторы и Лицензия / Credits

* **Разработчик:** [ReiKatari](https://github.com/ReiKatari)
* **Издатель:** **STORM TEAM**
* © 2026 STORM TEAM. Все права защищены.