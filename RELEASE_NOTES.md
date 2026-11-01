> 💡 **Релиз 6.8.0** — *Поддержка прямых дампов без тикетов Direct Dump RightsId-as-TitleKey Fallback (синхронизация с Eden fbfe0c95fa30), упреждающая инъекция ключей в LibHac и полная совместимость с RomFS, сжатием NCZ/XCZ и слиянием 4-в-1*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Криптография / Совместимость] Поддержка прямых дампов без тикетов (Direct Dump RightsId-as-TitleKey Fallback)**: во все криптографические конвейеры на Android (`StormNczEngine.kt`) и Windows (`StormNczCompressor.cs`, `StormNczStorage.cs`, `MultiContentService.cs`, `NszCompressionService.cs`, `RomFsService.cs`, `TitleKeyAuditService.cs`) внедрена архитектурная синхронизация с эмулятором Nintendo Switch **Eden** (`commit fbfe0c95fa30`). При отсутствии `.tik`-билета и записи в `title.keys` программа автоматически подставляет 16 сырых байт `RightsId` (`смещение 0x230..0x23F`) в качестве ключа `TitleKey` напрямую (без обёртки мастер-ключами `TitleKek`), сопровождая операцию верификацией по сигнатурам заголовков секций (`PFS0`, `IVFC`, `RomFS 0x50`).
- 🌟 **[LibHac и Мульти-контейнеры] Упреждающая инъекция ключей EnsureDirectDumpRightsIdFallback**: на платформе Windows перед открытием любого `Nca` автоматически проверяется наличие ключа для ненулевого `RightsId` и регистрируется `AccessKey(rightsIdBytes)` в системном наборе `ExternalKeySet`. В хранилище `StormNczStorage` исключено ошибочное использование чужого ключа из общего словаря `_titleKeys` при сборке и распаковке мульти-контейнеров с несколькими DLC, обладающими разными `RightsId`.
- 🌟 **[Экосистема форматов] Бесшовная обработка прямых дампов во всех операциях**: прямые дампы без тикетов теперь открываются в инспекторе `RomFS` с моментальным извлечением файлов, распаковываются, патчатся обновлениями и модификациями в режиме `4-в-1` (`Base + Update + DLC + Mods`) и сжимаются в высокоскоростные блочные контейнеры `NSZ` (`NCZBLOCK`) и `XCZ`.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Direct Dump Detection]**: сканирование RightsId и валидация заголовка секции через `SectionHeader` для 100% точного определения прямых дампов без ложных срабатываний.
- 🔹 **[Multi-DLC Key Isolation]**: изоляция индивидуальных прав доступа к ключам `RightsId` в PFS0 при наличии более 50 DLC в одном монолитном пакете.
- 🔹 **[Virtual RomFS Inspector]**: просмотр и распаковка ресурсов RomFS из прямых дампов без предварительного преобразования в Standard Crypto.
- 🔹 **[SmartShrink и MultiContent]**: корректное сохранение структуры прямых дампов при удалении лишних языковых дорожек и слиянии обновлений.
- 🔹 **[Android Cryptographic Parity]**: перенос логики RightsId-as-TitleKey в нативный движок `StormNczEngine.kt` с поддержкой 64-битных и legacy 32-битных архитектур.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **Direct Dump RightsId-as-TitleKey Fallback**: synchronized cryptographic pipeline with Nintendo Switch emulator **Eden** (`commit fbfe0c95fa30`). The engine automatically uses the 16 raw `RightsId` bytes (`offset 0x230..0x23F`) as the direct `TitleKey` (bypassing `TitleKek` decryption) when `.tik` tickets are absent, verified against `PFS0`, `IVFC`, and `RomFS 0x50` section headers.
- 🔸 **LibHac Preemptive Key Injection and Multi-DLC Safety**: automated `EnsureDirectDumpRightsIdFallback` injects `AccessKey(rightsIdBytes)` into `ExternalKeySet` before NCA access, eliminating key collisions across multiple DLCs with distinct `RightsIds` in `StormNczStorage`.
- 🔸 **Universal Direct Dump Tooling Compatibility**: ticketless direct dumps now seamlessly open in Virtual `RomFS` Inspector, compress into block `NCZBLOCK` / `XCZ` archives, and merge via `4-in-1` Multi-Content workflow.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
