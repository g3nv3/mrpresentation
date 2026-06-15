# Location Bridge

Компоненты для передачи GPS-координат телефона в сцену шлема Pico. Телефон получает реальную геолокацию через Android `Input.location`, отправляет ее по UDP, шлем принимает пакеты и может обновлять `YandexHelmetNavigator.CurrentCoordinate`.

## Что здесь решается

Pico 4 Ultra сам не дает GPS как обычный смартфон. Поэтому схема такая:

1. Телефон лежит у человека или в кармане.
2. Телефон получает координаты через GPS/геолокацию Android.
3. Телефон отправляет координаты в локальную сеть.
4. Pico принимает координаты и считает их текущей координатой человека.

Это не строит маршрут само по себе. Это только источник текущей широты/долготы для твоей MR/Яндекс-логики.

## Почему Wi-Fi/UDP, а не Bluetooth

Для текущей задачи лучше использовать Wi-Fi UDP.

Причины:

- В Unity на Android UDP работает обычным C#-кодом без отдельного нативного Bluetooth-плагина.
- Не нужен BLE GATT-сервер, pairing, service UUID, characteristic UUID и разные разрешения Android 12+ (`BLUETOOTH_SCAN`, `BLUETOOTH_CONNECT`, `BLUETOOTH_ADVERTISE`).
- Координаты маленькие и отправляются часто. Если один UDP-пакет потеряется, следующий придет через секунду.
- Pico и телефон легко объединить одной Wi-Fi сетью или hotspot с телефона.

Bluetooth имеет смысл только если Wi-Fi вообще нельзя использовать. Тогда придется писать отдельный Android-плагин или подключать готовый BLE-плагин.

## Какая сеть нужна

Телефон и Pico должны быть в одной локальной сети, чтобы UDP-пакеты доходили до шлема.

### Вариант 1: телефон раздает hotspot

Рекомендуемый вариант для полевых тестов.

1. На телефоне включи мобильный интернет и точку доступа Wi-Fi.
2. Подключи Pico к Wi-Fi сети телефона.
3. Запусти телефонную Unity-сцену.
4. Запусти сцену на Pico.

Плюсы:

- Не нужен роутер.
- Телефон точно рядом с человеком.
- Меньше проблем с корпоративными/университетскими Wi-Fi сетями.

Минусы:

- Некоторые телефоны могут плохо пропускать broadcast-пакеты на hotspot.
- Если broadcast не работает, нужно указать IP шлема вручную.

### Вариант 2: общий Wi-Fi роутер

Подходит для разработки в помещении.

1. Подключи телефон и Pico к одной Wi-Fi сети.
2. Убедись, что сеть не изолирует клиентов друг от друга.
3. Используй broadcast `255.255.255.255` или прямой IP Pico.

Важный нюанс: многие публичные/корпоративные сети включают client isolation. В такой сети телефон и Pico оба имеют интернет, но не видят друг друга. UDP тогда не пройдет.

### Вариант 3: прямой IP вместо broadcast

Если broadcast не работает, это нормальная ситуация. Тогда на телефоне в `PhoneLocationUdpBroadcaster` задай IP Pico:

```csharp
broadcaster.SetTarget("192.168.43.123", 47777);
broadcaster.StartBroadcasting();
```

IP Pico можно посмотреть в настройках Wi-Fi шлема. Нужен именно IP шлема в той сети, к которой подключен телефон.

## Компоненты

## Внешнее API для меню и игровой логики

Основная идея API: меню управляет сессией, а не отдельными UDP-пакетами.

Снаружи обычно нужны только действия:

- выбрать адрес шлема или broadcast;
- включить передачу на телефоне;
- выключить передачу на телефоне;
- включить прием на шлеме;
- выключить прием на шлеме;
- получить последнюю координату или подписаться на обновления;
- опционально прокинуть координату в `YandexHelmetNavigator`.

Ручная пересылка каждого пакета не нужна. После запуска `PhoneLocationUdpBroadcaster` сам отправляет координаты в `Update()` с интервалом `Send Interval Seconds`. После запуска `PhoneLocationUdpReceiver` сам слушает UDP в фоне и вызывает `LocationReceived`, когда приходит новая координата.

### API телефонной сцены

На телефоне публичной точкой управления являются `PhoneGpsLocationProvider` и `PhoneLocationUdpBroadcaster`.

