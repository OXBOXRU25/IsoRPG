using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Огонь и дым в очаге лагеря.
    ///
    /// Котелок над кострищем без пламени читается как заброшенная стоянка, а
    /// лагерь у нас живой — рядом стоит Дозорный. Эффекты берём родные для
    /// нашего арта: `Synty/PolygonParticleFX` нарисован тем же плоскогранным
    /// языком, что и весь мир. Чужой реалистичный огонь пришлось бы приглушать
    /// до неузнаваемости, чтобы он не спорил со стилем.
    ///
    /// Точки сняты щупом `fire-probe`, а не подобраны на глаз: костровище
    /// лежит в (53.62, 0.51, −27.76), тренога с котелком — в (53.65, 0.52,
    /// −27.77), то есть ровно над ним.
    ///
    /// Эффекты вешаются ДЕТЬМИ костровища. Так они переезжают вместе с ним и
    /// не разъезжаются, если лагерь когда-нибудь подвинут: мировые координаты
    /// в коде — это отложенная поломка, которая всплывёт молча.
    /// </summary>
    public static class CampFire
    {
        private const string FireName = "FX_Огонь";
        private const string SmokeName = "FX_Дым";

        private const string FirePrefab =
            "Assets/Synty/PolygonParticleFX/Prefabs/FX_Fire_Small_01.prefab";

        private const string SmokePrefab =
            "Assets/Synty/PolygonParticleFX/Prefabs/FX_Smoke_White_Small_01.prefab";

        /// <summary>Меш костровища — по нему и ищем, имя объекта может быть любым.</summary>
        private const string HearthMesh = "SM_Prop_Camp_Fireplace_01";

        public static void Apply()
        {
            GameObject hearth = null;

            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,
                                                                    FindObjectsSortMode.None))
            {
                if (mf.sharedMesh != null && mf.sharedMesh.name == HearthMesh)
                {
                    hearth = mf.gameObject;
                    break;
                }
            }

            if (hearth == null)
            {
                Debug.LogError("[IsoRPG] Костровища «" + HearthMesh + "» в сцене нет — огонь ставить некуда.");
                return;
            }

            Debug.Log("[IsoRPG] Костровище найдено: «" + hearth.name + "» в " +
                      hearth.transform.position.ToString("0.00"));

            var renderer = hearth.GetComponent<Renderer>();
            float logsTop = renderer != null
                ? renderer.bounds.max.y - hearth.transform.position.y
                : 0.3f;

            // Высота — как у автора в его демо-сцене: ноль, то есть в
            // основание. Первая попытка ставила пламя на 0.6 высоты дров, и
            // огня в кадре не было вовсе — язычки остались внутри поленницы.
            // Число подобрано не на глаз: в `PolygonParticleFX/Scenes/Demo`
            // автор кладёт этот же префаб с localPosition.y = 0, масштаб 1.
            // Масштаб 1.4, а не авторская единица: у автора префаб стоит сам
            // по себе, а у нас — в каменном круге под треногой, и пламя
            // размером в поленницу оттуда едва выглядывает. На кадре 31.08 это
            // читалось как «тлеет», а не «горит».
            Place(hearth, FirePrefab, FireName, Vector3.zero, 1.4f);

            // Дым над котелком. Число подбиралось дважды и оба раза по кадру:
            // 0.6 дало ватный столб выше палатки (у автора частица дыма ростом
            // ШЕСТЬ метров — набор рассчитан на пожары), 0.15 — цепочку
            // отдельных шариков, которая читается как пузыри, а не дым. 0.35
            // сливает их в струю по росту костерка.
            Place(hearth, SmokePrefab, SmokeName, new Vector3(0f, logsTop + 0.55f, 0f), 0.35f);

            EditorUtility.SetDirty(hearth);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

            Check(hearth);
        }

        /// <summary>
        /// Варианты огня в ряд рядом с лагерем — один кадр вместо круга догадок.
        ///
        /// Приём из свода: число, которое нельзя вывести (какой из пяти
        /// вариантов пламени подойдёт костерку), ставится в ряд и снимается
        /// одним кадром. Подбирать по одному — это сборка игры на каждую
        /// догадку, а их тут пять.
        ///
        /// Снимается тем же `campfire-off`.
        /// </summary>
        public static void Row()
        {
            var names = new[]
            {
                "FX_Fire_Small_01",
                "FX_Fire_01",
                "FX_Fire_Big_01",
                "FX_Embers_01",
                "FX_Smoke_White_Small_01",
            };

            var scales = new[] { 1f, 1f, 0.5f, 1f, 0.15f };

            // Открытая площадка у лагеря: костровище в (53.6, −27.8), ряд
            // ставим западнее, чтобы палатка и тренога не перекрывали.
            var start = new Vector3(48f, 0f, -32f);

            var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude,
                                                            FindObjectsSortMode.None);

            for (int i = 0; i < names.Length; i++)
            {
                var path = "Assets/Synty/PolygonParticleFX/Prefabs/" + names[i] + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) { Debug.LogError("[IsoRPG] Нет " + path); continue; }

                var at = start + new Vector3(i * 2.5f, 0f, 0f);

                if (terrain.Length > 0)
                    at.y = terrain[0].SampleHeight(at) + terrain[0].transform.position.y;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = FireName + " ряд " + (i + 1) + " " + names[i];
                instance.transform.position = at;
                instance.transform.localScale = Vector3.one * scales[i];

                Debug.Log("[IsoRPG] Вариант " + (i + 1) + ": " + names[i] +
                          " масштаб " + scales[i] + " в " + at.ToString("0.0"));
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        /// <summary>Снять эффекты — если Павлу не понравится, отката на одну строку.</summary>
        public static void Clear()
        {
            int removed = 0;

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                                                                  FindObjectsSortMode.None))
            {
                if (t == null) continue;

                // По началу имени, а не по точному совпадению: ряд вариантов
                // зовётся «FX_Огонь ряд 3 …», и точное сравнение оставило бы
                // его в сцене — а это ровно тот мусор, который потом ищут
                // глазами по всей карте.
                if (!t.name.StartsWith(FireName) && !t.name.StartsWith(SmokeName)) continue;

                Object.DestroyImmediate(t.gameObject);
                removed++;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("[IsoRPG] Эффектов очага снято: " + removed + ".");
        }

        // ------------------------------------------------------------------

        private static void Place(GameObject parent, string path, string name,
                                  Vector3 localPosition, float scale)
        {
            // Уже стоит — не плодим второй. Задание гоняется вместе с другими,
            // и повторный прогон иначе набивал бы очаг копиями пламени.
            var existing = parent.transform.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                Debug.LogError("[IsoRPG] Нет эффекта " + path);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent.transform, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localScale = Vector3.one * scale;

            Debug.Log("[IsoRPG] Поставлен «" + name + "» в " +
                      instance.transform.position.ToString("0.00") + ", масштаб " + scale + ".");
        }

        /// <summary>
        /// Щуп: спрашиваем сцену, а не журнал. И заодно проверяем шейдеры
        /// частиц — набор рисовался под встроенный конвейер, и в URP такой
        /// материал становится розовым или невидимым.
        /// </summary>
        private static void Check(GameObject hearth)
        {
            var fire = hearth.transform.Find(FireName);
            var smoke = hearth.transform.Find(SmokeName);

            Debug.Log("[IsoRPG] Очаг: огонь " + (fire != null ? "есть" : "НЕТ") +
                      ", дым " + (smoke != null ? "есть" : "НЕТ") + ".");

            int bad = 0;

            foreach (var ps in hearth.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var material = ps.sharedMaterial;

                if (material == null || material.shader == null)
                {
                    Debug.LogError("[IsoRPG]   у «" + ps.name + "» нет материала.");
                    bad++;
                    continue;
                }

                string shader = material.shader.name;

                // Признак «шейдер не из нашего конвейера»: в URP такой
                // материал не рисуется вовсе или становится розовым — и
                // выглядит это как «эффект не поставился».
                bool urp = shader.StartsWith("Universal Render Pipeline") ||
                           shader.StartsWith("Shader Graphs") ||
                           shader.StartsWith("Particles/") == false;

                Debug.Log("[IsoRPG]   «" + ps.name + "» шейдер «" + shader + "»" +
                          (urp ? "" : " — НЕ URP, будет розовым"));

                if (!urp) bad++;
            }

            if (bad > 0)
                Debug.LogError("[IsoRPG] Частиц с плохим материалом: " + bad + ".");
        }
    }
}
