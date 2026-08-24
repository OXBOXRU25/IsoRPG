using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Лагерь разбойников — вторая обжитая точка мира.
    ///
    /// Строится не по клеточной карте, как руины, а свободно: лагерь и должен
    /// выглядеть поставленным наспех. Прямые углы и ровные ряды выдали бы
    /// строение, а тут люди просто заняли поляну.
    ///
    /// Стоит на отдалении и в стороне от руин: путь до него — часть игры.
    /// Место, куда попадаешь за десять шагов, не ощущается другим местом.
    /// </summary>
    public static class BanditCampBuilder
    {
        private const string DungeonFolder = "Assets/_Game/Art/KayKit/Dungeon";
        private const string NatureFolder = "Assets/_Game/Art/KayKit/Nature";

        /// <summary>Куда поставлен лагерь. Отсюда же берутся места разбойников.</summary>
        public static readonly Vector3 Centre = new Vector3(-62f, 0f, -46f);

        private const float Radius = 13f;

        public static void Build(Transform parent)
        {
            var camp = new GameObject("Лагерь разбойников");
            camp.transform.SetParent(parent);
            camp.transform.position = Centre;

            BuildFence(camp.transform);
            BuildFirePit(camp.transform);
            BuildLiving(camp.transform);
            BuildGoods(camp.transform);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Частокол по кругу с проходом.
        ///
        /// Разрыв обязателен: замкнутая ограда читается как загон, а лагерь —
        /// это место, куда приходят и откуда уходят. Проход обращён к руинам,
        /// то есть к игроку.
        /// </summary>
        private static void BuildFence(Transform parent)
        {
            const int segments = 22;

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * 360f;

                // Пропуск на северо-востоке — там вход со стороны руин.
                if (angle > 28f && angle < 72f) continue;

                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var at = Centre + direction * Radius;

                // Столбы через один: сплошная стена из ограждений выглядит
                // забором вокруг дачи, а не частоколом.
                string model = i % 4 == 0 ? "barrier_column" : "barrier";

                var piece = Place(DungeonFolder + "/" + model + ".fbx", parent, at,
                                  angle + 90f + Random.Range(-4f, 4f));

                AddCollider(piece);

                // Знамя вешаем прямо на кол, а не рядом с ним.
                //
                // До этого три знамени ставились по кругу на высоте 1.4 м
                // безо всякой опоры — в руинах их держит стена, а тут
                // держать нечем, и полотнища висели в воздухе. Кол ровно
                // для этого и годится: он вертикальный и уже стоит.
                if (model == "barrier_column" && i % 8 == 0)
                {
                    HangBanner(parent, piece, at, angle);
                }
            }
        }

        /// <summary>
        /// Костровище: круг камней и обгорелые обломки.
        ///
        /// Огня в наборе нет, но кострище узнаётся и без пламени — по кольцу
        /// камней. Свет даём отдельно: он и делает место обжитым.
        /// </summary>
        private static void BuildFirePit(Transform parent)
        {
            var at = Centre;

            for (int i = 0; i < 9; i++)
            {
                float angle = i / 9f * 360f;
                var spot = at + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 1.5f;

                Place(NatureFolder + "/Rock_1_A_Color1.fbx", parent, spot,
                      Random.Range(0f, 360f), Random.Range(0.5f, 0.8f));
            }

            Place(DungeonFolder + "/rubble_half.fbx", parent, at, Random.Range(0f, 360f), 0.9f);

            var lightGo = new GameObject("CampFire", typeof(Light));
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.position = at + Vector3.up * 0.6f;

            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.68f, 0.36f);
            light.intensity = 9f;
            light.range = 16f;
            light.shadows = LightShadows.None;
        }

        /// <summary>Лежанки и стол — по ним видно, что здесь ночуют.</summary>
        private static void BuildLiving(Transform parent)
        {
            var beds = new[]
            {
                new Vector3(-5.5f, 0f, 3.5f),
                new Vector3(-6.5f, 0f, -1.5f),
                new Vector3(-3.5f, 0f, -5.5f),
                new Vector3(4.5f, 0f, -5f),
            };

            foreach (var offset in beds)
            {
                var at = Centre + offset;

                Place(DungeonFolder + "/bed_floor.fbx", parent, at,
                      Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg + Random.Range(-12f, 12f));
            }

            var table = Place(DungeonFolder + "/table_long_tablecloth.fbx", parent,
                              Centre + new Vector3(5.5f, 0f, 2.5f), 108f);
            AddCollider(table);

            Place(DungeonFolder + "/stool.fbx", parent, Centre + new Vector3(6.8f, 0f, 4.2f), 40f);
            Place(DungeonFolder + "/chair.fbx", parent, Centre + new Vector3(4.2f, 0f, 0.8f), 220f);
            Place(DungeonFolder + "/plate_food_A.fbx", parent, Centre + new Vector3(5.2f, 0.78f, 2.2f), 30f);
            Place(DungeonFolder + "/bottle_A_labeled_brown.fbx", parent, Centre + new Vector3(6f, 0.78f, 3f), 0f);
        }

        /// <summary>Награбленное: ящики, бочки, мешки у ограды.</summary>
        private static void BuildGoods(Transform parent)
        {
            var spots = new (string model, Vector3 offset, float angle)[]
            {
                ("crates_stacked", new Vector3(8.5f, 0f, -2.5f), 24f),
                ("box_large", new Vector3(7.5f, 0f, -5.5f), 190f),
                ("barrel_large", new Vector3(-8.5f, 0f, 6.5f), 0f),
                ("barrel_small_stack", new Vector3(-2.5f, 0f, 8.5f), 60f),
                ("box_small_decorated", new Vector3(2.5f, 0f, 8f), 140f),
                ("keg", new Vector3(-7f, 0f, -6.5f), 75f),
                ("chest", new Vector3(9f, 0f, 1.5f), 250f),
            };

            foreach (var (model, offset, angle) in spots)
            {
                var piece = Place(DungeonFolder + "/" + model + ".fbx", parent, Centre + offset, angle);
                AddCollider(piece);
            }

            // Знамёна переехали на колья частокола — см. BuildFence.
        }

        /// <summary>
        /// Вешает знамя на кол частокола.
        ///
        /// Высоту берём из самой модели кола, а не числом: колья набора
        /// разной высоты, и вписанное значение подошло бы одному из них,
        /// а на остальных полотнище оказалось бы то в земле, то над ней.
        /// </summary>
        private static void HangBanner(Transform parent, GameObject post,
                                       Vector3 at, float angle)
        {
            if (post == null) return;

            var renderer = post.GetComponentInChildren<Renderer>();
            if (renderer == null) return;

            float top = renderer.bounds.max.y;

            // Чуть ниже верхушки: полотнище, начинающееся ровно с торца,
            // выглядит надетым на кол, а не подвешенным.
            var spot = new Vector3(at.x, top - 0.25f, at.z);

            // Разворачиваем внутрь лагеря: знамя, смотрящее в лес, видно
            // только с той стороны, откуда никто не приходит.
            var banner = Place(DungeonFolder + "/banner_thin_red.fbx",
                               parent, spot, angle + 180f);

            // Тень от полотнища падает на землю отдельным прямоугольником
            // и читается как предмет, которого нет.
            if (banner == null) return;

            foreach (var piece in banner.GetComponentsInChildren<Renderer>())
            {
                piece.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // ------------------------------------------------------------------

        private static GameObject Place(string path, Transform parent, Vector3 at,
                                        float angle, float scale = 1f)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (model == null)
            {
                Debug.LogWarning("[IsoRPG] Нет модели " + path);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.transform.SetParent(parent);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            go.transform.localScale = Vector3.one * scale;

            return go;
        }

        /// <summary>
        /// Коллайдер по границам модели: через частокол и ящики ходить нельзя,
        /// иначе ограда — рисунок на земле, а не преграда.
        /// </summary>
        private static void AddCollider(GameObject go)
        {
            if (go == null) return;

            var renderer = go.GetComponentInChildren<Renderer>();
            if (renderer == null) return;

            var box = go.AddComponent<BoxCollider>();
            var bounds = renderer.bounds;

            box.center = go.transform.InverseTransformPoint(bounds.center);
            box.size = bounds.size;
        }
    }
}
