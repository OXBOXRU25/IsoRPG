using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Край мира: стена из леса и скал по периметру карты.
    ///
    /// До сих пор земля просто обрывалась: доходишь до края — а там пустота,
    /// «космос». Это разом убивает ощущение мира: становится видно, что он
    /// кончается, и притом кончается ничем.
    ///
    /// Приём взят у WoW, и он честный: границу закрывают непроходимым лесом
    /// и скалами. Игрок упирается взглядом в мир, а не в его отсутствие, и
    /// локация кажется куском чего-то большого, хотя размер тот же.
    ///
    /// Три слоя, и каждый нужен:
    ///
    /// 1. **Внутренний лес** — редкие деревья заходят на игровую площадь,
    ///    чтобы граница не выглядела линией, проведённой по линейке.
    /// 2. **Плотная стена** — деревья вплотную, сквозь них не видно.
    /// 3. **Скалы за краем** — они выше леса и торчат над кронами, давая
    ///    дальний план: мир продолжается там, куда не дойти.
    /// </summary>
    public static class WorldBorder
    {
        private const string HolderName = "WorldBorder";
        private const string Synty = "Assets/_Game/Prefabs/Synty";

        /// <summary>Деревья стены. Разные, иначе стена читается как забор.</summary>
        private static readonly string[] Trees =
        {
            Synty + "/SM_Env_Tree_Large_01.prefab",
            Synty + "/SM_Env_Tree_Round_04.prefab",
            Synty + "/SM_Env_Tree_Thin_02.prefab",
            Synty + "/SM_Env_Tree_Thin_03.prefab",
            Synty + "/SM_Env_Tree_Thin_04.prefab",
        };

        /// <summary>Скалы дальнего плана.</summary>
        private static readonly string[] Cliffs =
        {
            "Assets/Synty/PolygonNatureBiomes/PNB_Enchanted_Forest/Prefabs/SM_Env_Dirt_Cliff_01.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Enchanted_Forest/Prefabs/SM_Env_Dirt_Cliff_03.prefab",
            "Assets/PolygonElvenRealm/Prefabs/Environment/SM_Env_Rock_01.prefab",
            "Assets/PolygonElvenRealm/Prefabs/Environment/SM_Env_Rock_03.prefab",
        };

        [MenuItem("Tools/IsoRPG/Край мира: закрыть лесом и скалами", priority = 59)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            Clear();

            var ground = GameObject.Find("Ground");
            var groundRenderer = ground != null ? ground.GetComponent<Renderer>() : null;

            if (groundRenderer == null)
            {
                Debug.LogError("[IsoRPG] Не нашёл землю — не от чего отмерять край.");
                return;
            }

            var bounds = groundRenderer.bounds;
            var holder = new GameObject(HolderName);

            var random = new System.Random(11);
            var taken = new System.Collections.Generic.List<Vector3>();
            int placed = 0, skipped = 0;

            // Обходим периметр по четырём сторонам.
            // Шаг вдвое мельче прежнего.
            //
            // С семи метров стена вышла дырявой: у этих деревьев высокий
            // голый ствол, а крона наверху — и ровно на уровне глаз между
            // стволами видно небо. Смысл границы в том, чтобы взгляд
            // упирался в мир, а не в просвет.
            const float StepAlong = 4f;

            foreach (var side in new[] { 0, 1, 2, 3 })
            {
                bool horizontal = side < 2;
                float fixedAt = side == 0 ? bounds.max.z
                              : side == 1 ? bounds.min.z
                              : side == 2 ? bounds.max.x
                                          : bounds.min.x;

                float outward = (side == 0 || side == 2) ? 1f : -1f;

                float from = horizontal ? bounds.min.x : bounds.min.z;
                float to = horizontal ? bounds.max.x : bounds.max.z;

                for (float along = from; along <= to; along += StepAlong)
                {
                    // Все ряды ВНУТРЬ игрового квадрата, а не наружу.
                    //
                    // Первая версия ставила стену по кромке и за ней — а за
                    // краем пола нет, деревья висели в пустоте, и между
                    // стволами просвечивало небо снизу. Стена должна стоять
                    // на земле: тогда она читается как настоящий лес, а не
                    // как декорация, приклеенная к обрыву.
                    for (int row = 0; row < 4; row++)
                    {
                        // Самый внутренний ряд редкий: он размывает линию,
                        // чтобы граница не выглядела проведённой по линейке.
                        if (row == 3 && random.Next(100) > 40) continue;

                        float offset = fixedAt - outward * (2f + row * 6f);

                        float jitterAlong = (float)(random.NextDouble() - 0.5) * StepAlong * 0.9f;
                        float jitterOut = (float)(random.NextDouble() - 0.5) * 4f;

                        Vector3 at = horizontal
                            ? new Vector3(along + jitterAlong, 0f, offset + outward * jitterOut)
                            : new Vector3(offset + outward * jitterOut, 0f, along + jitterAlong);

                        // Держим расстояние: крупные деревья, поставленные
                        // вплотную, срастаются в мешанину, где мелкое
                        // растёт внутри большого.
                        // Девять метров вместо шести: у этих деревьев корни
                        // расходятся лапами на несколько метров, и с прежним
                        // зазором стена превращалась в переплетение корней.
                        // Стена от этого не поредеет — она держится не
                        // частотой, а четырьмя рядами со сдвигом.
                        if (Crowded(taken, at, 9f)) { skipped++; continue; }

                        string what = Trees[random.Next(Trees.Length)];

                        int put = Put(what, holder.transform, at,
                                      random.Next(360),
                                      1.1f + (float)random.NextDouble() * 0.7f);

                        if (put > 0) taken.Add(at);
                        placed += put;
                    }

                    // Скалы за краем — реже деревьев и крупнее: они дают
                    // дальний план, а сплошная стена камня выглядела бы
                    // коробкой.
                    // Скалы придвинуты вплотную за деревья и стоят чаще:
                    // они и есть настоящая стена, а лес перед ними —
                    // только передний план.
                    // Скалы — самым дальним рядом, тоже на земле: они выше
                    // леса и закрывают то, что видно поверх крон.
                    if (random.Next(100) < 70)
                    {
                        float offset = fixedAt - outward * 1f;

                        Vector3 at = horizontal
                            ? new Vector3(along, 0f, offset)
                            : new Vector3(offset, 0f, along);

                        placed += Put(Cliffs[random.Next(Cliffs.Length)], holder.transform, at,
                                      random.Next(360),
                                      2.2f + (float)random.NextDouble() * 1.8f);
                    }
                }
            }

            Rebake();

            Selection.activeGameObject = holder;

            Debug.Log("[IsoRPG] Край мира закрыт: деталей " + placed + ", проредено " + skipped +
                      ". Земля " + bounds.size.x.ToString("0") + "×" + bounds.size.z.ToString("0") + " м.");
        }

        [MenuItem("Tools/IsoRPG/Край мира: убрать", priority = 60)]
        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == HolderName);

            if (old != null) Object.DestroyImmediate(old);
        }

        // ------------------------------------------------------------------

        /// <summary>Есть ли рядом уже поставленный сосед.</summary>
        private static bool Crowded(System.Collections.Generic.List<Vector3> taken, Vector3 at, float gap)
        {
            foreach (var other in taken)
            {
                float dx = other.x - at.x;
                float dz = other.z - at.z;

                if (dx * dx + dz * dz < gap * gap) return true;
            }

            return false;
        }

        private static int Put(string path, Transform parent, Vector3 at, float angle, float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return 0;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            go.transform.localScale = Vector3.one * scale;

            // Сажаем на землю по нарисованным границам: деревья и скалы
            // собраны под свой рельеф, и половина висела бы в воздухе.
            var renderers = go.GetComponentsInChildren<Renderer>()
                              .Where(r => !(r is ParticleSystemRenderer))
                              .ToArray();

            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                go.transform.position += new Vector3(0f, at.y - bounds.min.y, 0f);
            }

            return 1;
        }

        /// <summary>
        /// Перепекает навигацию: стена должна быть непроходимой.
        ///
        /// Без этого игрок пройдёт сквозь лес и снова окажется в пустоте —
        /// то есть вся затея не сработает ровно там, где её проверяют.
        /// </summary>
        private static void Rebake() => NavBake.Rebake();
    }
}
