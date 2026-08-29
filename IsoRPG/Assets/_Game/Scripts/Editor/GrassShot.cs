using System.IO;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Снимок куста травы рядом с эталоном роста — чтобы размер читался глазом.
    ///
    /// Число «высота 2.1 м» само по себе ничего не говорит: рядом с человеком
    /// это заросли по грудь, а на кадре с высоты птичьего полёта — коврик.
    /// Поэтому ставим рядом капсулу ростом 1.8 м и снимаем сбоку, с уровня
    /// земли: так видно и высоту, и ширину, и насколько куст утоплен.
    /// </summary>
    public static class GrassShot
    {
        private const string Prefab =
            "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Grass_Tall_Clump_04.prefab";

        private const string Out = "D:/GAME Ai/shots/grass-size.png";
        private const int W = 1000, H = 700;

        /// <summary>
        /// Снять демо-сцену набора его же камерой.
        ///
        /// Нужен как ЭТАЛОН. Когда наш щуп выдаёт кислотные цвета, по одному
        /// кадру не понять, сломали мы что-то у себя или набор так нарисован.
        /// Демо-сцена — авторская: свет, камера и материалы там те, что задумал
        /// художник. Совпало — цвета набора; не совпало — виноват наш щуп.
        /// </summary>
        /// <summary>Демо-сцена лугового биома — та, с которой сняты все числа.</summary>
        [MenuItem("Tools/IsoRPG/Щуп: демо-сцена луга", priority = 52)]
        public static void MeadowDemo()
        {
            Demo("Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Scene/Demo.unity",
                 "D:/GAME Ai/shots/meadow-demo.png");
        }

        [MenuItem("Tools/IsoRPG/Щуп: демо-сцена Зачарованного леса", priority = 52)]
        public static void EnchantedDemo()
        {
            Demo("Assets/PolygonNatureBiomes/PNB_Enchanted_Forest/Scene/Demo_URP.unity",
                 "D:/GAME Ai/shots/enchanted-demo.png");
        }

        private static void Demo(string scene, string outPath)
        {

            if (!File.Exists(scene))
            {
                Debug.LogError("[IsoRPG] Демо-сцены нет: " + scene);
                return;
            }

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                scene, UnityEditor.SceneManagement.OpenSceneMode.Single);

            var cam = Object.FindFirstObjectByType<Camera>();

            if (cam == null)
            {
                Debug.LogError("[IsoRPG] В демо-сцене нет камеры — снимать нечем.");
                return;
            }

            const int dw = 1600, dh = 900;

            var rt = new RenderTexture(dw, dh, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            var prevTarget = cam.targetTexture;

            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var shot = new Texture2D(dw, dh, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, dw, dh), 0, 0);
            shot.Apply();

            RenderTexture.active = prev;
            cam.targetTexture = prevTarget;

            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllBytes(outPath, shot.EncodeToPNG());

            Debug.Log("[IsoRPG] Демо-сцена набора снята камерой «" + cam.name +
                      "». Кадр: " + outPath);

            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(shot);
        }

        /// <summary>
        /// Померить пачку префабов, прежде чем сеять.
        ///
        /// Дважды за вечер я добавлял виды, не измерив их: плоские коврики
        /// оказались лежащими на земле плоскостями, а «почвопокровные» —
        /// зарослями выше героя. Число дешевле круга пересева.
        /// </summary>
        /// <summary>
        /// Великаны в ряд рядом с фигурой роста человека.
        ///
        /// Число «44 метра» ничего не говорит глазу. Ряд, где рядом стоит
        /// человек, отвечает на вопрос «поместится ли такое в нашем мире»
        /// одним взглядом.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Щуп: великаны в ряд", priority = 51)]
        public static void Giants()
        {
            // Биом и имя: великаны живут в Зачарованном лесу, а два луговых
            // дерева стоят в ряду КОНТРОЛЬНЫМ образцом. Луговые материалы у
            // нас заведомо рисуются верно, поэтому один кадр отвечает сразу
            // на два вопроса: как выглядят великаны и не сломан ли сам щуп.
            string[] names =
            {
                "PNB_Enchanted_Forest/SM_Env_Tree_Giant_01",
                "PNB_Enchanted_Forest/SM_Env_Tree_Giant_02",
                "PNB_Enchanted_Forest/SM_Env_Tree_Portal_01",
                "PNB_Enchanted_Forest/SM_Env_Tree_Large_01",
                "PNB_Enchanted_Forest/SM_Env_Tree_Large_02",
                "PNB_Enchanted_Forest/SM_Env_Tree_House_01",
                "PNB_Meadow_Forest/SM_Env_Tree_Meadow_01",
                "PNB_Meadow_Forest/SM_Env_Tree_Birch_01",
            };

            var root = new GameObject("ЩУП_ВЕЛИКАНЫ");

            // Щуп ставим ЗА пределами мира: задание гонится в открытой арене,
            // а камера тут отъезжает на сто сорок метров и захватывает
            // террейн с травой фоном. Карта 400 м, поэтому три тысячи —
            // заведомо чистое место.
            const float Away = 3000f;

            float x = 0f, tallest = 0f;

            foreach (var entry in names)
            {
                int slash = entry.IndexOf('/');
                string biome = entry.Substring(0, slash);
                string name = entry.Substring(slash + 1);

                var guids = AssetDatabase.FindAssets(name + " t:Prefab",
                            new[] { "Assets/PolygonNatureBiomes/" + biome });

                if (guids.Length == 0)
                {
                    Debug.LogWarning("[IsoRPG] Не найден префаб " + entry);
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                            AssetDatabase.GUIDToAssetPath(guids[0]));

                if (asset == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                go.transform.SetParent(root.transform);

                var rs = go.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;

                var bb = rs[0].bounds;
                foreach (var r in rs) bb.Encapsulate(r.bounds);

                // Ставим впритык друг к другу по ширине кроны, чтобы ряд не
                // разъезжался и всё влезло в кадр.
                x += bb.size.x * 0.5f + 4f;
                go.transform.position = new Vector3(x, -bb.min.y, 0f);
                x += bb.size.x * 0.5f;

                if (bb.size.y > tallest) tallest = bb.size.y;

                Debug.Log("[IsoRPG]   " + name + ": высота " + bb.size.y.ToString("0.0") +
                          " м, крона " + bb.size.x.ToString("0.0") + " × " +
                          bb.size.z.ToString("0.0") + " м");
            }

            var man = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            man.transform.SetParent(root.transform);
            man.transform.localScale = new Vector3(0.45f, 0.9f, 0.45f);
            man.transform.position = new Vector3(-8f, 0.9f, 0f);

            // Свой материал, а не общий.
            //
            // `sharedMaterial.color` у примитива красит ВСТРОЕННУЮ болванку
            // Unity — ту же самую, что стоит на всех остальных примитивах
            // редактора. Щуп ради одной красной капсулы перекрашивал бы всё,
            // и на соседнем кадре это выглядело бы необъяснимо.
            var manMat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.9f, 0.25f, 0.2f),
            };

            man.GetComponent<Renderer>().sharedMaterial = manMat;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(x * 0.5f, 0f, 0f);
            floor.transform.localScale = Vector3.one * 30f;

            var lightGo = new GameObject("ЩУП_СВЕТ");
            lightGo.transform.SetParent(root.transform);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightGo.transform.rotation = Quaternion.Euler(42f, 150f, 0f);

            var camGo = new GameObject("ЩУП_КАМЕРА");
            camGo.transform.SetParent(root.transform);
            var cam = camGo.AddComponent<Camera>();

            const int gw = 2400, gh = 1000;

            // Кадр считаем по ДВУМ размерам ряда, а не по высоте самого
            // рослого. Кроны великанов по сорок метров, ряд вытягивается на
            // полторы сотни, и рамка «по высоте» обрезала бы половину.
            const float LeftEdge = -12f;

            float rowWidth = x - LeftEdge + 8f;
            float aspect = (float)gw / gh;
            float size = Mathf.Max(tallest * 0.58f, rowWidth * 0.5f / aspect);

            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.transform.position = new Vector3((LeftEdge + x) * 0.5f, size * 0.95f, -140f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.60f, 0.72f, 0.86f);

            // Дальняя граница вплотную за рядом: даже если щуп окажется рядом
            // с миром, фон останется ровным цветом.
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 320f;

            root.transform.position = new Vector3(Away, 0f, Away);

            var rt = new RenderTexture(gw, gh, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };

            // Два кадра одного ряда: как есть и с выключенным пакетированием
            // материалов (SRP Batcher). Цвет менялся от прогона к прогону у
            // одних и тех же деревьев — так ведёт себя мусор в постоянном
            // буфере материала, а не рисунок художника. Пакетирование —
            // ровно тот механизм, который этот буфер собирает, поэтому один
            // прогон с ним и без него называет виновного без догадок.
            Snap(cam, rt, gw, gh, "D:/GAME Ai/shots/giants.png");

            bool batching = UnityEngine.Rendering.GraphicsSettings
                            .useScriptableRenderPipelineBatching;

            UnityEngine.Rendering.GraphicsSettings
                       .useScriptableRenderPipelineBatching = false;

            Snap(cam, rt, gw, gh, "D:/GAME Ai/shots/giants-nobatch.png");

            UnityEngine.Rendering.GraphicsSettings
                       .useScriptableRenderPipelineBatching = batching;

            Debug.Log("[IsoRPG] Великаны сняты, наибольшая высота " +
                      tallest.ToString("0.0") + " м, ряд " +
                      (x + 12f).ToString("0") + " м в длину. Два кадра: " +
                      "giants.png и giants-nobatch.png");

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(manMat);
        }

        /// <summary>Снять кадр камерой в файл.</summary>
        private static void Snap(Camera cam, RenderTexture rt, int w, int h, string path)
        {
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var shot = new Texture2D(w, h, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            shot.Apply();

            RenderTexture.active = prev;
            cam.targetTexture = null;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, shot.EncodeToPNG());

            Object.DestroyImmediate(shot);
        }

        [MenuItem("Tools/IsoRPG/Щуп: размеры хвойных", priority = 50)]
        public static void Pines()
        {
            string[] list =
            {
                "SM_Env_Pine_01", "SM_Env_Pine_02", "SM_Env_Pine_03",
                "SM_Env_Pine_04", "SM_Env_Pine_05", "SM_Env_Pine_NoLeaves_01",
                "SM_Env_Bush_01", "SM_Env_Bush_Flower_01", "SM_Env_Grass_01",
                "SM_Env_Flowers_01",
            };

            // Диковины Зачарованного леса: великаны, дерево-портал, дерево-дом.
            string[] fromEnchanted =
            {
                "SM_Env_Tree_Giant_01", "SM_Env_Tree_Giant_02",
                "SM_Env_Tree_Portal_01", "SM_Env_Tree_House_01",
                "SM_Env_Tree_Large_01", "SM_Env_Fern_Tree_01",
                "SM_Env_Mushroom_01", "SM_Env_Mushroom_Small_Group_01",
                "SM_Env_Moss_Lumps_01", "SM_Env_Undergrowth_Fern_01",
            };

            Measure(list, "Assets/PolygonNatureBiomes/PNB_Alpine_Mountain");
            Measure(fromEnchanted, "Assets/PolygonNatureBiomes/PNB_Enchanted_Forest");
        }

        private static void Measure(string[] list, string folder)
        {

            foreach (var name in list)
            {
                var guids = AssetDatabase.FindAssets(name + " t:Prefab",
                            new[] { folder });

                if (guids.Length == 0)
                {
                    Debug.LogWarning("[IsoRPG] Не нашёлся: " + name);
                    continue;
                }

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(
                         AssetDatabase.GUIDToAssetPath(guids[0]));

                if (go == null) continue;

                var mf = go.GetComponentInChildren<MeshFilter>();

                if (mf == null || mf.sharedMesh == null)
                {
                    Debug.LogWarning("[IsoRPG] Меша нет: " + name);
                    continue;
                }

                var b = mf.sharedMesh.bounds;

                Debug.Log("[IsoRPG] " + name + ": высота " + b.size.y.ToString("0.00") +
                          " м, ширина " + b.size.x.ToString("0.00") + " x " +
                          b.size.z.ToString("0.00") + " м (рост героя 1.80).");
            }
        }

        [MenuItem("Tools/IsoRPG/Щуп: размер куста травы", priority = 49)]
        public static void Shoot()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);

            if (asset == null)
            {
                Debug.LogError("[IsoRPG] Префаба травы нет: " + Prefab);
                return;
            }

            var root = new GameObject("ЩУП_ТРАВА");

            var bush = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            bush.transform.SetParent(root.transform);
            bush.transform.position = Vector3.zero;

            // Габариты считаем по всем рендерерам: точка отсчёта префаба
            // ничего не говорит о том, где у куста верх и низ.
            var rs = bush.GetComponentsInChildren<Renderer>(true);
            var box = rs[0].bounds;
            foreach (var r in rs) box.Encapsulate(r.bounds);

            // Эталон роста: капсула 1.8 м. Примитив высотой 2 при масштабе
            // 0.9 даёт ровно 1.8.
            var man = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            man.transform.SetParent(root.transform);
            man.transform.localScale = new Vector3(0.45f, 0.9f, 0.45f);
            man.transform.position = new Vector3(1.6f, 0.9f, 0f);

            // Свой материал: `sharedMaterial` у примитива — общая болванка
            // Unity, покраска её красит все примитивы редактора разом.
            var manMat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.85f, 0.3f, 0.25f),
            };

            man.GetComponent<Renderer>().sharedMaterial = manMat;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.SetParent(root.transform);
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = Vector3.one * 2f;

            var lightGo = new GameObject("ЩУП_СВЕТ");
            lightGo.transform.SetParent(root.transform);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            lightGo.transform.rotation = Quaternion.Euler(38f, 140f, 0f);

            var camGo = new GameObject("ЩУП_КАМЕРА");
            camGo.transform.SetParent(root.transform);
            var cam = camGo.AddComponent<Camera>();

            // Снимаем сбоку и с уровня пояса: вид сверху скрадывает высоту,
            // а ради неё всё и затевалось.
            float span = Mathf.Max(box.size.y, 2f) * 0.75f;

            cam.orthographic = true;
            cam.orthographicSize = span;
            cam.transform.position = new Vector3(0.8f, span * 0.85f, -6f);
            cam.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.72f, 0.85f);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;

            var shot = new Texture2D(W, H, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            shot.Apply();

            RenderTexture.active = previous;
            cam.targetTexture = null;

            Directory.CreateDirectory(Path.GetDirectoryName(Out));
            File.WriteAllBytes(Out, shot.EncodeToPNG());

            Debug.Log("[IsoRPG] Куст «" + asset.name + "»: высота " +
                      box.size.y.ToString("0.00") + " м, ширина " +
                      box.size.x.ToString("0.00") + " x " + box.size.z.ToString("0.00") +
                      " м. Точка отсчёта выше низа на " +
                      (0f - box.min.y).ToString("0.00") + " м. Рядом эталон 1.80 м. Кадр: " + Out);

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(shot);
            Object.DestroyImmediate(manMat);
        }
    }
}
