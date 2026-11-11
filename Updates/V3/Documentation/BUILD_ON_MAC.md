# Сборка Cascade на Mac → TestFlight

Подготовлена версия **1.0 (15)**. Новый дизайн и 3D-мир находятся в **Updates/V3**. Последний документированный загруженный выпуск — 1.0 (14). Эта инструкция не означает, что 15 уже загружена.

## 1. Подготовить Mac

Установить в Unity Hub **6000.6.8f1** с модулем **iOS Build Support**. Использовать стабильный Xcode, совместимый с macOS; предыдущий выпуск проекта собирался в Xcode 27.1. Apple сейчас требует Xcode 26+ и iOS SDK 26+ для загрузки: [официальное требование](https://developer.apple.com/news/upcoming-requirements/?id=04282026a).

Открыть Xcode один раз, завершить установку компонентов и войти в Apple Account с доступом к команде **Quokka / RUT46C38YA**. В Xcode → Settings → Locations выбрать Command Line Tools текущего Xcode. Нужен Python 3 для проверки скачанных файлов.

## 2. Получить проект

Если репозитория ещё нет:

```bash
git clone https://github.com/vadimvaskov/Cascade.git
cd Cascade/Updates/V3
```

Если он уже есть, сначала сохранить свои локальные изменения, затем из корня репозитория:

```bash
git switch main
git pull --ff-only origin main
cd Updates/V3
```

Здесь не требуется Git LFS: модели и текстуры находятся непосредственно в Git. Для первого импорта добавить **Updates/V3** через Unity Hub, дождаться восстановления пакетов и открыть Assets/Scenes/CascadeGame.unity. Посмотреть игру через Play, затем **закрыть этот проект в Unity** перед запуском пакетной сборки.

## 3. Экспортировать Xcode-проект

В Terminal из папки Updates/V3:

```bash
bash Tools/build-ios-mac.sh 15
```

Скрипт проверяет ресурсы и регистр имён файлов, использует нужную версию Unity, настраивает IL2CPP Release и запускает проверки. Он экспортирует **только CascadeGame.unity**, затем открывает:

```text
Updates/V3/Builds/iOS/1.0-15/Xcode/Unity-iPhone.xcodeproj
```

Журнал: Builds/iOS/1.0-15/unity-export.log. Отчёт Unity: Builds/iOS/build-status-1.0-15.json. Старые папки экспорта автоматически не перезаписываются. При неудачной попытке сохраните/переименуйте её папку, затем повторите команду, либо выберите следующий номер. Если 15 уже использована в App Store Connect, запустите команду с новым, более высоким номером.

Если Unity установлена в другом месте, задайте путь только для этой команды:

```bash
UNITY_EDITOR="/путь/Unity.app/Contents/MacOS/Unity" bash Tools/build-ios-mac.sh 15
```

Unity генерирует Xcode-проект, а Xcode собирает и подписывает iOS-приложение: [порядок сборки Unity](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/iphone/ios-building-and-delivering/build-process).

## 4. Архив и загрузка

В открытом Xcode выбрать **Unity-iPhone** и **Any iOS Device (arm64)**, конфигурацию Release. В Signing & Capabilities проверить Automatic Signing и команду **Quokka**. Не менять идентификатор существующего приложения:

| Настройка | Значение |
|---|---|
| Bundle ID | fitness.quokka.caskade |
| Version / Build | 1.0 / 15 (или выбранный новый номер) |
| Team | RUT46C38YA |
| App Store Connect App ID | 6827096867 |
| Минимальная iOS | 15.0 |
| Ориентация | Portrait, iPhone + iPad |

Выполнить **Product → Archive**. В Organizer выбрать новый архив, проверить номер и время, затем **Distribute App → App Store Connect → Upload**. Оставить загрузку символов включённой. После успешной отправки дождаться обработки Apple в [App Store Connect](https://appstoreconnect.apple.com/apps/6827096867/testflight/ios), выбрать новую сборку и добавить её группе Internal. При запросе Apple проверить ответы по шифрованию и доступности тестирования; экспорт выставляет ITSAppUsesNonExemptEncryption=false, как предыдущие выпуски.

Скрипт экспорта ничего не загружает в Apple. Существующий Tools/ExportOptions-TestFlight.plist предназначен для отдельной командной загрузки; обычный путь через Organizer достаточен.

## 5. Проверка на устройстве

Проверить оба персонажа, новый главный экран и настройки, свечение фонарей, одну летящую сферу, объёмную энергию **от неё к герою**, остановку эффектов на паузе, переходы ¼ → ⅛ → ¹⁄₁₆, игру без музыки и рост пульсации к ×10. Проверить Safe Area на iPhone/iPad, вибрацию, аудиопрерывания, нагрев и стабильность кадров.

На Windows прошли проверки логики, ресурсов, игрового режима и запуск player. iOS/Metal/IL2CPP и Xcode на этой машине не запускались; их результат станет известен после сборки на Mac.
