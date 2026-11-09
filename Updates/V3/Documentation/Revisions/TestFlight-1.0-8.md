# Cascade — TestFlight 1.0 (8)

Статус на **7 ноября 2026**: **1.0 (8)** доступна в TestFlight группе **Internal** (9 тестировщиков). Загрузка завершилась 6 ноября в **19:45:43 UTC** (`EXPORT SUCCEEDED`), обработка Apple и назначение группы подтверждены. Добавлены настройка ритма на телефоне, синхронизация мерцания с поправкой и более явная обратная связь. Пользователь подтвердил нормальный зачёт ритма и завершил Midnight Floor: 181 ход, 179 попаданий, 1 промах и 1 ход в свободном ритме, 178 510 очков, ×10, поправка +142 мс. Журнал проверен через Unity MCP без расхождений; подробности — в Validation.md.

## Назначение и настройки

Используется существующее приложение [Cascade в App Store Connect](https://appstoreconnect.apple.com/apps/6827096867/testflight/ios), App ID `6827096867`, bundle ID `fitness.quokka.caskade`, команда Quokka `RUT46C38YA`. Опубликована **1.0 (8)** для существующей внутренней группы **Internal** из 9 тестировщиков. Новые приглашения не отправлялись; отправка на App Review не выполнялась.

- Unity **6000.6.8f1**, iOS Device SDK, **IL2CPP Release**, без Development Build и подключения Profiler.
- iPhone + iPad, портретная ориентация, полноэкранный режим, минимальная iOS не ниже 15.0.
- В релиз входит только `Assets/Scenes/CascadePrototype.unity`; исходные сцены не включаются.
- Существующая иконка 1024×1024 скопирована в `Assets/Cascade/Art/AppIcon.png` и назначена во все 19 слотов iOS.
- Подписание автоматическое. `Tools/ExportOptions-TestFlight.plist` задаёт загрузку в App Store Connect, сохранение номера версии/сборки и отправку символов.

Настройки применяет `Assets/Cascade/Editor/CascadeIOSBuild.cs`: `Configure("1.0", "8")`, затем после переключения на iOS и завершения импорта — `Build("Builds/iOS/Xcode")`. Вызов Build выполняется в Edit Mode; при использовании Unity MCP ответ может завершиться тайм-аутом, пока Unity продолжает экспорт — проверять JSON-отчёт, не запускать вторую сборку; сам импорт скрипта не запускает сборку. Отчёт Unity: `Builds/iOS/build-status.json`. К архивированию переходить после `succeeded: true`.

## Архив и загрузка

Команды выполняются из корня проекта. Используется уже установленный Xcode **26.6** и существующая учётная запись разработчика в Xcode; секреты в командах не нужны.

```sh
export DEVELOPER_DIR="/Users/anastasiavaskova/Downloads/Xcode.app/Contents/Developer"
xcrun xcodebuild -version
```

Архивирование экспортированного Unity-проекта:

```sh
xcrun xcodebuild \
  -project Builds/iOS/Xcode/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone \
  -configuration Release \
  -sdk iphoneos \
  -destination 'generic/platform=iOS' \
  -archivePath Builds/iOS/Cascade-1.0-8.xcarchive \
  -derivedDataPath Builds/iOS/DerivedData \
  -jobs 4 \
  -allowProvisioningUpdates \
  DEVELOPMENT_TEAM=RUT46C38YA \
  CODE_SIGN_STYLE=Automatic \
  archive > Builds/iOS/archive-1.0-8.log 2>&1
```

После успешного завершения архива следующая команда **загружает сборку** согласно `destination=upload` в plist:

```sh
xcrun xcodebuild -exportArchive \
  -archivePath Builds/iOS/Cascade-1.0-8.xcarchive \
  -exportPath Builds/iOS/export-1.0-8 \
  -exportOptionsPlist Tools/ExportOptions-TestFlight.plist \
  -allowProvisioningUpdates > Builds/iOS/upload-1.0-8.log 2>&1
```

Загрузка и обработка завершены; [карточка сборки 1.0 (8)](https://appstoreconnect.apple.com/teams/2a2b9828-a50d-4a91-8201-d046537c8f16/apps/6827096867/testflight/ios/e7455318-61ae-48b3-93d3-60ca0ccb5fa5) подтверждает Internal и 9 тестировщиков. Отчёт публикации: `Builds/iOS/published-1.0-8.json`. История: [TestFlight 1.0 (7)](Revisions/TestFlight-1.0-7.md).

## Что проверять в этой версии

Объёмное поле и музыкальные сферы, каскадные эффекты, постановки под музыку, множитель ×1–×10, пауза/возобновление, Safe Area, звук и плавность на устройстве. Правила размещения и каскадов сохранены. Новые Unity-пакеты не устанавливались.

Карты трёх треков предварительные: `analysisPassed=true`, `verified=false`, `reviewStatus=automated-review-pending-listen`. Автоматический анализ и проверка SHA-256 аудио пройдены; Midnight Floor полностью пройден пользователем на iPhone; полная слуховая приёмка карт, прохождение двух других треков и профилирование устройства ещё не завершены. Подробнее: [README](../README.md), [проверки](Validation.md), [анализ музыки](AudioAnalysis.md).

## Проверка архива и предупреждения

`Tools/verify_ios_archive.py` проверяет подпись, профиль, Bundle ID, версию, архитектуру, privacy manifests и UUID dSYM. Отчёт текущей сборки: `Builds/iOS/preflight-1.0-8.json`.

Unity выдала 19 одинаковых предупреждений о компрессии импортированной иконки и предупреждение о несобранных изменениях редакторского кода. Экспорт Player завершился без ошибок; в сгенерированном IL2CPP подтверждены `SetupCalibration`, `AcceptCalibrationPointer`, `ApplyCalibration`, `RhythmCalibration.TryEstimate`, поля поправки и диагностики. Код постобработки iOS не менялся. Все размеры иконок присутствуют, иконка App Store — 1024×1024 без alpha.

Для архива 1.0 (8) Xcode сохранил реальные dSYM `Cascade.app` и `UnityFramework.framework`, а для статической `UnityRuntime` внедрил пустую динамическую заглушку при копировании framework (`Injecting stub binary into codeless framework`). Её отдельного dSYM нет; исключение явно записывается в проверке, искусственные символы не создаются. Это не отсутствие символов игрового кода. Проверка текущего архива пройдена; ожидаемое исключение записано в `preflight-1.0-8.json`.

При экспорте Unity автоматически сохранила открытую `ConnectionTest`, уже имевшую несохранённые изменения до этой задачи. В пакет она не включена. Файл `SampleScene` не изменён.


## Исправление ввода в сборке 7

Нативное событие касания содержит укороченный payload, а прежний обработчик пытался читать весь 56-байтовый managed `TouchState`; Input System отклоняла чтение, поэтому UI работал, а поле — нет. Теперь из исходного события отдельно читаются phase, touchId и position; timestamp сохраняется.

`CascadeRuntimeChecks.RunNativeTouch()` воспроизводит отказ до исправления и проходит после него с 36-байтовым TOUC. Проверены быстрые тапы в одном обновлении, второй палец, Ended/Canceled и восстановление после отмены. Полные Runtime/Rules/Rhythm проверки также PASS. Пользователь подтвердил работу поля в сборке 7 на физическом iPhone. Это подтверждение касается постановки сфер; музыкальная синхронизация не принята. Отчёт: `Builds/iOS/touch-regression-1.0-7.json`.


## Настройка ритма в сборке 8

Откройте **Настройка ритма** из меню или паузы → **Начать проверку**. Первые два щелчка — разминка, затем нужно десять ровных касаний по кнопке в такт звуку. Настройка сохраняется только через **Применить**; выход и прерывание оставляют прежнее значение. При смене наушников/динамика измерение следует повторить. Допускается ручная поправка ±400 мс с шагом 10 мс.

Проверяйте Midnight Floor: подтверждение каждого попадания и прогресс до следующего множителя, сообщения рано/поздно, мерцание с поправкой. До четвёртого попадания множитель по правилам остаётся ×1. Карты, окно ±120 мс, блокировка на время каскада и модель поля не менялись. Dark Bass Drop по-прежнему имеет предварительную неоднозначную разметку.

Реальный журнал сборки 7: 51 попадание, 78 промахов; медианная знаковая ошибка +122.914 мс. Повторный расчёт тех же касаний с +100 мс даёт 113 попаданий / 16 промахов. Это моделирование, а не подтверждённый прогон новой сборки на телефоне; общий offset всем устройствам не назначен. Источники: `Builds/iOS/device-last-attempt-1.0-7.json`, `Builds/iOS/rhythm-device-analysis-1.0-7.json`.
