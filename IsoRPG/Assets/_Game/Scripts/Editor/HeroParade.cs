using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Выводит в арену всех героев набора — живыми и ходящими.
    ///
    /// Восемь штук на скриншоте ничего не решают: выбирать надо из всех, и
    /// смотреть надо в движении. Стоячая фигура выглядит прилично у любого
    /// набора, а идущая сразу показывает походку, пропорции и не скользят ли
    /// ноги.
    ///
    /// Почему это дёшево именно здесь. Персонажи Synty и анимации Synty — один
    /// скелет: ретаргета нет вовсе, а значит нет и трёх вчерашних бед
    /// (несовпадение типа рига, высота корня, разъезжающиеся пропорции). Тот
    /// же контроллер, что у нашего игрока, работает на них без единой правки.
    ///
    /// Каждому даём навигацию и витринное блуждание — то же, чем мы смотрели
    /// монстров. Боевой логики не вешаем: это смотрины, а не игра.
    /// </summary>
    public static class HeroParade
    {
        private const string Presets =
            "Assets/PolygonFantasyHeroCharacters/Prefabs/Characters_Presets";

        private const string Controller =
            "Assets/_Game/Art/Animations/Controllers/AC_Rogue.controller";

        private const string HolderName = "Парад героев";

        /// <summary>Шаг сетки, метров. Меньше — толкаются при блуждании.</summary>
        private const float Step = 3.2f;

        /// <summary>Сколько в ряду. Двенадцать на сто двадцать — десять рядов.</summary>
        private const int PerRow = 12;

        /// <summary>Куда ставим строй относительно центра арены.</summary>
        private static readonly Vector3 Origin = new Vector3(-18f, 0f, 14f);

        [MenuItem("Tools/IsoRPG/Парад героев: вывести всех в арену", priority = 43)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play изменения не сохранятся.");
                return;
            }

            Clear(silent: true);

            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { Presets })
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .OrderBy(NumberIn)
                                     .ToArray();

            if (paths.Length == 0)
            {
                Debug.LogWarning("[IsoRPG] Пресетов не найдено в " + Presets);
                return;
            }

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controller);

            if (controller == null)
                Debug.LogWarning("[IsoRPG] Нет контроллера " + Controller +
                                 " — герои встанут столбами.");

            var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None)
                                .FirstOrDefault();

            var holder = new GameObject(HolderName);
            int placed = 0, noAvatar = 0;

            for (int i = 0; i < paths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);

                int row = i / PerRow;
                int col = i % PerRow;

                var at = Origin + new Vector3(col * Step, 0f, -row * Step);

                // Ставим на землю по рельефу: арена больше не плоская, и
                // строй, выложенный по нулю, половиной уйдёт в холм.
                if (terrain != null)
                    at.y = terrain.SampleHeight(at) + terrain.transform.position.y;

                go.transform.position = at;
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                // Аниматор — на самом объекте модели. Аватар описывает пути к
                // костям от своего корня; повешенный этажом выше, он не найдёт
                // ни одной, и персонаж замрёт без единой ошибки в журнале.
                var animator = go.GetComponentInChildren<Animator>(true);

                if (animator == null) animator = go.AddComponent<Animator>();

                if (animator.avatar == null)
                {
                    // У префаба Synty аватар лежит в модели: достаём оттуда.
                    var model = PrefabUtility.GetCorrespondingObjectFromSource(prefab) ?? prefab;
                    var fromModel = FindAvatar(model);

                    if (fromModel != null) animator.avatar = fromModel;
                    else noAvatar++;
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;   // двигает навигация

                var agent = go.GetComponent<NavMeshAgent>();
                if (agent == null) agent = go.AddComponent<NavMeshAgent>();

                agent.radius = 0.35f;
                agent.height = 1.8f;
                agent.speed = 1.6f;
                agent.angularSpeed = 220f;
                agent.acceleration = 8f;

                // Витринное блуждание: ходят вокруг своего места и не
                // разбредаются по всей карте.
                var wanderer = go.GetComponent<IsoRPG.Dev.Wanderer>();
                if (wanderer == null) wanderer = go.AddComponent<IsoRPG.Dev.Wanderer>();

                wanderer.Range = Step * 0.9f;

                go.name = System.IO.Path.GetFileNameWithoutExtension(paths[i]);

                // Номер над головой.
                //
                // Заказчик выбирает из ста двадцати, и единственный способ
                // указать на конкретного — назвать номер. Контактные листы из
                // превью тут хуже: на них не видно, как персонаж держится в
                // нашем свете и в движении.
                //
                // Текст сам поворачивается к камере — за это отвечает Billboard
                // на самом объекте; без него половина цифр окажется к нам
                // боком и не прочитается.
                var label = new GameObject("Номер " + Number(paths[i]));
                label.transform.SetParent(go.transform, false);
                label.transform.localPosition = new Vector3(0f, 2.05f, 0f);

                var mesh = label.AddComponent<TextMesh>();
                mesh.text = Number(paths[i]).ToString();
                mesh.characterSize = 0.035f;   // в десять раз мельче: прежние закрывали самих героев
                mesh.fontSize = 64;
                mesh.anchor = TextAnchor.MiddleCenter;
                mesh.alignment = TextAlignment.Center;
                mesh.color = new Color(1f, 0.92f, 0.35f);

                // Рисуем поверх всего: иначе цифра прячется за плечами и
                // кронами, а искать её глазами — та же морока, что и без неё.
                var renderer = label.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = 100;
                    renderer.shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                label.AddComponent<IsoRPG.World.FaceCamera>();

                placed++;
            }

            Debug.Log("[IsoRPG] Парад героев: выведено " + placed + " из " + paths.Length +
                      (noAvatar > 0 ? ", без аватара " + noAvatar : "") +
                      ". Строй " + PerRow + " в ряд, шаг " + Step + " м, начало " + Origin + ".");

            MarkDirty();
        }

        [MenuItem("Tools/IsoRPG/Парад героев: убрать", priority = 44)]
        public static void ClearMenu() => Clear(silent: false);

        private static void Clear(bool silent)
        {
            var found = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                              .Where(g => g.name == HolderName)
                              .ToList();

            foreach (var g in found) Object.DestroyImmediate(g);

            if (!silent)
            {
                Debug.Log("[IsoRPG] Парад убран: " + found.Count + ".");
                MarkDirty();
            }
        }

        /// <summary>Аватар из модели префаба — ищем по всем вложенным ассетам.</summary>
        private static Avatar FindAvatar(GameObject prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);

            if (string.IsNullOrEmpty(path)) return null;

            var direct = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (direct != null) return direct;

            // У префаба-пресета аватар лежит в исходной модели: идём по
            // ссылкам на skinned-меши и берём аватар их файла.
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;

                string meshPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
                if (string.IsNullOrEmpty(meshPath)) continue;

                var a = AssetDatabase.LoadAllAssetsAtPath(meshPath).OfType<Avatar>().FirstOrDefault();
                if (a != null) return a;
            }

            return null;
        }

        /// <summary>Номер пресета для подписи над головой.</summary>
        private static int Number(string path) => NumberIn(path);

        /// <summary>Номер из имени: чтобы строй шёл по порядку, а не по алфавиту.</summary>
        private static int NumberIn(string path)
        {
            var digits = new string(System.IO.Path.GetFileNameWithoutExtension(path)
                                                  .Where(char.IsDigit).ToArray());

            return int.TryParse(digits, out int n) ? n : 0;
        }

        private static void MarkDirty()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