#### `broadcaster.SetTarget(address, port)`

Настраивает, куда телефон будет постоянно отправлять координаты.

Используй перед `StartBroadcasting()`, когда пользователь в меню выбирает режим связи:

```csharp
// Автопоиск в локальной сети через broadcast.
broadcaster.SetTarget("255.255.255.255", 47777);

// Более надежный режим: отправлять прямо на IP Pico.
broadcaster.SetTarget("192.168.43.123", 47777);
```

`address`:

- `255.255.255.255` - телефон рассылает координату всем устройствам в локальной сети;
- IP шлема, например `192.168.43.123` - телефон отправляет только на Pico.

`port` должен совпадать с `PhoneLocationUdpReceiver/Listen Port` на шлеме. По умолчанию используется `47777`.

Этот метод не запускает GPS и не отправляет данные сам по себе. Он только меняет сетевую настройку.

#### `gps.StartLocationService()`

Запускает Android location service на телефоне.

При первом запуске Android покажет системное окно разрешения геолокации. Если пользователь не даст разрешение или геолокация выключена в настройках телефона, координаты не появятся и вызовется `gps.Failed`.

Обычно этот метод можно не вызывать отдельно, потому что `PhoneLocationUdpBroadcaster` имеет поле `Start Location Provider`. Если оно включено, `broadcaster.StartBroadcasting()` сам запускает GPS.

Используй отдельно только если в меню нужно показать статус GPS до начала отправки.

#### `broadcaster.StartBroadcasting()`

Запускает постоянную передачу координат.

Что происходит внутри:

1. Если включен `Start Location Provider`, запускается `PhoneGpsLocationProvider`.
2. Создается UDP socket.
3. Запоминается endpoint из `SetTarget(...)` или инспектора.
4. В каждом `Update()` broadcaster проверяет, есть ли свежая координата.
5. Если прошел `Send Interval Seconds`, координата отправляется автоматически.

Меню должно вызывать этот метод один раз при нажатии "поделиться геопозицией" или "подключиться к шлему".

```csharp
public void EnablePhoneSharing(string picoIp)
{
    broadcaster.SetTarget(picoIp, 47777);
    broadcaster.StartBroadcasting();
}
```

#### `broadcaster.StopBroadcasting()`

Останавливает UDP-передачу.

Метод закрывает socket и перестает отправлять координаты. Сейчас GPS provider при этом не обязательно останавливается, потому что он может использоваться UI для отображения текущей координаты на телефоне.

Если нужно полностью остановить и GPS, вызывай оба метода:

```csharp
public void DisablePhoneSharing()
{
    broadcaster.StopBroadcasting();
    gps.StopLocationService();
}
```

#### `gps.StopLocationService()`

Останавливает Android location service. Используй, когда телефонное приложение больше не должно тратить батарею на геолокацию.

Для кнопки "остановить все" лучше вызывать после `broadcaster.StopBroadcasting()`.

#### `gps.SetDeviceId(deviceId)`

Задает логический идентификатор телефона.

Это полезно, если в одной сети может быть несколько телефонов. Например, телефон гида можно назвать `guide-phone`, а на шлеме поставить фильтр:

```csharp
gps.SetDeviceId("guide-phone");
receiver.SetAcceptedDeviceId("guide-phone");
```

Если `Device Id Override` не задан, используется `SystemInfo.deviceUniqueIdentifier`.

#### `gps.LocationUpdated`

Событие вызывается на телефоне, когда Android location service дал новую координату.

Используй для UI:

- показать широту/долготу;
- показать точность;
- показать, что GPS уже работает;
- диагностировать, что проблема не в GPS, а в сети.

```csharp
gps.LocationUpdated += sample =>
{
    statusText.text = "GPS: " + sample.HorizontalAccuracyMeters + " m";
};
```

Это событие не означает, что пакет дошел до Pico. Оно означает только, что телефон получил координату.

#### `broadcaster.PacketSent`

Событие вызывается после успешной отправки UDP-пакета из телефона.

Используй для отладки или UI-индикатора "данные отправляются". Это не подтверждение доставки до Pico, потому что UDP не имеет acknowledge.

```csharp
broadcaster.PacketSent += sample =>
{
    lastSentText.text = "Sent seq " + sample.Sequence;
};
```

#### `gps.Failed` и `broadcaster.Failed`

События ошибок.

