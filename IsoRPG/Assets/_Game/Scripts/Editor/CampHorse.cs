using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Вторая лошадь — у лагеря дозорного.
    ///
    /// Модель та же, что у первой лошади (Malbers Poly Art), и это не лень.
    /// Сперва я взял лошадь Synty — она ближе по стилю и у неё десяток
    /// мастей. Но набор 2018 года везёт ОДИН безымянный клип на весь пакет:
    /// лошадь встала у палатки истуканом, и Павлон это увидел раньше, чем я
    /// сообразил проверить. Анимации живут у Malbers, а стиль вторичен по
    /// сравнению с неподвижной лошадью посреди живого лагеря.
    ///
    /// Масть — «в яблоках», чтобы не путалась с первой, гнедой. Красится тем
    /// же способом, что и первая: текстура поверх URP/Lit, в обход авторского
    /// шейдера Malbers (он под старый конвейер и в Universal не рисуется).
    ///
    /// Бить нельзя: сторона Neutral. Кольцо выделения при этом работает и
    /// будет зелёным — игрок вправе щёлкнуть по лошади и увидеть, что это.
    /// </summary>
    public static class CampHorse
    {
        private const string GroupName = "Лошадь у лагеря";

        private const string ModelPath =
            "Assets/Malbers Animations/Horse AnimSet Pro/7 - Models/Horses/Horse Poly Art.FBX";

        private const string SkinPath =
            "Assets/Malbers Animations/Horse AnimSet Pro/5 - Materials & Textures/Horse Poly Art/T_Horse_Gray_Spots.psd";

        private const string MaterialPath = "Assets/_Game/Art/Materials/Mobs/Horse_Spots_URP.mat";

        /// <summary>
        /// Место. В 3.4 м от дозорного (51.73, −28.23), юго-западнее — там
        /// открыто: бочки, стойка и ящик лагеря стоят восточнее.
        /// </summary>
        private static readonly Vector2 Spot = new Vector2(48.9f, -30.1f);

        /// <summary>Вторая лошадь — там, где её ставил прежний отдельный сборщик.</summary>
        private static readonly Vector2 SecondSpot = new Vector2(36f, 30f);

        private const string HearthMesh = "SM_Prop_Camp_Fireplace_01";

        [MenuItem("Tools/IsoRPG/Мир: лошадь у лагеря", priority = 43)]
        /// <summary>
        /// Ставит ОБЕИХ лошадей — одним и тем же кодом.
        ///
        /// До 01.09.2026 их собирали два разных задания, HorsePack и
        /// CampHorse, хотя набор компонентов у них совпадал полностью.
        /// Разошлись они только в мелочах сборки — и одна лошадь то
        /// отталкивалась, то вставала камнем без контроллера, пока вторая
        /// работала. Павлон: «зачем их ставят разные задания? чем это
        /// аргументировано? сделай первую тем же заданием, что вторую».
        ///
        /// Возразить нечем: одинаковые вещи должен собирать один код.
        /// </summary>
        public static void Build()
        {
            // Контроллер собираем ОДИН раз на обеих.
            //
            // HorseAnimations.Build() удаляет старый ассет и создаёт заново,
            // поэтому второй вызов рушил ссылку, которую только что получила
            // первая лошадь — она и вставала камнем. Классическая ловушка
            // общего ассета: код одинаковый, а ресурс на двоих один.
            // Сносим лошадей ЛЮБОГО прежнего имени, а не только своего.
            //
            // Прежний сборщик звал свою «Лошадь», наш ищет «Лошадь у пруда»
            // — и старая оставалась на месте: Павлон увидел двух в одной
            // точке. Убираем по всем именам, которые у них когда-либо были.
            foreach (var name in new[] { "Лошадь", "Лошадь у лагеря", "Лошадь у пруда" })
            {
                var stale = GameObject.Find(name);
                if (stale != null) Object.DestroyImmediate(stale);
            }

            var controller = HorseAnimations.Build();
            var skinMaterial = MakeSkin();

            BuildOne(Spot, "Лошадь у лагеря", controller, skinMaterial);
            BuildOne(SecondSpot, "Лошадь у пруда", controller, skinMaterial);
        }

        private static void BuildOne(Vector2 spot, string groupName, UnityEditor.Animations.AnimatorController controller, Material skinMaterial)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[IsoRPG] Останови игру — в режиме игры сцена не правится.");
                return;
            }

            var old = GameObject.Find(groupName);
            if (old != null) Object.DestroyImmediate(old);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

            if (source == null)
            {
                Debug.LogError("[IsoRPG] Нет модели " + ModelPath);
                return;
            }

            var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude,
                                                            FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — ставить не на что.");
                return;
            }

            float y = terrain.SampleHeight(new Vector3(Spot.x, 0f, Spot.y)) +
                      terrain.transform.position.y;

            var horse = new GameObject(groupName);
            horse.transform.position = new Vector3(spot.x, y, spot.y);
            horse.transform.rotation = Quaternion.Euler(0f, FaceHearth(horse.transform.position), 0f);

            // Узел наклона: он же прижимает модель к грунту, как у остальной
            // живности — иначе лошадь встанет на высоту навигационной сетки.
            var tilt = new GameObject("Наклон");
            tilt.transform.SetParent(horse.transform, false);
            tilt.AddComponent<IsoRPG.World.GroundAlign>();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = "Модель";
            model.transform.SetParent(tilt.transform, false);
            model.transform.localPosition = Vector3.zero;

            Paint(model, skinMaterial);

            // Прижимаем нижней точкой к земле.
            var parts = model.GetComponentsInChildren<Renderer>(true);

            if (parts.Length > 0)
            {
                var box = parts[0].bounds;
                foreach (var part in parts) box.Encapsulate(part.bounds);

                tilt.transform.localPosition = new Vector3(0f, y - box.min.y, 0f);
            }

            // Анимация. Без неё лошадь стоит истуканом посреди живого
            // лагеря — и это первое, что видно глазом.

            if (controller != null)
            {
                var animator = horse.GetComponentInChildren<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
            }
            else Debug.LogWarning("[IsoRPG] Контроллер лошади не собрался — вторая лошадь будет статичной.");

            MobMaterials.FixLegacyShaders(model);

            var body = horse.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 0.9f, 0f);
            body.height = 1.8f;
            body.radius = 0.6f;

            var targetable = horse.AddComponent<Targetable>();
            targetable.Setup("Лошадь", Faction.Neutral);
            targetable.SetOverheadHeight(2.4f);

            var health = horse.AddComponent<Health>();
            health.Setup(999);

            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] " + groupName + ": " + horse.transform.position.ToString("0.00") +
                      ", разворот " + horse.transform.eulerAngles.y.ToString("0") + "°.");

            Check(horse);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Масть текстурой поверх нашего материала.
        ///
        /// Материал ОБЯЗАТЕЛЬНО сохраняем файлом: несохранённый живёт до
        /// перезагрузки сцены, а потом объект остаётся вовсе без материала и
        /// становится пурпурным — поломка, которую легко принять за чужую.
        /// </summary>
        /// <summary>
        /// Готовит материал масти РАЗ на всех лошадей.
        ///
        /// Создание удаляет старый ассет и делает новый, поэтому вызов на
        /// вторую лошадь оставлял первую без материала — она становилась
        /// пурпурной. Ровно то же было с контроллером анимаций. Общий
        /// ресурс готовим один раз и раздаём ссылку.
        /// </summary>
        private static Material MakeSkin()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;

            var skin = AssetDatabase.LoadAssetAtPath<Texture2D>(SkinPath);
            if (skin == null) { Debug.LogError("[IsoRPG] Нет текстуры масти " + SkinPath); return null; }

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) { Debug.LogError("[IsoRPG] Нет шейдера URP/Lit — красить нечем."); return null; }

            var material = new Material(lit) { name = "Horse_Synty_03" };
            material.SetTexture("_BaseMap", skin);
            material.SetFloat("_Smoothness", 0.1f);

            var folder = System.IO.Path.GetDirectoryName(MaterialPath).Replace((char)92, (char)47);
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace((char)92, (char)47),
                                           System.IO.Path.GetFileName(folder));

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void Paint(GameObject model, Material material)
        {
            if (material != null) MobMaterials.ApplyMaterial(model, material);
        }

        private static void PaintOld(GameObject model)
        {
            var skin = AssetDatabase.LoadAssetAtPath<Texture2D>(SkinPath);

            if (skin == null)
            {
                Debug.LogError("[IsoRPG] Нет текстуры масти " + SkinPath);
                return;
            }

            var lit = Shader.Find("Universal Render Pipeline/Lit");

            if (lit == null)
            {
                Debug.LogError("[IsoRPG] Нет шейдера URP/Lit — красить нечем.");
                return;
            }

            var material = new Material(lit) { name = "Horse_Synty_03" };
            material.SetTexture("_BaseMap", skin);
            material.SetFloat("_Smoothness", 0.1f);

            var folder = System.IO.Path.GetDirectoryName(MaterialPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'),
                                           System.IO.Path.GetFileName(folder));

            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
                AssetDatabase.DeleteAsset(MaterialPath);

            AssetDatabase.CreateAsset(material, MaterialPath);

            MobMaterials.ApplyMaterial(model, material);
        }

        /// <summary>Смотрит на костёр — как и дозорный.</summary>
        private static float FaceHearth(Vector3 from)
        {
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,
                                                                     FindObjectsSortMode.None))
            {
                if (mf.sharedMesh == null || mf.sharedMesh.name != HearthMesh) continue;

                Vector3 to = mf.transform.position - from;
                to.y = 0f;

                if (to.sqrMagnitude > 0.01f) return Quaternion.LookRotation(to).eulerAngles.y;
            }

            return 60f;
        }

        /// <summary>Щуп: спрашиваем сцену, а не журнал.</summary>
        private static void Check(GameObject horse)
        {
            var npc = Object.FindObjectsByType<Targetable>(FindObjectsInactive.Include,
                                                            FindObjectsSortMode.None)
                            .FirstOrDefault(t => t.DisplayName == "Дозорный");

            float away = npc != null
                ? Vector3.Distance(horse.transform.position, npc.transform.position)
                : -1f;

            Debug.Log("[IsoRPG] До дозорного " +
                      (away < 0f ? "дозорного в сцене НЕТ" : away.ToString("0.0") + " м") + ".");

            int painted = 0, pink = 0;

            foreach (var r in horse.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;

                if (m == null || m.shader == null) { pink++; continue; }
                if (m.shader.name.StartsWith("Universal Render Pipeline")) painted++;
                else pink++;
            }

            Debug.Log("[IsoRPG] Частей на URP " + painted + ", проблемных " + pink + ".");

            if (pink > 0) Debug.LogError("[IsoRPG] У лошади " + pink + " частей будут пурпурными.");

            // Анимация — спрашиваем сам объект. Статичная лошадь посреди
            // лагеря видна сразу, а в журнале выглядит как успех.
            var anim = horse.GetComponentInChildren<Animator>();

            if (anim == null || anim.runtimeAnimatorController == null)
                Debug.LogError("[IsoRPG] У лошади НЕТ анимации — будет стоять истуканом.");
            else
                Debug.Log("[IsoRPG] Анимация: «" + anim.runtimeAnimatorController.name + "».");
        }
    }
}
