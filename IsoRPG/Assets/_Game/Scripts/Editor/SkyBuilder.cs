using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Небо. До сих пор его у нас не было вовсе.
    ///
    /// Камера чистила кадр сплошным цветом дымки. Решение было осмысленным,
    /// пока сцена стояла пустая и смотрели на неё сверху: тогда неба почти не
    /// видно, а ровная заливка честно растворяла дальний край земли. Но
    /// камера переехала за плечо героя, наклон упал до 12–38 градусов, и
    /// верхняя треть кадра теперь — это небо. Пустая белёсая плоскость на
    /// треть экрана читается как незагруженная сцена.
    ///
    /// Ставим настоящее: купленные наборы привезли семь готовых. Берём
    /// первый, чей шейдер жив в нашем конвейере, — проверка обязательна,
    /// потому что старые небеса Synty написаны под встроенный конвейер и в
    /// URP дают лиловый купол на весь экран. Это тот случай, где «не
    /// проверил» видно с первого кадра.
    ///
    /// Цвет тумана подтягиваем к низу неба. Иначе на горизонте получается
    /// ступенька: земля тонет в одной дымке, а небо начинается другой.
    /// </summary>
    public static class SkyBuilder
    {
        /// <summary>
        /// Кандидаты по порядку предпочтения.
        ///
        /// Первым — небо из набора природы: наши деревья и скалы приехали
        /// оттуда же, и палитра у них общая. Дальше эльфийские, они мягче.
        /// </summary>
        private static readonly string[] Candidates =
        {
            "Assets/Synty/PNB_Core/Materials/Skybox_Mat_01.mat",
            "Assets/Synty/PolygonNatureBiomes/PNB_Enchanted_Forest/Materials/Skydome_EnchantedForest_01.mat",
            "Assets/PolygonElvenRealm/Materials/Sky/Skybox_01.mat",
            "Assets/PolygonElvenRealm/Materials/Sky/Skybox_02.mat",
            "Assets/PolygonElvenRealm/Materials/Sky/Skybox_03.mat",
            "Assets/Synty/PolygonDungeonRealms/Materials/Misc/SimpleSky_01.mat",
            "Assets/Synty/PolygonGeneric/Materials/Skybox_01.mat",
        };

        /// <summary>
        /// Купола неба из наборов, по порядку предпочтения.
        ///
        /// Первым — из набора природы: наши деревья и скалы приехали оттуда
        /// же, палитра общая. Это НЕ материалы неба, а меши: большая сфера с
        /// текстурой изнутри, которую ставят в сцену. Так Synty и задумали, и
        /// именно так выглядят их примеры.
        /// </summary>
        private static readonly string[] Domes =
        {
            "Assets/Synty/PNB_Core/Prefabs/SM_Env_Skydome_01.prefab",
            "Assets/PolygonElvenRealm/Prefabs/Environment/SM_Env_Skydome_01.prefab",
            "Assets/PolygonElvenRealm/Prefabs/Environment/SM_Env_Skydome_02.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Generic/SM_SkyDome_01.prefab",
        };

        private const string DomeName = "SkyDome";

        /// <summary>
        /// Ставить ли купол из набора вместо простого неба.
        ///
        /// По умолчанию НЕТ, и это решение по порядку работ, а не по вкусу.
        /// Купол Enchanted Forest даёт чёрный верх, красную полосу и серый
        /// провал между своим краем и горизонтом — его надо разбирать
        /// отдельно, с его материалом и градиентом. Небо у нас слой
        /// четвёртый; пока идут земля и бой, оно обязано быть просто
        /// правильным и не мешать.
        /// </summary>
        public static bool UseDome = false;

        [MenuItem("Tools/IsoRPG/Небо: поставить", priority = 58)]
        public static void Apply()
        {
            if (UseDome && PutDome()) return;

            // Купол убираем, если он остался от прошлой сборки: два неба
            // разом дают ровно ту картинку, которую мы и чиним.
            var stale = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                              .FirstOrDefault(g => g.name == DomeName);

            if (stale != null) Object.DestroyImmediate(stale);

            var sky = Pick();

            if (sky == null)
            {
                Debug.LogError("[IsoRPG] Ни одно небо не подошло — все шейдеры " +
                               "мертвы в URP. Кадр остаётся с заливкой.");
                return;
            }

            RenderSettings.skybox = sky;

            // Рассеянный свет НЕ берём от неба.
            //
            // Соблазн большой: небо даёт теневым сторонам свой оттенок, и это
            // красиво. Но связка «свет зависит от неба» стоила нам погасшей
            // сцены: небо оказалось сломанным, и вместе с ним ушёл весь
            // рассеянный свет, а следом пропали и тени — не потому что их
            // выключили, а потому что стало нечего затенять.
            //
            // Поэтому свет держим своим и явным. Три цвета — те самые, что
            // стояли в сцене до всех правок: холодный верх, нейтральная
            // середина, тёмный тёплый низ. Небо теперь отвечает только за то,
            // что видно над горизонтом, и сломать им освещение больше нельзя.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.353f, 0.455f, 0.651f);
            RenderSettings.ambientEquatorColor = new Color(0.384f, 0.388f, 0.439f);
            RenderSettings.ambientGroundColor = new Color(0.200f, 0.188f, 0.180f);
            RenderSettings.ambientIntensity = 1f;

            var camera = Camera.main;

            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                EditorUtility.SetDirty(camera);
            }

            // Туман подтягиваем к низу неба.
            //
            // Иначе на горизонте ступенька: земля тонет в дымке одного цвета,
            // а небо начинается другим. Именно эту ступеньку заказчик и
            // назвал «серое пространство между небом и землёй».
            RenderSettings.fogColor = new Color(0.62f, 0.68f, 0.78f);

            DynamicGI.UpdateEnvironment();

            Debug.Log("[IsoRPG] Небо поставлено: " + sky.name +
                      " (шейдер " + sky.shader.name + "). Рассеянный свет теперь от него.");
        }

        /// <summary>
        /// Первое небо с живым шейдером.
        ///
        /// «Живой» — значит шейдер загрузился и это не заглушка ошибки. У
        /// материала со сломанным шейдером Unity подставляет розовый и не
        /// говорит ни слова, поэтому спрашиваем сами.
        /// </summary>
        /// <summary>
        /// Ставит купол неба и вешает на него слежение за камерой.
        ///
        /// Купол огромен и рисуется изнутри, поэтому ему нужны три вещи,
        /// каждая из которых по отдельности не очевидна:
        ///
        /// 1. <b>Не отбрасывать и не принимать тени.</b> Сфера радиусом в
        ///    полкилометра, попавшая в карту теней, съедает её целиком — все
        ///    остальные тени становятся квадратными или пропадают.
        /// 2. <b>Не иметь коллайдера.</b> Иначе герой упирается в небо, а
        ///    навигация вырезает всю карту разом: мы ровно это проходили с
        ///    деревьями.
        /// 3. <b>Ездить за камерой.</b> Небо, оставшееся позади, — это уже
        ///    декорация, а не небо.
        /// </summary>
        private static bool PutDome()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == DomeName);

            if (old != null) Object.DestroyImmediate(old);

            foreach (var path in Domes)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;

                var dome = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                dome.name = DomeName;
                dome.transform.position = Vector3.zero;

                // Размер считаем от собственного размера купола, а не ставим
                // множитель на глаз.
                //
                // «Восемь» в прошлый раз означало «в восемь раз больше, чем
                // он есть», а насколько он есть — я не смотрел. Вышло за
                // километр, при дальней плоскости камеры в девяносто метров.
                // Небо просто отсекалось.
                //
                // Целимся в радиус 800: заведомо дальше любой видимой
                // геометрии (карта 230 метров) и заведомо ближе дальней
                // плоскости, которую камера держит на 1500.
                const float WantedRadius = 800f;

                float ownRadius = DomeRadius(dome);

                if (ownRadius > 0.01f)
                    dome.transform.localScale = Vector3.one * (WantedRadius / ownRadius);

                foreach (var renderer in dome.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                foreach (var collider in dome.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                if (dome.GetComponent<IsoRPG.World.SkyDomeFollow>() == null)
                    dome.AddComponent<IsoRPG.World.SkyDomeFollow>();

                // Камера чистит цветом, а не небом: купол сам закрывает весь
                // обзор, и плоское небо за ним всё равно не видно. Цвет
                // оставляем цветом дымки — он проглянет только в щели, если
                // камера всё-таки выйдет за купол.
                var camera = Camera.main;

                if (camera != null)
                {
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color32(0xB6, 0xBA, 0xA8, 0xFF);
                    EditorUtility.SetDirty(camera);
                }

                RestoreAmbient();

                // Туман купола не касается.
                //
                // Туман у нас линейный, 34–96 метров: всё, что дальше, красится
                // его цветом целиком. Купол стоит на восьмистах — то есть без
                // этой строки он был бы ровной заливкой цвета тумана, и
                // выглядело бы это в точности как «серое однотонное небо».
                foreach (var renderer in dome.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        if (material != null) material.DisableKeyword("_FOG");

                foreach (var renderer in dome.GetComponentsInChildren<Renderer>(true))
                {
                    // Рисуем купол ПЕРВЫМ, до всей геометрии: он фон, и
                    // спорить за глубину ему не с чем.
                    foreach (var material in renderer.sharedMaterials)
                        if (material != null) material.renderQueue = 1000;
                }

                Debug.Log("[IsoRPG] Небо: купол " + asset.name +
                          " из " + path.Split('/')[2] +
                          ", радиус " + WantedRadius + " м (свой был " +
                          ownRadius.ToString("0") + "), ездит за камерой, " +
                          "теней не даёт, туманом не красится.");

                return true;
            }

            return false;
        }

        /// <summary>Собственный радиус купола по нарисованным границам.</summary>
        private static float DomeRadius(GameObject dome)
        {
            var renderers = dome.GetComponentsInChildren<Renderer>(true)
                                .Where(r => !(r is ParticleSystemRenderer))
                                .ToArray();

            if (renderers.Length == 0) return 0f;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            return Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        /// <summary>
        /// Свой рассеянный свет — тремя цветами, как было до всех правок.
        /// </summary>
        private static void RestoreAmbient()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.353f, 0.455f, 0.651f);
            RenderSettings.ambientEquatorColor = new Color(0.384f, 0.388f, 0.439f);
            RenderSettings.ambientGroundColor = new Color(0.200f, 0.188f, 0.180f);
            RenderSettings.ambientIntensity = 1f;
        }

        /// <summary>
        /// Материал неба. Требование к нему ровно одно, и оно жёсткое:
        /// шейдер обязан быть ИЗ СЕМЕЙСТВА Skybox.
        ///
        /// Первая версия проверяла только «работает ли шейдер в URP» — и
        /// пропустила `Synty/SkyDome`. Он в URP работает прекрасно, но он
        /// написан для КУПОЛА-МЕША: рисует внутреннюю поверхность сферы,
        /// стоящей в сцене. Материалом неба его ставить нельзя — Unity
        /// натянет его на свой куб по чужим координатам, и получится то, что
        /// увидел заказчик: чёрный верх и красная полоса у горизонта.
        ///
        /// Хуже, что за небом потянулся свет: рассеянное освещение бралось
        /// «от неба», а небо было чёрным. Сцена погасла целиком. Урок:
        /// **проверять надо не «заводится ли», а «то ли это вообще».**
        /// </summary>
        private static Material Pick()
        {
            foreach (var path in Candidates)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null || material.shader == null) continue;

                string name = material.shader.name;

                if (name.StartsWith("Skybox/")) return material;

                Debug.Log("[IsoRPG] Небо " + material.name + " пропущено: шейдер " +
                          name + " не для материала неба (скорее всего купол-меш).");
            }

            return Procedural();
        }

        /// <summary>
        /// Своё процедурное небо — на случай, если готового не нашлось.
        ///
        /// Шейдер `Skybox/Procedural` встроен в Unity и работает в любом
        /// конвейере: он рисует градиент от горизонта к зениту, солнце берёт
        /// с направленного света сцены. Стилизованному лоу-поли это подходит
        /// лучше фотографического неба — крупные плоские заливки, без
        /// облаков, которые спорили бы с гранёными кронами.
        ///
        /// Числа подобраны под нашу закатную сцену: небо приглушённо-голубое,
        /// плотность атмосферы выше единицы (тогда у горизонта появляется
        /// тёплая дымка), земля цвета нашей травы — от неё идёт отражённый
        /// свет снизу.
        /// </summary>
        private static Material Procedural()
        {
            const string path = "Assets/_Game/Art/Materials/Sky_Procedural.mat";

            var made = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (made == null)
            {
                var shader = Shader.Find("Skybox/Procedural");

                if (shader == null)
                {
                    Debug.LogError("[IsoRPG] Нет даже встроенного Skybox/Procedural.");
                    return null;
                }

                made = new Material(shader);

                System.IO.Directory.CreateDirectory("Assets/_Game/Art/Materials");
                AssetDatabase.CreateAsset(made, path);
            }

            made.SetFloat("_SunSize", 0.04f);
            made.SetFloat("_SunSizeConvergence", 5f);
            made.SetFloat("_AtmosphereThickness", 1.35f);
            made.SetColor("_SkyTint", new Color(0.52f, 0.62f, 0.78f));
            made.SetColor("_GroundColor", new Color(0.35f, 0.38f, 0.28f));
            made.SetFloat("_Exposure", 1.25f);

            EditorUtility.SetDirty(made);
            AssetDatabase.SaveAssets();

            return made;
        }
    }
}
