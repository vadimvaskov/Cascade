# Cascade — TestFlight 1.0 (6)

Статус на **6 ноября 2026**: **Cascade 1.0 (6) загружена, обработана Apple и доступна существующей группе Internal из 9 тестировщиков.** Xcode подтвердил `Upload succeeded` и `EXPORT SUCCEEDED` в 18:39:54 UTC. В карточке новой сборки подтверждены версия 1.0 (6), группа Internal и 9 тестировщиков. Можно обновить приложение через TestFlight.

## Назначение и настройки

Используется существующее приложение [Cascade в App Store Connect](https://appstoreconnect.apple.com/apps/6827096867/testflight/ios), App ID `6827096867`, bundle ID `fitness.quokka.caskade`, команда Quokka `RUT46C38YA`. Загружена **1.0 (6)** для существующей внутренней группы **Internal** из 9 тестировщиков. Новые приглашения и отправка на App Review не входят в эту загрузку.

- Unity **6000.6.8f1**, iOS Device SDK, **IL2CPP Release**, без Development Build и подключения Profiler.
- iPhone + iPad, портретная ориентация, полноэкранный режим, минимальная iOS не ниже 15.0.
- В релиз входит только `Assets/Scenes/CascadePrototype.unity`; исходные сцены не включаются.
- Существующая иконка 1024×1024 скопирована в `Assets/Cascade/Art/AppIcon.png` и назначена во все 19 слотов iOS.
- Подписание автоматическое. `Tools/ExportOptions-TestFlight.plist` задаёт загрузку в App Store Connect, сохранение номера версии/сборки и отправку символов.

Настройки применяет `Assets/Cascade/Editor/CascadeIOSBuild.cs`: `Configure("1.0", "6")`, затем после переключения на iOS и завершения импорта — `Build("Builds/iOS/Xcode")`. Вызов Build выполняется в Edit Mode; при использовании Unity MCP ответ может завершиться тайм-аутом, пока Unity продолжает экспорт — проверять JSON-отчёт, не запускать вторую сборку; сам импорт скрипта не запускает сборку. Отчёт Unity: `Builds/iOS/build-status.json`. К архивированию переходить после `succeeded: true`.

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
  -archivePath Builds/iOS/Cascade-1.0-6.xcarchive \
  -derivedDataPath Builds/iOS/DerivedData \
  -jobs 4 \
  -allowProvisioningUpdates \
  DEVELOPMENT_TEAM=RUT46C38YA \
  CODE_SIGN_STYLE=Automatic \
  archive > Builds/iOS/archive.log 2>&1
```

После успешного завершения архива следующая команда **загружает сборку** согласно `destination=upload` в plist:

```sh
xcrun xcodebuild -exportArchive \
  -archivePath Builds/iOS/Cascade-1.0-6.xcarchive \
  -exportPath Builds/iOS/export-1.0-6 \
  -exportOptionsPlist Tools/ExportOptions-TestFlight.plist \
  -allowProvisioningUpdates > Builds/iOS/upload.log 2>&1
```

Для этой загрузки подтверждены код завершения 0, `EXPORT SUCCEEDED` в `upload.log` и обработанная **1.0 (6)** в App Store Connect. После обработки Apple необходимо отдельно проверить доступность сборки группе Internal; завершённый upload сам по себе этого не подтверждает. При повторном релизе сначала проверить занятые номера сборок.

## Что проверять в этой версии

Объёмное поле и музыкальные сферы, каскадные эффекты, постановки под музыку, множитель ×1–×10, пауза/возобновление, Safe Area, звук и плавность на устройстве. Правила размещения и каскадов сохранены. Новые Unity-пакеты не устанавливались.

Карты трёх треков предварительные: `analysisPassed=true`, `verified=false`, `reviewStatus=automated-review-pending-listen`. Автоматический анализ и проверка SHA-256 аудио пройдены; слуховая приёмка, полный естественный прогон всех треков и профилирование физического iPhone ещё не завершены. Подробнее: [README](../README.md), [проверки](Validation.md), [анализ музыки](AudioAnalysis.md).

## Проверка архива и предупреждения

`Tools/verify_ios_archive.py` проверяет подпись, профиль, Bundle ID, версию, архитектуру, privacy manifests и UUID dSYM. Отчёт текущей сборки: `Builds/iOS/preflight-1.0-6.json`.

Unity выдала 19 одинаковых предупреждений о компрессии импортированной иконки; все экспортированные размеры присутствуют, иконка App Store — 1024×1024 без alpha. Xcode сохранил реальные dSYM `Cascade.app` и `UnityFramework.framework`. Для статической `UnityRuntime` он внедрил пустую динамическую заглушку при копировании framework (`Injecting stub binary into codeless framework` в `archive.log`). Её отдельного dSYM нет; исключение явно записано в проверке, искусственные символы не создавались. Это не отсутствие символов игрового кода.

При экспорте Unity автоматически сохранила открытую `ConnectionTest`, уже имевшую несохранённые изменения до этой задачи. В пакет она не включена. Файл `SampleScene` не изменён.

Загрузка завершилась с ожидаемым предупреждением `Upload Symbols Failed` только для служебной заглушки UnityRuntime (UUID `7815EBAD-17A1-3FB8-A4D7-C72F75048F73`). Apple приняла приложение: `EXPORT SUCCEEDED`.

Итог публикации сохранён в `Builds/iOS/published-1.0-6.json`. Новые приглашения, внешняя beta-проверка и публичный App Review не выполнялись.
