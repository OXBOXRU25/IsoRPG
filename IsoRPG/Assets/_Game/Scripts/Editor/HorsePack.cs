using System.Linq;
using IsoRPG.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Ставит на карту одну лошадь — не бойца: мирная, стоит на месте и
    /// щиплет траву, никогда не нападает и не нападают на неё. Три этажа
    /// корень/наклон/модель — та же физика вставания на склон, что у
    /// волков и кабанов.
    /// </summary>
    public static class HorsePack
    {
        // Модель — из «Horse AnimSet Pro» (Malbers), не старый POLYGON
        // Horse 2018 года: у того был один безымянный клип на весь пакет,
        // здесь — полный набор, включая щипание травы.
        private const string PrefabPath =
            "Assets/Malbers Animations/Horse AnimSet Pro/7 - Models/Horses/Horse Poly Art.FBX";
        private const string GroupName = "Лошадь";

        // (13, 22) тоже оказалась водой — не в чаше одного из четырёх
        // прудов (это проверяется), а в русле реки, которое SyntyWater
        // вообще не знает и проверить не может. Вместо повторного гадания —
        // вплотную к герою (40, 28), там место уже дважды подтверждено
        // сухим на кадрах.
        private static readonly Vector2 Spot = new Vector2(36f, 30f);

        [MenuItem("Tools/IsoRPG/Мир: поставить лошадь", priority = 40)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play изменения не сохранятся.");
                return;
            }

            var controller = HorseAnimations.Build();   // может быть null — лошадь тогда статична

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (source == null)
            {
                Debug.LogError("[IsoRPG] Не найден префаб лошади: " + PrefabPath);
                return;
            }

            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — не на что ставить лошадь.");
                return;
            }

            var old = GameObject.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old);

            // Формально внутри радиуса чаши пруда (60, 58) — но это то же
            // самое место, вплотную к точке героя, которая на кадрах
            // сухая раз за разом. Проверка «внутри чаши» исключает весь
            // круг по самому широкому языку залива, а не форму настоящего
            // берега — здесь она ошибается, пропускаем её нарочно.

            float y = terrain.SampleHeight(new Vector3(Spot.x, 0f, Spot.y)) +
                      terrain.transform.position.y;

            if (NavMesh.SamplePosition(new Vector3(Spot.x, y, Spot.y), out var hit, 6f, NavMesh.AllAreas))
                y = hit.position.y;
            else
                Debug.LogWarning("[IsoRPG] Точка лошади вне навигационной сетки — стоять сможет, гулять нет.");

            var horse = new GameObject(GroupName);
            horse.transform.position = new Vector3(Spot.x, y, Spot.y);

            var tilt = new GameObject("Наклон");
            tilt.transform.SetParent(horse.transform, false);
            tilt.AddComponent<IsoRPG.World.GroundAlign>();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = "Модель";
            model.transform.SetParent(tilt.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // FixLegacyShaders чинит чужой шейдер в занятом слоте, но у
            // этой модели рендереры вовсе БЕЗ материала (у FBX в мете
            // materialImportMode: 0 — материалы не импортируются), и
            // Unity подставляет запасной оранжевый, потом наш перевод дал
            // сплошной белый: родной материал набора — не обычная текстура
            // с цветом, а шейдер-перекраска по маске (Color4x4), и в нём
            // нет ни «_MainTex», ни «_Color» под привычными именами —
            // угадывание свойств ушло в пустоту. Берём текстуру ФАЙЛОМ
            // напрямую, в обход чужого шейдера вообще.
            const string texPath =
                "Assets/Malbers Animations/Horse AnimSet Pro/5 - Materials & Textures/Horse Poly Art/T_Horse_Brown.psd";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            if (tex != null)
            {
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                var horseMat = new Material(lit) { name = "Horse_Brown_URP" };
                horseMat.SetTexture("_BaseMap", tex);

                const string savePath = "Assets/_Game/Art/Materials/Mobs/Horse_Brown_URP.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(savePath) != null)
                    AssetDatabase.DeleteAsset(savePath);
                AssetDatabase.CreateAsset(horseMat, savePath);

                MobMaterials.ApplyMaterial(model, horseMat);
            }
            else
            {
                Debug.LogWarning("[IsoRPG] Текстура лошади не найдена: " + texPath);
            }

            int fixedMats = MobMaterials.FixLegacyShaders(model);

            var parts = model.GetComponentsInChildren<Renderer>(true);
            float lift = 0f;

            if (parts.Length > 0)
            {
                var box = parts[0].bounds;
                foreach (var part in parts) box.Encapsulate(part.bounds);

                float groundY = terrain.SampleHeight(new Vector3(Spot.x, 0f, Spot.y)) +
                                terrain.transform.position.y;

                lift = groundY - box.min.y;
                tilt.transform.localPosition = new Vector3(0f, lift, 0f);
            }

            if (controller != null)
            {
                var animator = horse.GetComponentInChildren<Animator>();
                if (animator == null) animator = horse.AddComponent<Animator>();

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
            }

            var body = horse.AddComponent<CapsuleCollider>();
            body.height = 1.8f;
            body.radius = 0.6f;
            body.center = new Vector3(0f, 0.9f, 0f);

            var targetable = horse.AddComponent<Targetable>();
            targetable.Setup("Лошадь", Faction.Neutral);

            var health = horse.AddComponent<Health>();
            health.Setup(999);

            // Без агента и без мозга — НАРОЧНО.
            //
            // Прогулка через тот же ИИ, что у волка, дала скольжение прямо
            // сквозь забор (навигация не знает о декоративных заборах) и
            // рывок на большое расстояние, что выглядело как телепортация.
            // Теперь у лошади есть настоящая анимация («щипание травы»),
            // и стоять на месте с ней — не «поломка без анимации», а
            // осмысленный декоративный зверь. Гулять при желании можно
            // будет добавить отдельно, вместе с настоящим циклом ходьбы.
            EditorUtility.SetDirty(horse);
            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] Лошадь поставлена на " + horse.transform.position.ToString("0.0") +
                      ", подъём модели " + lift.ToString("0.00") + " м, материалов на URP переведено " +
                      fixedMats + ", анимация: " + (controller != null ? "есть" : "нет (статична)") + ".");
        }
    }
}
