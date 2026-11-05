> 💡 **Релиз 6.9.6** — *Исправление конвейера пересборки игр Switch: сырой TitleKeyBlock для title.keys, ликвидация дедлоков stdout/stderr в hactoolnet/hacpack, валидация RomFS и исключение дублирующего Patch CNMT*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[TicketHarvester & title.keys] Сырой TitleKeyBlock для криптографического конвейера**: в `title.keys` и `ExternalKeySet` внедрена запись сырого `TitleKeyBlock` из бинарных билетов `.tik` (зашифрованный `TitleKek`), что полностью восстановило совместимость с `LibHac`, `hactoolnet` и `yanu-cli`, выполняющими штатную дешифровку ключей прав через TitleKek.
- 🌟 **[Ликвидация дедлоков stdout/stderr в hactoolnet и hacpack] Асинхронное дренирование каналов ввода-вывода**: во всех системных вызовах `hactoolnet` и `hacpack` при распаковке и компиляции RomFS, ExeFS и Control NCA реализовано асинхронное считывание (`BeginOutputReadLine` / `BeginErrorReadLine` с подпиской на события), что полностью устраняет 120-секундные дедлоки и таймауты из-за переполнения 4-килобайтного буфера анонимных каналов Windows.
- 🌟 **[Контроль полноты извлечения Base RomFS и 65% порог валидации]**: добавлена проверка завершённости и полноты дерева файлов RomFS при частичном выходе `hactoolnet`, а также внедрён адаптивный порог валидации размера выходного файла (не менее 65% от размера базы для полноразмерных игр), гарантирующий отсутствие битых или пустых образов.
- 🌟 **[Исключение дублирующего Patch CNMT 0x81 из монолитных NSP]**: в монолитные пакеты NSP теперь внедряется строго единый Program NCA (`...000`) и корневой Application CNMT (`...000.cnmt.nca`) с обновлённой версией приложения, исключая конфликтный мета-раздел Patch CNMT (`...800.cnmt.nca`), вызывавший ошибки монтирования в эмуляторе Eden.
- 🌟 **[Android Паритет и Синхронизация] Полнофункциональный релиз 6.9.6**: обновлены версия и код сборки (`versionCode = 696`, `versionName = "6.9.6"`) для Modern (64-бит ARMv8/x86_64) и Legacy (32-бит ARMv7/x86) редакций.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[TicketHarvesterService]**: метод сбора ключей теперь извлекает сырой зашифрованный TitleKeyBlock из `.tik` и добавляет его в `ExternalKeySet` и `title.keys`.
- 🔹 **[HardPatchEngine Async Drain]**: внедрены асинхронные обработчики `OutputDataReceived`/`ErrorDataReceived` для всех процессов `hactoolnet` (ExeFS, RomFS base, RomFS update, Control) и `hacpack`.
- 🔹 **[RomFS Extraction Guard]**: очистка и повторная попытка распаковки RomFS при аварийном завершении процесса без зависания конвейера.
- 🔹 **[Monolithic NSP CNMT Layout]**: удалено встраивание дублирующего файла `extraCnmtRebuiltPath` (`.cnmt.nca` с контентом Patch), предотвращая попытки эмуляторов обращаться к несуществующим секциям патча.
- 🔹 **[Adaptive Size Threshold]**: порог валидации размера Program NCA установлен в 65% от исходной базы при полной пересборке и 30% при фильтрации языковых дорожек.
- 🔹 **[Инсталлятор StormInstaller]**: сборка инсталлятора и метаданные обновлены до версии 6.9.6 с сохранением Authenticode-подписи STORM TEAM.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **TicketHarvester Raw TitleKeyBlock**: recorded raw `TitleKeyBlock` from `.tik` files into `title.keys` and `ExternalKeySet`, ensuring native decryption compatibility for LibHac, hactoolnet, and yanu-cli.
- 🔸 **Async Stdout/Stderr Pipe Drain (Deadlock Fix)**: eliminated 120-second process hangs in `hactoolnet` and `hacpack` by asynchronously draining output/error streams via `BeginOutputReadLine` and `BeginErrorReadLine`.
- 🔸 **Base RomFS Extraction Guard & 65% Size Threshold**: implemented partial-extraction detection and adaptive file size validation (65% of base game size) to guarantee uncorrupted RomFS outputs.
- 🔸 **Monolithic NSP Clean CNMT Layout**: prevented injection of duplicate Patch CNMT 0x81 (`...800.cnmt.nca`) into monolithic NSP files, resolving mounting errors in Eden and modern Switch emulators.
- 🔸 **Android Parity 6.9.6**: synchronized Modern and Legacy Android packages to version 6.9.6 (`versionCode 696`).

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
