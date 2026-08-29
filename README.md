<div align="center">

<img src="logo.png" width="128" height="128" alt="STORM SWITCH BOX Logo" />

# ⚡ STORM SWITCH BOX

<p align="center">
  <b>Высокопроизводительный комплекс для управления, умной обработки, конвертации и верификации игр Nintendo Switch и Nintendo 3DS.</b>
</p>

[![Version](https://img.shields.io/badge/version-4.9.5-00D2FF.svg?style=for-the-badge)](https://github.com/ReiKatari/STORM_SWITCH_BOX)
[![Platform](https://img.shields.io/badge/Platform-Windows%2011%20%7C%2010-0EA5E9.svg?style=for-the-badge)](https://github.com/ReiKatari/STORM_SWITCH_BOX)
[![Framework](https://img.shields.io/badge/.NET-8.0%20WinUI%203-7C3AED.svg?style=for-the-badge)](https://dotnet.microsoft.com/)
[![Publisher](https://img.shields.io/badge/Publisher-STORM%20TEAM-10B981.svg?style=for-the-badge)](https://github.com/ReiKatari)
[![Developer](https://img.shields.io/badge/Author-ReiKatari-F59E0B.svg?style=for-the-badge)](https://github.com/ReiKatari)
[![Signed](https://img.shields.io/badge/Security-SHA256%20Signed-10B981.svg?style=for-the-badge)](https://github.com/ReiKatari)

</div>

---

## 🌟 О проекте / Overview

**STORM SWITCH BOX** — это флагманский инструмент экосистемы **STORM**, созданный для комплексной работы с игровыми архивами и библиотеками консолей **Nintendo Switch** и **Nintendo 3DS**. Приложение объединяет нативные высокоскоростные алгоритмы сшивки, декомпрессии и сжатия, систему умного анализа обновлений, полноценную кросс-платформенную библиотеку на 19 игровых поколений и средства глубокой верификации целостности файлов.

---

## 🚀 Ключевые возможности / Key Features

### 🧠 Умная обработка (Smart Processing)
* **Автоматический анализ контента**: программа автоматически оценивает соотношение размеров базовой игры и файла обновления:
  * **Легковесные патчи (< 40% от базы)**: прямое нативное сшивание **LibHac PFS0** без полной распаковки RomFS на диск — гарантирует максимальную скорость и исходный компактный размер контейнера.
  * **Массивные обновления (≥ 40% от базы)**: физическая пересборка **HardPatch** для очистки устаревших и дублирующихся ресурсов базовой игры.
* **Интеграция модификаций**: физическое вшивание `romfs`, `exefs` и IPS-патчей непосредственно в Program RomFS игры с сохранением совместимости и чистого запуска в эмуляторах.
* **Строгая системная сортировка PFS0**: гарантированный системный порядок метаданных (Base CNMT $\rightarrow$ Patch CNMT $\rightarrow$ Control $\rightarrow$ Program $\rightarrow$ DLC $\rightarrow$ Tickets), предотвращающий сбои и вылеты эмуляторов.

### 🔄 Конвертация и поддержка форматов
* **Nintendo Switch**: нативная и потоковая конвертация между `NSP`, `XCI`, `NSZ`, `XCZ`.
* **Nintendo 3DS**: изолированная экосистема конвертации и мульти-сборки `3DS (CCI)` $\leftrightarrow$ `CIA` $\leftrightarrow$ `CXI`.
* **Zstandard Сжатие**: аппаратное многопоточное сжатие в формат `NSZ`/`XCZ` с автоматической валидацией и регенерацией хеш-деревьев IVFC.

### 📚 Игровая библиотека на 19 поколений (Game Library)
* Поддержка 19 игровых систем: Switch, 3DS, Wii U, Wii, GameCube, N64, SNES, NES, GBA, NDS, PS1, PS2, PS3, PSP, PS Vita, Xbox и ретро-платформы.
* Автоматическая загрузка обложек, интеграция с TitleDB и eShop, редактирование метаданных, быстрый запуск в назначенных эмуляторах.

### 🛡️ Верификация и Каталог (Catalog & Verification)
* Сканирование накопителей, проверка целостности заголовков NCA/NCZ/CIA/CCI, сверка хешей и выявление битых файлов.

---

## 💻 Системные требования / System Requirements

* **Операционная система:** Windows 10 (x64, версия 19041+) / Windows 11
* **Платформа:** .NET 8.0 Desktop Runtime / Windows App SDK (включены в инсталлятор)
* **Архитектура:** x64

---

## 📦 Установка / Installation

Установка производится через инсталлятор **STORM INSTALLER**:

1. Скачайте и запустите релизный файл `STORM_SWITCH_BOX_4.8.6_Setup.exe`.
2. Выберите желаемый вариант:
   * **Стандартная установка** — установка в `C:\Program Files\STORM SWITCH BOX` с созданием ярлыков и регистрацией в системе.
   * **Портативная версия** — распаковка в любую выбранную папку без изменения реестра.
3. Опция автоматической регистрации сертификата **STORM TEAM** исключает предупреждения SmartScreen и Smart App Control.

---

## 🛡️ Безопасность и Цифровая подпись / Code Signing

Все исполняемые файлы, библиотеки и инсталляторы подписаны сертификатом **STORM TEAM** с алгоритмом хеширования SHA-256 и меткой времени RFC 3161 (DigiCert).

* Для ручной установки сертификата запустите файл `Files\Разблокировать_И_Установить_Сертификат.bat` от имени Администратора.

---

## 📁 Структура файлов репозитория / Structure

* `Assembling/` — готовые скомпилированные релизные бинарные файлы и зависимости.
* `Files/` — инсталлятор `STORM_SWITCH_BOX_4.8.6_Setup.exe`, сертификат `STORM_Certificate.cer` и сервисные скрипты.
* `installer/` — исходный код и ресурсы инсталлятора StormInstaller.
* `tools/` — утилиты для работы с образами и контейнерами (yanu-cli, hacpack, hactoolnet, nsz, 3dstool, ctrtool, makerom).

---

## 👥 Авторы и Лицензия / Credits

* **Разработчик:** [ReiKatari](https://github.com/ReiKatari)
* **Издатель:** **STORM TEAM**
* © 2026 STORM TEAM. Все права защищены.