`gps.Failed` обычно означает проблему с разрешением, выключенной геолокацией или невозможностью стартовать `Input.location`.

`broadcaster.Failed` обычно означает проблему с адресом, DNS/IP или созданием UDP socket.

```csharp
gps.Failed += ShowError;
broadcaster.Failed += ShowError;
```

### API сцены шлема

На шлеме публичной точкой управления являются `PhoneLocationUdpReceiver` и, если используется Яндекс, `PhoneLocationYandexNavigatorBridge`.

#### `receiver.StartListening()`

Запускает постоянный прием координат от телефона.

Что происходит внутри:

1. Создается UDP socket на `Listen Port`.
2. Запускается background thread `ReceiveLoop()`.
3. Поток постоянно ждет входящие UDP-пакеты.
4. Принятые JSON-пакеты складываются в очередь.
5. В `Update()` Unity-поток разбирает очередь и вызывает `LocationReceived`.

Меню шлема должно вызывать этот метод один раз при нажатии "начать получать координаты".

```csharp
public void EnablePhoneTracking()
{
    receiver.StartListening();
}
```

Если `Start On Enable` включен в инспекторе, прием стартует автоматически при включении объекта.

#### `receiver.StopListening()`

Останавливает прием: закрывает UDP socket и завершает background thread.

Используй при выходе из режима Яндекс-навигации или при отключении связи с телефоном.

```csharp
public void DisablePhoneTracking()
{
    receiver.StopListening();
}
```

#### `receiver.SetAcceptedDeviceId(deviceId)`

Фильтрует пакеты по `DeviceId`.

Нужно, если рядом может быть несколько телефонов с таким же приложением. Если фильтр пустой, шлем принимает координаты от любого телефона, который шлет пакет правильного формата на нужный порт.

Вызывать лучше до `StartListening()`:

```csharp
receiver.SetAcceptedDeviceId("guide-phone");
receiver.StartListening();
```

#### `receiver.TryGetLatestCoordinate(out coordinate)`

Возвращает последнюю принятую координату телефона в формате `GeoCoordinate`.

Используй, когда другой код сам хочет забрать текущую позицию человека, например при построении маршрута:

```csharp
if (receiver.TryGetLatestCoordinate(out var coordinate))
{
    navigator.SetCurrentCoordinate(coordinate);
    navigator.ShowRouteTo(destination);
}
```

Метод не запускает прием и не ждет новый пакет. Он только читает последнее уже принятое значение.

#### `receiver.LocationReceived`

Главное событие на стороне шлема. Вызывается каждый раз, когда пришла новая валидная координата телефона.

Используй для:

- обновления UI "телефон подключен";
- отображения точности GPS;
- ручной передачи координаты в свою систему;
- логирования remote endpoint;
- запуска логики, которая зависит от обновления позиции.

```csharp
receiver.LocationReceived += (sample, remote) =>
{
    phoneStatus.text = "Phone GPS " + sample.HorizontalAccuracyMeters + " m";
    Debug.Log("Phone packet from " + remote);
};
```

Если стоит `PhoneLocationYandexNavigatorBridge` с `Apply Every Received Location`, вручную вызывать `navigator.SetCurrentCoordinate(...)` в этом событии уже не нужно.

#### `receiver.HasLocation`

Показывает, была ли принята хотя бы одна валидная координата.

Подходит для UI:

```csharp
connectButton.interactable = receiver.IsListening;
routeButton.interactable = receiver.HasLocation;
```

#### `receiver.IsLatestLocationFresh`

Показывает, не устарела ли последняя координата. Порог задается полем `Stale After Seconds`.

Используй, чтобы не строить маршрут по старой позиции, если телефон давно не присылал данные:

```csharp
if (!receiver.IsLatestLocationFresh)
{
    ShowWarning("Нет свежей координаты телефона");
    return;
}
```

#### `bridge.ApplyLatestLocation()`

Разово берет последнюю координату из receiver и записывает ее в `YandexHelmetNavigator`.

Нужно, если `Apply Every Received Location` выключен и ты хочешь обновлять Яндекс-координату только в конкретный момент, например перед построением маршрута:

```csharp
if (bridge.ApplyLatestLocation())
{
    navigator.ShowRouteTo(destination);
}
```

#### `PhoneLocationYandexNavigatorBridge / Apply Every Received Location`

