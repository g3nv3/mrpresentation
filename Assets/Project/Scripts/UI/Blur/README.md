# UI background blur

Компоненты делают frosted glass/acrylic панели для world-space UGUI поверх RenderTexture с камеры Pico.

## Файлы

- `PicoCameraBackgroundBlur.cs` готовит размытую версию RenderTexture камеры.
- `FrostedGlassImage.cs` вешается на обычный `UnityEngine.UI.Image` и рисует внутри него размытый фон. По умолчанию UV считается через world projection RGB-камеры Pico: world position пикселя панели проецируется в camera texture через intrinsics/extrinsics.
- `Assets/Project/Prefabs/UI/Base-Panel.prefab` уже настроен как frosted glass панель.
- Шейдеры лежат в `Assets/Project/Shaders/UI/Blur`.
- Материалы `Assets/Project/Materials/Frosted Glass Image.mat` и `Assets/Project/Materials/Kawase Blur.mat` держат ссылки на шейдеры, чтобы Unity не выкинула их из player build.

## Как подключить

Основной путь:

1. В сцене должен быть `PicoCameraRenderTextureSource`.
2. На этом же объекте или рядом должен быть `Project/UI/Pico Camera Background Blur`.
3. У `PicoCameraBackgroundBlur`:
   - `Camera Source` укажите на `PicoCameraRenderTextureSource`, если auto-find не подходит.
   - `Blur Material` должен ссылаться на `Assets/Project/Materials/Kawase Blur.mat`.
   - `Full Resolution Output` оставьте включенным: blur все еще считается в уменьшенном размере, но наружу отдается мягко растянутая полноразмерная текстура без крупных квадратов.
   - `Set Global Texture` оставьте включенным.
4. Добавляйте панели через `Assets/Project/Prefabs/UI/Base-Panel.prefab`.
5. У prefab уже есть `FrostedGlassImage` и `Material Template = Assets/Project/Materials/Frosted Glass Image.mat`.
6. Для каждой панели обычно нужно настроить только размер, позицию, sprite/alpha форму и параметры `Tint`, `Opacity`, `Saturation`, `Brightness`.
7. Дефолт рассчитан на заметный blur-only вид: `Downsample = 3`, `Iterations = 5`, `Radius = 6`, `Opacity = 1`, `Tint alpha = 0`.
8. `Reference Raw Image` на `FrostedGlassImage` обычно оставляйте пустым. Если поставить туда маленький debug preview `RawImage`, панель будет сэмплить именно этот маленький прямоугольник, и картинка внутри панели станет мелкой/смещенной.
9. `Use World Camera Projection` оставьте включенным. Это тот же класс калибровки, что в QR: `CameraExtrinsicsInterpretation = CameraPoseRelativeToDevice`, `ProjectionModelMode = DeriveFromFrameFov`, `Invert Camera Space Forward = true`, `Flip Reported Camera Translation Z = true`.
10. Если после projection остается небольшой общий сдвиг, правьте `Projection Uv Offset`. Если картинка слишком крупная/мелкая, правьте `Projection Uv Scale`.
11. Если blur дергается из-за микроскачков проекции, оставьте `Smooth Projection` включенным. Увеличивайте `Projection Smoothing Time` для более мягкой картинки, уменьшайте его если появляется заметное запаздывание.

Ручной путь для новой UI-картинки:

1. Создайте обычный world-space `Image`.
2. Добавьте `Project/UI/Frosted Glass Image`.
3. В `Material Template` поставьте `Assets/Project/Materials/Frosted Glass Image.mat`.
4. В `Background Blur` можно поставить сценовый `PicoCameraBackgroundBlur`; если поле пустое, компонент попробует найти его сам.
5. Форма и alpha sprite у `Image` работают как маска панели.
6. Если фон отображается вверх ногами, включите `Flip Y` на `FrostedGlassImage`; он применяется и к обычному screen-space mapping, и к `Use World Camera Projection`.

## Диагностика

- Белая обычная UI-панель: чаще всего не применился `FrostedGlassImage` material. Проверьте `Material Template` и что shader `Project/UI/FrostedGlassImage` не missing.
- Черная/однотонная frosted панель: material работает, но нет кадра камеры. Проверьте permission камеры, инициализацию `PicoCameraRenderTextureSource` и что его `TargetTexture` обновляется.
- Картинка мелкая, смещенная или в панели виден debug preview: проверьте, что `Reference Raw Image` у `FrostedGlassImage` пустой. Это поле нужно только для специальных случаев, когда camera feed реально рисуется отдельным fullscreen `RawImage`.
- Картинка явно больше/меньше или вся сдвинута относительно passthrough: сначала проверьте, что `Use World Camera Projection` включен. После этого используйте `Projection Uv Scale/Offset` для небольшой докалибровки.
- Картинка совпадает в среднем, но мелко дрожит: увеличьте `Projection Smoothing Time` на `FrostedGlassImage`. Если после recenter/потери трекинга картинка долго догоняет положение, уменьшите `Projection Jump Reset Distance/Angle`.
- Blur выглядит квадратным/пиксельным: проверьте, что `Full Resolution Output` включен. Если квадраты все еще заметны, уменьшите `Downsample` или `Radius`; слишком большой радиус на маленькой camera texture быстро проявляет сетку.
- Нет blur, но видно картинку: проверьте `Blur Material` у `PicoCameraBackgroundBlur` и значения `Downsample`, `Iterations`, `Radius`.
- Важно: это не настоящий blur системного passthrough-слоя Pico. Панель рисует размытую RGB camera RenderTexture внутри своей формы. Поэтому дефолт сделан без прозрачности: виден только blurred camera feed, а не смешивание с системным passthrough под UI.

Базовый цвет акрилика по умолчанию: `#F4F4F0`.
