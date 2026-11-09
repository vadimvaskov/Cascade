# Cascade — TestFlight 1.0 (7)

Статус на **6 ноября 2026**: **1.0 (7)** с исправлением неработающих касаний поля на iPhone доступна в TestFlight для группы **Internal** (9 тестировщиков). Загрузка завершилась в 19:07:49 UTC (`EXPORT SUCCEEDED`); обработка Apple и назначение группы подтверждены. Пользователь подтвердил, что на физическом телефоне сферы ставятся, и сообщил о проблеме засчитывания ритма через встроенный динамик — она исследуется отдельно. **1.0 (6)** содержит ошибку ввода и требует обновления.

## Назначение и настройки

Используется существующее приложение [Cascade в App Store Connect](https://appstoreconnect.apple.com/apps/6827096867/testflight/ios), App ID `6827096867`, bundle ID `fitness.quokka.caskade`, команда Quokka `RUT46C38YA`. Опубликована **1.0 (7)** для существующей внутренней группы **Internal** из 9 тестировщиков. Новые приглашения не отправлялись; отправка на App Review не выполнялась.

- Unity **6000.6.8f1**, iOS Device SDK, **IL2CPP Release**, без Development Build и подключения Profiler.
- iPhone + iPad, портретная ориентация, полноэкранный режим, минимальная iOS не ниже 15.0.
- В релиз входит только `Assets/Scenes/CascadePrototype.unity`; исходные сцены не включаются.
- Существующая иконка 1024×1024 скопирована в `Assets/Cascade/Art/AppIcon.png` и назначена во все 19 слотов iOS.
- Подписание автоматическое. `Tools/ExportOptions-TestFlight.plist` задаёт загрузку в App Store Connect, сохранение номера версии/сборки и отправку символов.

Настройки применяет `Assets/Cascade/Editor/CascadeIOSBuild.cs`: `Configure("1.0", "7")`, затем после переключения на iOS и завершения импорта — `Build("Builds/iOS/Xcode")`. Вызов Build выполняется в Edit Mode; при использовании Unity MCP ответ может завершиться тайм-аутом, пока Unity продолжает экспорт — проверять JSON-отчёт, не запускать вторую сборку; сам импорт скрипта не запускает сборку. Отчёт Unity: `Builds/iOS/build-status.json`. К архивированию переходить после `succeeded: true`.

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
  -archivePath Builds/iOS/Cascade-1.0-7.xcarchive \
  -derivedDataPath Builds/iOS/DerivedData \
  -jobs 4 \
  -allowProvisioningUpdates \
  DEVELOPMENT_TEAM=RUT46C38YA \
  CODE_SIGN_STYLE=Automatic \
  archive > Builds/iOS/archive-1.0-7.log 2>&1
```

После успешного завершения архива следующая команда **загружает сборку** согласно `destination=upload` в plist:

```sh
xcrun xcodebuild -exportArchive \
  -archivePath Builds/iOS/Cascade-1.0-7.xcarchive \
  -exportPath Builds/iOS/export-1.0-7 \
  -exportOptionsPlist Tools/ExportOptions-TestFlight.plist \
  -allowProvisioningUpdates > Builds/iOS/upload-1.0-7.log 2>&1
```

Загрузка завершилась `EXPORT SUCCEEDED`. Обработка Apple завершена; [карточка 1.0 (7)](https://appstoreconnect.apple.com/teams/2a2b9828-a50d-4a91-8201-d046537c8f16/apps/6827096867/testflight/ios/339d77f1-cc7a-4a7c-a267-8b652fab66d4) подтверждает назначение Internal. Отчёт: `Builds/iOS/published-1.0-7.json`. История предыдущей публикации: [TestFlight 1.0 (6)](Revisions/TestFlight-1.0-6.md).

## Что проверять в этой версии

Объёмное поле и музыкальные сферы, каскадные эффекты, постановки под музыку, множитель ×1–×10, пауза/возобновление, Safe Area, звук и плавность на устройстве. Правила размещения и каскадов сохранены. Новые Unity-пакеты не устанавливались.

Карты трёх треков предварительные: `analysisPassed=true`, `verified=false`, `reviewStatus=automated-review-pending-listen`. Автоматический анализ и проверка SHA-256 аудио пройдены; слуховая приёмка, полный естественный прогон всех треков и профилирование физического iPhone ещё не завершены. Подробнее: [README](../README.md), [проверки](Validation.md), [анализ музыки](AudioAnalysis.md).

## Проверка архива и предупреждения

`Tools/verify_ios_archive.py` проверяет подпись, профиль, Bundle ID, версию, архитектуру, privacy manifests и UUID dSYM. Отчёт текущей сборки: `Builds/iOS/preflight-1.0-7.json`.

Unity выдала 19 одинаковых предупреждений о компрессии импортированной иконки; все экспортированные размеры присутствуют, иконка App Store — 1024×1024 без alpha. Xcode сохранил реальные dSYM `Cascade.app` и `UnityFramework.framework`. Для статической `UnityRuntime` он внедрил пустую динамическую заглушку при копировании framework (`Injecting stub binary into codeless framework` в `archive-1.0-7.log`). Её отдельного dSYM нет; исключение явно записано в проверке, искусственные символы не создавались. Это не отсутствие символов игрового кода.

При экспорте Unity автоматически сохранила открытую `ConnectionTest`, уже имевшую несохранённые изменения до этой задачи. В пакет она не включена. Файл `SampleScene` не изменён.


## Исправление ввода в сборке 7

Нативное событие касания содержит укороченный payload, а прежний обработчик пытался читать весь 56-байтовый managed `TouchState`; Input System отклоняла чтение, поэтому UI работал, а поле — нет. Теперь из исходного события отдельно читаются phase, touchId и position; timestamp сохраняется.

`CascadeRuntimeChecks.RunNativeTouch()` воспроизводит отказ до исправления и проходит после него с 36-байтовым TOUC. Проверены быстрые тапы в одном обновлении, второй палец, Ended/Canceled и восстановление после отмены. Полные Runtime/Rules/Rhythm проверки также PASS. Пользователь подтвердил работу поля в сборке 7 на физическом iPhone. Это подтверждение касается постановки сфер; музыкальная синхронизация не принята. Отчёт: `Builds/iOS/touch-regression-1.0-7.json`.