Если включено, bridge сам обновляет `YandexHelmetNavigator.CurrentCoordinate` при каждом входящем пакете.

Это основной режим для постоянной Яндекс-навигации:

1. `receiver.StartListening()`.
2. Телефон постоянно шлет координаты.
3. `receiver.LocationReceived` срабатывает на каждом пакете.
4. bridge вызывает `navigator.SetCurrentCoordinate(...)`.
5. `navigator.ShowRouteTo(...)` использует актуальную координату телефона.

### Что не нужно дергать из меню

`broadcaster.SendNow()` и `broadcaster.Send(sample)` существуют как низкоуровневые/отладочные методы. Для обычного пользовательского сценария они не нужны.

Правильный внешний цикл:

```csharp
// Телефон: один раз включили.
broadcaster.SetTarget(picoIp, 47777);
broadcaster.StartBroadcasting();

// Шлем: один раз включили.
receiver.StartListening();

// Дальше координаты идут сами, пока не вызваны Stop-методы.
```

Если приходится вручную вызывать `SendNow()` каждый кадр, значит API используется неправильно. Это уже делает `PhoneLocationUdpBroadcaster.Update()`.

### `PhoneGpsLocationProvider`

Ставится на телефонную сцену. Запрашивает разрешение геолокации и читает `Input.location`.

Публичное API:

```csharp
gps.StartLocationService();
gps.StopLocationService();
gps.SetDeviceId("main-phone");

gps.LocationUpdated += sample =>
{
    Debug.Log(sample.Latitude + ", " + sample.Longitude);
};

gps.Failed += error =>
{
    Debug.LogWarning(error);
};
```

Ключевые поля в инспекторе:

- `Start On Enable` - запускать GPS автоматически.
- `Desired Accuracy Meters` - желаемая точность. Для навигации обычно `5`.
- `Update Distance Meters` - минимальное смещение для обновления. Для человека обычно `1`.
- `Device Id Override` - можно задать понятное имя телефона, например `guide-phone`.

### `PhoneLocationUdpBroadcaster`

Ставится на телефонную сцену рядом с `PhoneGpsLocationProvider`. Отправляет последний GPS sample по UDP.

Публичное API:

```csharp
broadcaster.SetTarget("255.255.255.255", 47777);
broadcaster.StartBroadcasting();
broadcaster.StopBroadcasting();
broadcaster.SendNow();

broadcaster.PacketSent += sample =>
{
    Debug.Log("Sent " + sample);
};

broadcaster.Failed += error =>
{
    Debug.LogWarning(error);
};
```

Ключевые поля:

- `Target Address` - `255.255.255.255` для broadcast или IP Pico для прямой отправки.
- `Target Port` - порт, по умолчанию `47777`.
- `Allow Broadcast` - включить для `255.255.255.255`.
- `Send Interval Seconds` - частота отправки. Для ходьбы достаточно `1`.

### `PhoneLocationUdpReceiver`

Ставится на сцену шлема. Слушает UDP и хранит последнюю координату телефона.

Публичное API:

```csharp
receiver.StartListening();
receiver.StopListening();
receiver.SetAcceptedDeviceId("guide-phone");

receiver.LocationReceived += (sample, remote) =>
{
    Debug.Log("Phone: " + sample + " from " + remote);
};

if (receiver.TryGetLatestCoordinate(out var coordinate))
{
    navigator.SetCurrentCoordinate(coordinate);
}
```

Ключевые поля:

- `Listen Port` - должен совпадать с `Target Port` на телефоне.
- `Accepted Device Id` - опциональный фильтр, если в сети несколько телефонов.
- `Stale After Seconds` - через сколько секунд координата считается устаревшей.

### `PhoneLocationYandexNavigatorBridge`

Ставится на сцену шлема, если нужно автоматически прокидывать координату телефона в `YandexHelmetNavigator`.

```csharp
bridge.ApplyLatestLocation();
```

Если `Apply Every Received Location` включен, bridge сам вызывает:

```csharp
navigator.SetCurrentCoordinate(new GeoCoordinate(sample.Latitude, sample.Longitude));
```

### `PhoneLocationDebugLogger`

Отдельный debug-компонент для ADB/logcat. Его можно повесить на тот же объект, где стоят `PhoneGpsLocationProvider`, `PhoneLocationUdpBroadcaster`, `PhoneLocationUdpReceiver` или `PhoneLocationYandexNavigatorBridge`.

