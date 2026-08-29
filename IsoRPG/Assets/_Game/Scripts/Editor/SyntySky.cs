using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Небо от Synty вместо COZY.
    ///
    /// COZY даёт идущие сутки и погоду, но она из другого художественного
    /// языка: у неё небо мягкое и «фотографическое», а весь наш мир —
    /// гранёный low-poly от Synty. Заказчик попросил держать один язык.
    ///
    /// <b>Что теряем, вслух:</b> ход времени, ночь, погодные переходы.
    /// Skybox Synty — это статичный градиент с облаками, нарисованными в их
    /// стиле. Свет ведёт наше собственное солнце, теми числами, что стояли
    /// до COZY: закат под 21 градусом, длинные тени.
    /// </summary>
    public static class SyntySky
    {
        private const string Folder = "Assets/PolygonNatureBiomes";

        /// <param name="which">
        /// Имя материала без пути: Skybox_Mat_01, Skybox_Meadows_Mat_01 и т.д.
        /// </param>
        public static void Apply(string which = "Skybox_Meadows_Mat_01")
        {
            // Сначала снимаем COZY: пока купол висит, он ведёт небо сам, и
            // подмена материала не даст ничего видимого.
            CozySky.Remove();

            string[] found = AssetDatabase.FindAssets("t:Material " + which)
                                          .Select(AssetDatabase.GUIDToAssetPath)
                                          .Where(p => p.StartsWith(Folder))
                                          .ToArray();

            if (found.Length == 0)
            {
                Debug.LogError("[IsoRPG] Небо Synty «" + which + "» не найдено в " +
                               Folder + ". Набор биомов не установлен?");
                return;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(found[0]);

            if (mat == null)
            {
                Debug.LogError("[IsoRPG] Материал не загрузился: " + found[0]);
                return;
            }

            // ------------------------------------------------------------
            // Небо Synty — это КУПОЛ-МОДЕЛЬ, а не скайбокс движка.
            //
            // Шейдер SyntyStudios/SkyboxUnlit несмотря на имя объявляет
            // Tags { RenderType=Opaque, Queue=Geometry } — обычная
            // непрозрачная геометрия. В RenderSettings.skybox он не встаёт
            // по устройству: движок там ждёт шейдер с Queue=Background.
            //
            // 29.08.2026 я на этом сжёг пять кругов. Небо назначалось,
            // щуп показывал его в сцене, камера очищалась Skybox — и всё
            // равно кадр заливался фоном камеры, потому что рисовать было
            // нечем. Проверка, которая закрыла бы вопрос за минуту: чем
            // объявлен шейдер, а не что написано в его имени.
            // ------------------------------------------------------------

            var domeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "/PNB_Core/Prefabs/SM_Env_Skydome_01.prefab");

            if (domeAsset == null)
            {
                Debug.LogError("[IsoRPG] Купол SM_Env_Skydome_01 не найден в " + Folder);
                return;
            }

            foreach (var old in Object.FindObjectsByType<IsoRPG.World.SkyDomeFollow>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);

            // Небо из ЧЕТЫРЁХ слоёв, как собрано у автора в демо лугового
            // леса. Один купол давал плоский задник — глубину держат
            // перевёрнутый нижний купол и два облачных кольца на разной
            // высоте.
            //
            // Держатель пустой и с единичным масштабом: у автора кольца
            // стоят отдельными объектами на мировых высотах 58 и 106. Повесь
            // я их детьми купола в масштабе 50 — улетели бы на 5300.
            var sky = new GameObject("Небо Synty");
            sky.transform.position = Vector3.zero;

            // Всё небо крупнее авторского в шесть раз: у них мир 150 м, у
            // нас дальность обзора больше радиуса купола, и он читался
            // силуэтом. Пропорции внутри держим авторские.
            // Держатель единичный: облака должны остаться на авторских высотах,
            // иначе они улетают вместе с куполом. Крупнее делаем ТОЛЬКО купола.
            sky.transform.localScale = Vector3.one;
            sky.AddComponent<IsoRPG.World.SkyDomeFollow>();

            var dome = (GameObject)PrefabUtility.InstantiatePrefab(domeAsset, sky.transform);
            dome.name = "Купол верхний";
            dome.transform.localPosition = Vector3.zero;
            // Отрицательный масштаб по X выворачивает грани: шейдер купола
            // объявляет Cull Back, и изнутри он иначе не виден вовсе.
            dome.transform.localScale = new Vector3(-300f, 404f, 300f);

            // Перевёрнутый на 180 нижний купол закрывает горизонт снизу:
            // одного верхнего мало, под его краем виден край мира.
            var lower = (GameObject)PrefabUtility.InstantiatePrefab(domeAsset, sky.transform);
            lower.name = "Купол нижний";
            lower.transform.localPosition = Vector3.zero;
            lower.transform.localRotation = Quaternion.Euler(0f, 180f, 180f);
            lower.transform.localScale = new Vector3(-300f, 147f, 300f);

            Clouds(sky.transform, "Облака верхние", 106f, 8.82f, 0.30f);
            Clouds(sky.transform, "Облака нижние", 58f, 10f, 0.47f);

            // Солнечный диск НЕ ставим. В обеих собранных сценах Synty солнца
            // нет вовсе: ни диска, ни лучей — оно существует только как
            // источник света. Лучи (FX_Sunray) в наборе есть, но автор их в
            // сцены не поставил: это столбы света сквозь крону, на открытом
            // поле им не за что зацепиться.
            // SunDisc(sky.transform);

            int painted = 0;

            foreach (var d in new[] { dome, lower })
            {
                foreach (var r in d.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    painted++;
                }
            }

            Debug.Log("[IsoRPG] Купол неба поставлен, материал назначен " +
                      painted + " рендерерам, тени с него сняты.");

            Unlight(sky);

            // В настройках освещения оставляем ПРОЦЕДУРНОЕ небо движка —
            // так сделано и у автора. Купол закрывает обзор, а процедурное
            // работает подстраховкой: если камера окажется выше купола,
            // вместо плоской заливки будет хотя бы небо.
            var fallback = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");

            if (fallback != null)
            {
                RenderSettings.skybox = fallback;
                Debug.Log("[IsoRPG] В настройках освещения — процедурное небо движка (подстраховка).");
            }

            // Камера обязана очищаться НЕБОМ, а не цветом.
            //
            // Иначе скайбокс не рисуется вовсе, и в кадре стоит ровная
            // заливка — у Unity по умолчанию тёмно-синяя. Выглядит как
            // «небо плоское и без градиента», и чинить лезут материал,
            // хотя материал ни при чём. Так и вышло 29.08.2026: я час
            // считал бы виноватым шейдер.
            foreach (var cam in Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.clearFlags == CameraClearFlags.Skybox) continue;

                Debug.Log("[IsoRPG] Камера «" + cam.name + "»: очистка была " +
                          cam.clearFlags + ", ставлю Skybox.");

                cam.clearFlags = CameraClearFlags.Skybox;
                EditorUtility.SetDirty(cam);
            }

            // Рассеянный свет берём с неба: иначе тени останутся окрашены
            // под прежнее небо, и подмена будет читаться как ошибка цвета.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            DynamicGI.UpdateEnvironment();

            Debug.Log("[IsoRPG] Небо Synty: " + found[0] +
                      ". Рассеянный свет пересчитан с него. " +
                      "Суток и погоды больше нет — небо статичное.");

            EditorSceneManager.MarkAllScenesDirty();
        }

        /// <summary>
        /// Солнечный диск на небе.
        ///
        /// В наборе Synty солнца нет — только лучи света (SunBeam). Небо у
        /// них градиент, солнце подразумевается за кадром. Но в изометрии
        /// небо занимает верхнюю треть экрана, и пустой градиент читается
        /// как затянутый день.
        ///
        /// Делаем диск сами: плоский круг без освещения, тёплого цвета, на
        /// слое неба и без теней. Ставим ПРОТИВ направления света — туда,
        /// откуда солнце светит, иначе диск и тени разойдутся, и это ловит
        /// глаз мгновенно.
        /// </summary>
        private static void SunDisc(Transform parent)
        {
            var sun = Object.FindObjectsByType<Light>(
                          FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                      .FirstOrDefault(l => l.type == LightType.Directional && l.enabled);

            if (sun == null)
            {
                Debug.LogWarning("[IsoRPG] Направленного света нет — диск ставить не по чему.");
                return;
            }

            var disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            disc.name = "Солнце";
            disc.transform.SetParent(parent, false);

            Object.DestroyImmediate(disc.GetComponent<Collider>());

            // Откуда светит: направление ЛУЧЕЙ — forward источника, значит
            // сам он в противоположной стороне.
            Vector3 dir = -sun.transform.forward;

            disc.transform.localPosition = dir * 240f;
            disc.transform.localRotation = Quaternion.LookRotation(-dir, Vector3.up);
            disc.transform.localScale = Vector3.one * 34f;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader) { name = "Sun_Disc" };

            // Цвет берём у самого солнца и высветляем: диск должен быть ярче
            // неба, иначе он читается пятном, а не источником.
            mat.color = Color.Lerp(sun.color, Color.white, 0.55f);

            AssetDatabase.CreateAsset(mat, "Assets/_Game/Art/Materials/Sun_Disc.mat");
            disc.GetComponent<Renderer>().sharedMaterial = mat;
            disc.GetComponent<Renderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            disc.GetComponent<Renderer>().receiveShadows = false;

            Debug.Log("[IsoRPG] Солнечный диск поставлен по направлению света " +
                      sun.transform.eulerAngles + ", цвет " + mat.color + ".");
        }

        /// <summary>Облачное кольцо на заданной высоте, с медленным вращением.</summary>
        private static void Clouds(Transform parent, string name, float height,
                                   float scale, float speed)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "/PNB_Core/Prefabs/SM_Env_Cloud_Ring_01.prefab");

            if (asset == null)
            {
                Debug.LogWarning("[IsoRPG] Облачного кольца в наборе нет.");
                return;
            }

            var ring = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            ring.name = name;
            ring.transform.localPosition = new Vector3(0f, height, 0f);
            ring.transform.localScale = Vector3.one * scale;
            ring.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var drift = ring.AddComponent<IsoRPG.World.CloudDrift>();
            drift.degreesPerSecond = speed;

            foreach (var r in ring.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            Debug.Log("[IsoRPG] " + name + ": высота " + height +
                      ", вращение " + speed + " град/с (круг за " +
                      (360f / speed / 60f).ToString("0") + " мин).");
        }

        /// <summary>
        /// Вывести купол из-под солнца.
        ///
        /// Шейдер называется SkyboxUnlit, но объявлен освещаемым
        /// (UniversalMaterialType=Lit). Значит солнце светит на купол как на
        /// обычный объект: поперёк неба идёт градиент яркости, а с той
        /// стороны, куда солнце не достаёт, ложится тёмная полоса. Небо от
        /// этого выглядит пыльным и мятым.
        ///
        /// Небо само себе свет. Выносим купол на отдельный слой и убираем
        /// этот слой из маски направленных источников — цвет становится
        /// ровно тем, что нарисовал художник.
        /// </summary>
        private static void Unlight(GameObject dome)
        {
            int layer = EnsureLayer("Sky");

            if (layer < 0) return;

            foreach (var t in dome.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;

            int mask = ~(1 << layer);
            int hushed = 0;

            foreach (var light in Object.FindObjectsByType<Light>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                if ((light.cullingMask & (1 << layer)) == 0) continue;

                light.cullingMask &= mask;
                EditorUtility.SetDirty(light);
                hushed++;
            }

            Debug.Log("[IsoRPG] Купол выведен на слой «Sky» (" + layer +
                      "), убран из маски у источников: " + hushed + ".");
        }

        /// <summary>Найти слой по имени или завести новый в первой свободной ячейке.</summary>
        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var so = new SerializedObject(asset);
            var layers = so.FindProperty("layers");

            // Ячейки с 0 по 7 заняты движком, свои начинаются с восьмой.
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = name;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();

                Debug.Log("[IsoRPG] Заведён слой «" + name + "» в ячейке " + i + ".");
                return i;
            }

            Debug.LogError("[IsoRPG] Свободных ячеек под слой не осталось.");
            return -1;
        }

        /// <summary>
        /// Внести шейдер неба в обязательные для сборки.
        ///
        /// <b>Почему без этого небо есть в редакторе и нет в игре.</b> Unity
        /// кладёт в сборку шейдеры тех материалов, что висят на объектах
        /// сцен. Небо висит не на объекте, а в настройках освещения, и его
        /// шейдер под эту выборку не попадает — его выбрасывают. Материал
        /// остаётся без шейдера, скайбокс не рисуется, и движок заливает
        /// кадр фоном камеры. Наружу это выглядит как «небо плоское».
        ///
        /// 29.08.2026 на этом сгорело четыре круга: я чинил материал,
        /// камеру и порядок сохранения, а сцена всё это время была
        /// настроена верно — щуп показывал небо и очистку Skybox. Разница
        /// была не в сцене, а между редактором и сборкой.
        /// </summary>
        public static void IncludeShader()
        {
            var sky = RenderSettings.skybox;

            if (sky == null || sky.shader == null)
            {
                Debug.LogError("[IsoRPG] Небо не назначено — включать нечего.");
                return;
            }

            var settings = AssetDatabase.LoadAllAssetsAtPath(
                "ProjectSettings/GraphicsSettings.asset")[0];

            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == sky.shader)
                {
                    Debug.Log("[IsoRPG] Шейдер неба уже в обязательных: " + sky.shader.name);
                    return;
                }
            }

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sky.shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Шейдер «" + sky.shader.name +
                      "» внесён в обязательные для сборки. Было " +
                      (list.arraySize - 1) + " шейдеров, стало " + list.arraySize + ".");
        }

        /// <summary>
        /// Что на самом деле лежит в сцене: небо, режим очистки у камер,
        /// рассеянный свет. Меряем, а не предполагаем — на этом уже сгорело
        /// три круга 29.08.2026.
        /// </summary>
        public static void Report()
        {
            var sky = RenderSettings.skybox;

            Debug.Log("[IsoRPG] ЩУП НЕБА. RenderSettings.skybox = " +
                      (sky == null ? "ПУСТО" : sky.name + " (" +
                       AssetDatabase.GetAssetPath(sky) + "), шейдер " +
                       (sky.shader == null ? "НЕТ" : sky.shader.name)) +
                      "; рассеянный свет: " + RenderSettings.ambientMode);

            foreach (var cam in Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Debug.Log("[IsoRPG]   камера «" + cam.name + "»: очистка " +
                          cam.clearFlags + ", фон " + cam.backgroundColor +
                          ", активна " + cam.gameObject.activeInHierarchy +
                          ", в сцене «" + cam.gameObject.scene.name + "»");
            }
        }

        /// <summary>Какие небеса вообще есть в наборе — чтобы выбирать по списку.</summary>
        public static void List()
        {
            var all = AssetDatabase.FindAssets("t:Material Skybox")
                                   .Select(AssetDatabase.GUIDToAssetPath)
                                   .Where(p => p.StartsWith(Folder))
                                   .OrderBy(p => p);

            Debug.Log("[IsoRPG] Небеса Synty в наборе:\n  " + string.Join("\n  ", all));
        }
    }
}
