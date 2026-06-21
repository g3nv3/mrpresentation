# Location Bridge (Pico receiver)

Этот каталог содержит только сторону Pico для приема координат и назначения маршрута от Android-приложения.

Телефонный GPS-провайдер и UDP-отправитель находятся в отдельном проекте:

`D:\Unity_Projects\phone-pico-provider`

## Поток координат

1. Телефон получает геопозицию и отправляет `PhoneLocationPacket` по UDP.
2. `PhoneLocationUdpReceiver` слушает порт `47777` и публикует `LocationReceived`.
3. `PhoneLocationYandexNavigatorBridge` обновляет текущую координату `YandexHelmetNavigator`.
4. При активном маршруте bridge ограниченно перестраивает его после фактического перемещения.

`PhoneLocationSample` и `PhoneLocationPacket` остаются в этом проекте как контракт входящего пакета.

## Поток назначения

1. Телефон отправляет `PhoneRouteDestinationPacket` по UDP.
2. `PhoneRouteDestinationUdpReceiver` слушает порт `47778`.
3. `PhoneRouteDestinationYandexNavigatorBridge` передает назначение в `YandexHelmetNavigator`.
4. `PhoneRouteDestinationLaunchUi` запускает, перестраивает или останавливает маршрут.

## Настройка VRScene

На объекте приема должны быть назначены:

- `PhoneLocationUdpReceiver`;
- `PhoneLocationYandexNavigatorBridge`;
- `PhoneRouteDestinationUdpReceiver`;
- `PhoneRouteDestinationYandexNavigatorBridge`;
- при необходимости `PhoneLocationDebugLogger`.

Телефон и Pico должны находиться в одной сети. Порты отправителя должны совпадать с портами ресиверов: `47777` для координат и `47778` для назначения.

## Диагностика

`PhoneLocationDebugLogger` логирует состояние ресивера, принятые координаты и ошибки UDP. Логи Android/Pico можно смотреть через:

```text
adb logcat -s Unity
```

В этом проекте не должно быть `PhoneGpsLocationProvider`, `PhoneLocationUdpBroadcaster` или телефонной сцены.
