using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Новая сцена, собираемая слоями. Слой первый: земля, навигация, небо.
    ///
    /// Зачем вообще новая. Старая песочница набрала слоями годовой мусор:
    /// руины, помосты витрин, восемьдесят персонажей, деревня, край
    /// мира, гиганты и остатки экспериментов. Чинить что-либо внутри неё
    /// нельзя: каждая проверка идёт в окружении, где сломано ещё пять вещей,
    /// и любой диагноз загрязнён. Именно так вышло за одну ночь — правил
    /// деревья, ломалось небо; правил небо, гас свет; возвращал свет,
    /// отваливались тени.
    ///
    /// Поэтому сцена строится с нуля и ПО СЛОЯМ, и следующий слой не
    /// добавляется, пока предыдущий не прошёл щуп.
    ///
    /// <b>Слой 1 (этот):</b> земля, навигация, небо, свет и голый персонаж-
    /// капсула. Капсула здесь не заглушка, а инструмент: если по всей карте
    /// проходит она, значит проходить будет и герой, и проверяется при этом
    /// ровно земля с навигацией, без вклада моделей и анимаций.
    ///
    /// Слой 2 — настоящий герой с анимациями. Слой 3 — один моб и бой.
    /// Слой 4 — декор биома. Слой 5 — НПС и квесты.
    /// </summary>
    public static class ArenaBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Arena.unity";

        /// <summary>Размер игровой площадки, метров. По нему печётся навигация.</summary>
        private const float Size = 160f;

        /// <summary>
        /// Насколько далеко тянется ВИДИМАЯ земля, метров.
        ///
        /// Сильно больше игровой площадки, и это не расточительность. Земля,
        /// кончающаяся ровно там, где кончается ходьба, показывает свой край:
        /// на горизонте появляется светлая полоска, за которой сразу небо.
        /// Читается как дыра в мире.
        ///
        /// Лист в шестьсот метров уходит далеко за предел видимости и тонет в
        /// дымке. Коллайдера у него нет, поэтому ни ходить по нему, ни печь
        /// навигацию с него нельзя — игровая площадка остаётся прежней.
        /// </summary>
        private const float VisualSize = 600f;

        private const string GrassTile =
            "Assets/Synty/PolygonNatureBiomes/PNB_Enchanted_Forest/Prefabs/SM_Env_Ground_Grass_Flat_01.prefab";

        [MenuItem("Tools/IsoRPG/Арена: собрать слой 1 (земля, навигация, небо)", priority = 2)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play сцены не сохраняются.", "Понятно");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Lighting();
            var ground = Ground();
            var player = Player();
            Camera(player.transform);
            Events();

            // Небо — после камеры: купол ищет главную камеру, чтобы за ней
            // ездить.
            SkyBuilder.Apply();

            // Сцену сохраняем ДО выпечки навигации, и это не мелочь порядка.
            //
            // NavMeshSurface хранит готовую сетку отдельным файлом РЯДОМ с
            // файлом сцены. Пока сцена не сохранена, у неё нет пути — и
            // писать сетку некуда. Выпечка при этом не ругается: она честно
            // отрабатывает и молча не оставляет ничего. В игре это выглядит
            // как «все стоят на месте», а щуп показывает ноль проходимых
            // клеток из тысячи.
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            NavBake.Rebake();

            // И ещё раз: выпечка меняет сцену, а несохранённое в пакетном
            // режиме теряется вместе с процессом.
            EditorSceneManager.SaveScene(scene, ScenePath);

            Register();

            Debug.Log("[IsoRPG] Арена, слой 1 собран: земля " + Size + "x" + Size +
                      " м, навигация по коллайдерам, небо-купол, персонаж-капсула. " +
                      "Проверять заданием «arena-probe».");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Свет — теми же числами, что были в старой сцене до всех правок.
        ///
        /// Они не случайные: закатное солнце под 21 градусом даёт длинные
        /// тени, а тёплый оттенок с холодным рассеянным светом — тот самый
        /// перепад, на котором держится объём в плоском лоу-поли.
        ///
        /// Рассеянный свет НЕ берём от неба. Эта связка уже гасила сцену
        /// целиком: небо сломалось, и вместе с ним ушёл весь свет, а следом
        /// тени.
        /// </summary>
        private static void Lighting()
        {
            var go = new GameObject("Sun", typeof(Light));
            var light = go.GetComponent<Light>();

            light.type = LightType.Directional;
            light.color = new Color(1f, 0.788f, 0.541f);
            light.intensity = 0.95f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.82f;

            go.transform.rotation = Quaternion.Euler(21f, 152f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.353f, 0.455f, 0.651f);
            RenderSettings.ambientEquatorColor = new Color(0.384f, 0.388f, 0.439f);
            RenderSettings.ambientGroundColor = new Color(0.200f, 0.188f, 0.180f);
            RenderSettings.ambientIntensity = 1f;

            // Туман прячет край мира.
            //
            // Земля тянется на шестьсот метров, а видно её метров на сто
            // двадцать: дальше она полностью тонет в дымке, и горизонт
            // становится мягким переходом, а не линией обрыва. Цвет дымки
            // подтянут к низу неба — иначе на стыке появляется ступенька.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 125f;
        }

        /// <summary>
        /// Земля: плитки биома плюс сплошной пол-страховка под ними.
        ///
        /// Страховка — не перестраховка, а ответ на конкретный баг: вчера
        /// монстры проваливались под землю. Причина была ровно в том, что
        /// пол состоял из отдельных объектов, часть из них я снёс, и под
        /// существами не осталось ничего. Один большой коллайдер на всю
        /// площадку делает такое невозможным по построению: что бы ни
        /// случилось с плитками, провалиться некуда.
        ///
        /// Коллайдер невидимый и лежит на волос ниже плиток — иначе две
        /// поверхности на одной высоте мерцают полосами, споря за пиксели.
        /// </summary>
        private static GameObject Ground()
        {
            var root = new GameObject("Ground");

            var floor = root.AddComponent<BoxCollider>();
            floor.center = new Vector3(0f, -0.05f, 0f);
            floor.size = new Vector3(Size, 0.1f, Size);

            var sheet = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sheet.name = "GroundSheet";
            sheet.transform.SetParent(root.transform, false);
            sheet.transform.position = new Vector3(0f, -0.03f, 0f);

            // Примитив Plane — десять метров на сторону, отсюда деление.
            sheet.transform.localScale = new Vector3(VisualSize / 10f, 1f, VisualSize / 10f);

            Object.DestroyImmediate(sheet.GetComponent<Collider>());

            // Материал пересоздаём каждый раз, иначе повторная сборка падает
            // на «ассет уже существует» и сцена остаётся с половиной земли.
            const string paintPath = "Assets/_Game/Art/Materials/GroundSheet.mat";

            Directory.CreateDirectory("Assets/_Game/Art/Materials");
            AssetDatabase.DeleteAsset(paintPath);

            var paint = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            // Луговой зелёный, приглушённый. Блеск в ноль: трава не блестит,
            // а на большой плоскости любой блик читается как мокрое пятно.
            paint.color = new Color(0.34f, 0.44f, 0.22f);
            paint.SetFloat("_Smoothness", 0f);

            AssetDatabase.CreateAsset(paint, paintPath);
            sheet.GetComponent<Renderer>().sharedMaterial = paint;

            // Плиток биома здесь нет намеренно.
            //
            // Первый заход мостил поле тысячей плиток Enchanted Forest, и
            // вышло две беды сразу. Стыки: у соседних плиток кромки разной
            // формы, между ними остаются щели в волос, и сквозь них видно
            // небо — на экране это белые прорези по всему полю. И цвет: у
            // этого биома «трава» тёмно-коричневая, это лесная подстилка, а
            // не луг. Зелёного в нём просто нет.
            //
            // Базовая земля — один лист. Ни швов, ни тысячи объектов в
            // сцене. Плитки и подстилка вернутся отдельным слоем как декор,
            // когда дойдём до вида, а не до механики.

            Debug.Log("[IsoRPG] Земля: сплошной лист " + Size + "x" + Size +
                      " м, один объект. Плитки биома — отдельным слоем позже.");

            return root;
        }

        /// <summary>Ищет префаб по имени: пути у наборов разной глубины.</summary>
        private static GameObject FindPrefab(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (Path.GetFileNameWithoutExtension(path) == name)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        private static float TileSize(GameObject tile)
        {
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(tile);
            probe.transform.position = new Vector3(0f, 5000f, 0f);

            var renderers = probe.GetComponentsInChildren<Renderer>()
                                 .Where(r => !(r is ParticleSystemRenderer))
                                 .ToArray();

            float size = 0f;

            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                size = Mathf.Max(bounds.size.x, bounds.size.z);
            }

            Object.DestroyImmediate(probe);

            return size;
        }

        /// <summary>
        /// Модель героя. Разбойник Synty — тот же производитель, что у клипов.
        ///
        /// Здесь стоял разбойник из Low-Poly Medieval Heroes, и походка на нём
        /// выглядела вывернутой. Риг у него честный Humanoid, дело не в этом:
        /// у набора попросту НЕТ своих анимаций ходьбы, только статичные позы
        /// для витрины. Значит любая походка надевается ретаргетом, а
        /// пропорции у двух производителей разные — Unity растягивает позу под
        /// чужие длины костей.
        ///
        /// Правило, оплаченное дважды за сутки: <b>модель и клипы берём у
        /// одного производителя.</b> Ретаргет между наборами разной анатомии —
        /// крайняя мера, а не приём. Synty-персонаж под Synty-локомоцию не
        /// ретаргетится вовсе: скелет и пропорции те же самые.
        /// </summary>
        private const string HeroChoice = "Assets/_Game/Scenes/hero-number.txt";

        /// <summary>
        /// Кого ставить героем. Номер тот же, что у задания «hero-num».
        ///
        /// Здесь стояло имя разбойника `SM_Chr_Male_Rouge_01`, и два кода
        /// спорили за одного героя: строитель ставил разбойника, задание
        /// «hero-num» меняло его на выбранного по номеру. В сцене оставался
        /// тот, кто прогнался последним — поэтому разбойник возвращался
        /// после каждой перестройки арены, сколько его ни удаляй. Удаление
        /// лечило след, а не причину. Теперь источник номера один на обоих.
        /// </summary>
        private static string HeroModel
        {
            get
            {
                string choice = File.Exists(HeroChoice)
                    ? File.ReadAllText(HeroChoice).Trim()
                    : "23";

                if (choice.Length == 0) choice = "23";

                // Номер — это пресет Polygon, всё остальное — имя префаба
                // как есть: героев стало два набора, а строка выбора одна.
                return int.TryParse(choice, out int number)
                    ? "Chr_FantasyHero_Preset_" + number
                    : choice;
            }
        }

        /// <summary>
        /// Персонаж: модель, походка, прыжок. Слой второй.
        ///
        /// Капсула первого слоя сделала своё дело — доказала, что земля и
        /// навигация целые. Теперь на её место встаёт герой, и всё, что
        /// сломается дальше, будет про НЕГО: скелет, аниматор, скорости. Так
        /// и надо: каждый слой добавляет ровно один источник ошибок.
        ///
        /// Боя здесь ещё нет. Он третий слой, вместе с мобом.
        /// </summary>
        private static GameObject Player()
        {
            var model = FindPrefab(HeroModel);
            GameObject go;

            if (model == null)
            {
                Debug.LogWarning("[IsoRPG] Модель " + HeroModel +
                                 " не нашлась — герой остался капсулой.");

                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            else
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(model);

                var animator = go.GetComponentInChildren<Animator>();
                if (animator == null) animator = go.AddComponent<Animator>();

                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/_Game/Art/Animations/Controllers/AC_Rogue.controller");

                if (controller == null)
                    Debug.LogError("[IsoRPG] Нет контроллера AC_Rogue — герой встанет столбом. " +
                                   "Сначала задание «combat-anims».");
                else
                    animator.runtimeAnimatorController = controller;

                // Позицию ведёт навигационный агент. С корневым движением
                // анимация тянет персонажа сама, и он уезжает от агента.
                animator.applyRootMotion = false;

                // Скелет проверяем здесь, а не в игре: humanoid-клип на
                // не-humanoid скелете не играет вовсе, и персонаж застывает
                // буквой «Т» без единой строчки в журнале.
                if (animator.avatar == null || !animator.avatar.isHuman)
                    Debug.LogError("[IsoRPG] " + HeroModel + " не Humanoid — " +
                                   "анимации на него не лягут.");

                if (go.GetComponent<IsoRPG.Player.CharacterAnimatorDriver>() == null)
                    go.AddComponent<IsoRPG.Player.CharacterAnimatorDriver>();

                if (go.GetComponent<IsoRPG.Player.JumpGesture>() == null)
                    go.AddComponent<IsoRPG.Player.JumpGesture>();
            }

            go.name = "Player";
            go.tag = "Player";
            go.transform.position = new Vector3(0f, 0f, 0f);

            // Берём существующий, если он есть.
            //
            // У покупных префабов часто уже висят компоненты от их
            // демо-сцены: у нашего героя это навигационный агент. Повторный
            // AddComponent на такой объект бросает исключение, и сборка
            // обрывается на середине — сцена остаётся без неба и навигации,
            // а выглядит это как «ничего не собралось».
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent == null) agent = go.AddComponent<NavMeshAgent>();

            agent.speed = 5.5f;
            agent.angularSpeed = 720f;
            agent.acceleration = 30f;
            agent.stoppingDistance = 0.1f;
            agent.radius = 0.35f;
            agent.height = 1.9f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

            if (go.GetComponent<IsoRPG.Player.KeyboardMove>() == null)
                go.AddComponent<IsoRPG.Player.KeyboardMove>();

            return go;
        }

        private static void Camera(Transform target)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            var camera = go.AddComponent<UnityEngine.Camera>();
            camera.orthographic = false;

            go.AddComponent<AudioListener>();

            var rig = go.AddComponent<IsoRPG.Cameras.IsoCameraRig>();
            rig.SetTarget(target);

            // Мелочь не рисуем дальше сорока пяти метров: издали каждая
            // травинка занимает меньше пикселя и превращается в рябь.
            go.AddComponent<IsoRPG.World.NearOnly>();
        }

        private static void Events()
        {
            var go = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        /// <summary>
        /// Вносит арену в список сцен сборки — иначе игра её не найдёт.
        ///
        /// Меню грузит сцену по имени, а по имени доступны только те, что
        /// перечислены здесь. Забыть эту строку значит получить чёрный экран
        /// после «Начать игру» и ни одной ошибки в журнале.
        /// </summary>
        private static void Register()
        {
            var scenes = EditorBuildSettings.scenes.ToList();

            if (scenes.Any(s => s.path == ScenePath)) return;

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[IsoRPG] Арена внесена в список сцен сборки.");
        }
    }
}
