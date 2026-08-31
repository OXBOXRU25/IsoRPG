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

        private const string HearthMesh = "SM_Prop_Camp_Fireplace_01";

        [MenuItem("Tools/IsoRPG/Мир: лошадь у лагеря", priority = 43)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[IsoRPG] Останови игру — в режиме игры сцена не правится.");
                return;
            }

            var old = GameObject.Find(GroupName);
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

            var horse = new GameObject(GroupName);
            horse.transform.position = new Vector3(Spot.x, y, Spot.y);
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

            Paint(model);

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
            var controller = HorseAnimations.Build();

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

            Debug.Log("[IsoRPG] Лошадь у лагеря: " + horse.transform.position.ToString("0.00") +
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
        private static void Paint(GameObject model)
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
