# Unity: імпорт, кеш та збереження

Спільний кеш Mesh/FragmentMeshData вже реалізовано; registry `asset_id`, JSON importer, Blender exporter та shared material cache лишаються планом під [контракт передачі](TransferFormat.md). Поточне використання й перевірки: [SharedMeshCache.md](../../Assets/SurfaceFragments/SharedMeshCache.md). Базова конфігурація проєкту: Unity 6000.3.10f1, URP 17.3.0. Нові dependencies для цього пайплайна не передбачені.

## Наявні точки інтеграції

- `Assets/SurfaceFragments/FragmentIslandAuthoring.cs`: source-параметри, footprint, ports, linked bake record.
- `Assets/SurfaceFragments/Editor/FragmentIslandBaker.cs`: обхід MeshFilter + MeshRenderer, нарізка, матеріали, візуальна копія й збереження.
- `Assets/SurfaceFragments/FragmentIslandBakeRecord.cs`: зв’язок source/output та поточні owned assets.
- `Assets/SurfaceFragments/Editor/FragmentMeshCache.cs` і `FragmentMeshCacheLibrary.cs`: staging, fingerprint, спільне збереження та editor-only metadata кешу Mesh/даних.
- `Assets/SurfaceFragmentPrototype/ConvexCellFragmentBuilder.cs`: файл із класом `CellFragmentBuilder`, який зараз виконує нарізку. Назва файлу не означає вимогу convex-only.
- `FragmentMeshData`: Mesh, центри фрагментів, maximum radius і revision.
- `FragmentRevealRenderer` та `FragmentRevealGroup`: runtime-ефект, спільний діапазон і clock.
- `FragmentIslandPorts`, `FragmentIslandMapGenerator`, `FragmentIslandMap`: стикування та сусідній reveal.

Поточний baker зберігає словник за Mesh у межах виклику та перевіряє persistent shared library між викликами. У папці острова лишаються prefab, bake record, локальні матеріали та legacy outputs. Одиночний bake працює через ті самі наявні кнопки.

## Бібліотека source assets