Он пишет через `Debug.Log`/`Debug.LogWarning`, поэтому на Android это видно в `adb logcat`.

Что логирует:

- старт/выключение логгера;
- периодический статус GPS, broadcaster, receiver и bridge;
- каждое GPS-обновление телефона;
- каждую UDP-отправку;
- каждый UDP-прием на шлеме;
- ошибки GPS/UDP;
- последнюю известную координату.

Минимальная настройка:

1. Добавь `PhoneLocationDebugLogger` на объект с location-компонентами.
2. Оставь `Log Periodic Status` включенным.
3. Собери APK.
4. Смотри логи:

```bash
adb logcat -s Unity
```

Или фильтр по тегу сообщения:

```bash
adb logcat | findstr PhoneLocation
```

На телефоне полезны `Log Gps Updates` и `Log Sent Packets`. На шлеме полезны `Log Received Packets` и `Log Periodic Status`.

## Быстрый сетап телефонной сцены

1. Создай отдельную Android-сцену для телефона.
2. Добавь пустой объект `Phone Location`.
3. Повесь на него:
   - `PhoneGpsLocationProvider`
   - `PhoneLocationUdpBroadcaster`
4. В `PhoneLocationUdpBroadcaster/Target Address` сначала оставь `255.255.255.255`.
5. `Target Port` оставь `47777`.
6. Собери APK на телефон.
7. На телефоне дай разрешение геолокации.

Если у тебя уже есть меню, кнопки можно привязать так:

```csharp
public void StartSharing()
{
    gps.StartLocationService();
    broadcaster.StartBroadcasting();
}

public void StopSharing()
{
    broadcaster.StopBroadcasting();
    gps.StopLocationService();
}

public void SetPicoIp(string ip)
{
    broadcaster.SetTarget(ip, 47777);
}
```

## Быстрый сетап сцены шлема

1. На сцене Pico добавь объект `Phone Location Receiver`.
2. Повесь на него `PhoneLocationUdpReceiver`.
3. Если используется Яндекс-навигация, добавь `PhoneLocationYandexNavigatorBridge`.
4. В bridge назначь:
   - `Receiver`
   - `Navigator`
5. `Listen Port` оставь `47777`.
6. Собери APK на Pico.

Пример ручного подключения к твоему меню:

```csharp
public void StartReceiving()
{
    receiver.StartListening();
}

public void StopReceiving()
{
    receiver.StopListening();
}

public bool TryGetPhoneCoordinate(out GeoCoordinate coordinate)
{
    return receiver.TryGetLatestCoordinate(out coordinate);
}
```

## Проверка, что связь работает

1. Телефон и Pico в одной сети.
2. На телефоне включена геолокация Android.
3. Приложению на телефоне выдано location permission.
4. `PhoneGpsLocationProvider.Status` стал `Running`.
5. `PhoneLocationUdpBroadcaster.IsBroadcasting` равен `true`.
6. На Pico `PhoneLocationUdpReceiver.IsListening` равен `true`.
7. На Pico начал вызываться `LocationReceived`.

Если `LocationReceived` не вызывается:

- Попробуй вместо `255.255.255.255` указать прямой IP Pico.
- Проверь, что `Target Port` и `Listen Port` одинаковые.
- Проверь, что телефон и Pico реально в одной сети.
- Не используй публичный Wi-Fi с client isolation.
- Перезапусти hotspot телефона после подключения Pico.

## Разрешения Android

В `Assets/Plugins/Android/AndroidManifest.xml` добавлены:

```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
```

`INTERNET` нужен для UDP-сокетов. `ACCESS_FINE_LOCATION` и `ACCESS_COARSE_LOCATION` нужны телефонной сборке для GPS.

На Pico location permission сам по себе GPS не добавит. Он нужен только потому, что один и тот же проект может собираться и на телефон.

## Ограничения

- Координата телефона равна координате человека только если телефон находится у человека.
- Точность зависит от Android location provider, качества GPS, зданий и режима энергосбережения.
- В помещении GPS может прыгать или не работать.
- UDP не гарантирует доставку каждого пакета, но для координат это нормально.
- Broadcast может быть заблокирован сетью или hotspot-режимом. В этом случае используй прямой IP Pico.
- Это не синхронизация ориентации человека. Передается только геопозиция, скорость и примерный course по изменению координат.