Майбутній registry зіставляє стабільний `asset_id` із канонічним model/prefab у Unity. Source prefab острова містить екземпляри цих assets; Unity підтримує [вкладені prefab](https://docs.unity.com/en-us/engine/6000.3/manual/working-with-gameobjects/prefabs/nested).

Імпорт нового ID створює бібліотечний запис. Повторний імпорт того самого ID оновлює відомий запис, зі збереженням `.meta`/GUID. Конфлікт між різними ресурсами з одним ID потрібно показати явно. Перейменування display name не створює новий ресурс.

Області збереження: FragmentLibrary створюється автоматично під час успішного bake; canonical registry та структура імпорту ще плануються.

```text
Assets/IslandLibrary/                 canonical imported models and source prefabs
Assets/Generated/FragmentLibrary/    shared generated meshes, data and materials
Assets/Islands/<island_id>/          source composition, import link and island output
```

Існуючі islands і source assets не потрібно перейменовувати чи переносити лише заради цієї структури. Registry може посилатися на їхні фактичні шляхи.

## Спільний кеш нарізки

Одиниця повторного використання — канонічний Mesh та параметри його геометричного перетворення. `asset_id` зручно знаходить модель, але сам по собі не доводить актуальність кешу.

Геометричний запис має враховувати:

- Unity GUID/local file ID вихідного Mesh;
- dependency/content fingerprint імпортованого Mesh, включно з UV, normals, vertex colors та індексами;
- Cell Size у локальному просторі Mesh;
- явну версію алгоритму та інші реальні параметри нарізки, якщо вони з’являться.

Позиція острова, instance transform, ports, footprint, progress, duration, scatter і chaos не входять до геометричного ключа поточного алгоритму. Нарізка зараз виконується з identity matrix. Зміна scale екземпляра масштабує вже нарізану геометрію; окреме запікання однакового світового розміру уламків не обіцяється.

Розділи стабільну ідентичність cache slot та fingerprint його актуального вмісту. Зміна Mesh перебудовує цей slot зі збереженням посилань на generated assets і збільшенням data revision. Інший Cell Size утворює окремий варіант, а не перезаписує геометрію, яку використовують острови з попереднім Cell Size.

Матеріали зараз зберігаються локально для кожного острова. Майбутній shared material cache має враховувати вихідний material, підтримувані властивості, залежності, версію shader-конвертації та потрібний fallback. Зміна tint/texture вже не дублює незмінену нарізану геометрію. Якщо майбутній алгоритм почне запікати ці властивості саме у vertices, відповідні залежності мають увійти до геометричного fingerprint.

Ownership уже розділено для геометрії: shared library володіє Mesh/даними, `FragmentIslandBakeRecord.SharedGeometry` містить перевірені shared references, а старий `Geometry` зберігає legacy owned outputs. `ValidateRecordAssets` досі перевіряє власність legacy assets і локальних матеріалів; cache перевіряє спільні посилання окремо. Старі локальні meshes не видаляються й не оновлюються після переходу renderers на кеш. Не додавай shared assets у legacy owned bindings.

Видалення екземпляра або острова не видаляє бібліотечну модель чи кеш, потрібний іншим островам. Автоматичне очищення невикористаних ресурсів не потрібне для першої версії.

## Імпорт композиції

1. Прочитай і перевір пакет цілком: format/version, paths, ID, parents, transforms, models, footprint і ports. Не запускай bake з частково зібраним островом.
2. Виріши assets через registry. Новий/змінений payload імпортується один раз; незмінений використовує наявний asset.
3. Знайди або створи source island за `island_id`. Збережи зв’язок із package та відповідність `instance_id` → source object.
4. Онови керовані nodes за стабільними ID, зберігаючи Unity-ідентичність. Не видаляй і не створюй заново весь source prefab на кожному імпорті: baker спирається на persistent object IDs.
5. Застосуй повний список nodes. Видали лише відсутні importer-owned nodes цього острова; незнайомі ручні додавання покажи як конфлікт або збережи поза керованою частиною. Не видаляй їх мовчки.
6. Запиши Cell Size, footprint і custom ports на authoring. Повторний імпорт зберігає наявні reveal settings, output link та інші Unity-налаштування, які пакет не визначає.
7. Збережи source, потім згенеруй/онови пов’язаний fragment prefab через shared cache. Вивід зберігає окрему ієрархію renderers і один кореневий reveal group.
8. Поверни фактичні шляхи source/output, число reused/rebuilt meshes та зрозумілий результат. Додавання до конкретного map catalog має бути явним вибором; не редагуй усі сцени/генератори автоматично.

Перед заміною результату збери й перевір його у staging. Звичайні Unity asset writes не є атомарною filesystem transaction; не обіцяй повне відновлення після будь-якого збою диска. Помилки підготовки мають залишати останній працездатний prefab доступним.

## Черговість реалізації

1. **Shared bake cache — реалізовано для геометрії.** Два вручну зібрані острови з тим самим Mesh та Cell Size використовують спільні Mesh/дані. Потрібні ручні Unity-перевірки. Canonical registry за `asset_id` і shared materials ще не реалізовані; registry потрібен перед автоматичним імпортом.
2. **Unity importer JSON.** Стабільні source instances, пов’язані outputs, оновлення композиції й метаданих без зайвої нарізки.
3. **Blender exporter.** Реалізувати контракт, перевірити одиниці/осі на малому асиметричному прикладі й налагодити повторний експорт.
4. **Повний прохід.** Опис користувача → бібліотечні assets → Blender-композиція → пакет → source/output prefab. Надати явну можливість додати результат до Templates.

Кожен крок — окрема сфокусована зміна. Цей документ не дозволяє автоматично реалізувати їх усі чи змінювати serialized assets без потрібної авторизації.

## Критерії приймання та ручні перевірки

- Два острови й кілька дерев одного виду: одна canonical source geometry і один generated geometry slot за однакових параметрів. Перевіряти GUID/посилання, а не лише назви файлів.
- Повторний імпорт незміненого пакета не створює дублів і не нарізає meshes повторно.
- Перестановка, rename, reparent чи scale одного дерева не перебудовує його Mesh і зберігає `instance_id`/Unity object identity.
- Зміна одного canonical Mesh перебудовує його кеш і робить результат доступним усім його посиланням, із коректним revision. Не обіцяти довільний hot reload у вже зібраному Player.
- Зміна матеріалу оновлює матеріальний результат без дублювання геометрії, якщо геометричні вхідні дані не змінилися.
- Інший Cell Size створює другий geometry variant; попередній лишається придатним для своїх островів.
- Повторний імпорт source/пакета після правок зберігає output prefab GUID, linked record, ports, footprint і ручні reveal settings за визначеним контрактом.
- У готовому prefab всі fragment renderers з’являються синхронно через один group; shadows, hover/scatter і прихований/повністю видимий стан працюють як раніше.
- У генераторі перевірити різні форми, кілька ports на довгій стороні, Gap 0, overlap rejection та preview від кількох сусідів.
- Невалідні пакети, невідомі ID й конфлікти не прибирають робочий результат і не змінюють сторонні islands.
- Runtime prefab працює у Play Mode без Blender, JSON importer та runtime-нарізки. Перевірити також зібраний Player, коли користувач дозволить build.

Поки що ці пункти є планом перевірки майбутньої реалізації. Документація не засвідчує, що відповідні ручні перевірки виконані.
