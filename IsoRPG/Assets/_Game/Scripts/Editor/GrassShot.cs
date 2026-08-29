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
            man.GetComponent<Renderer>().sharedMaterial.color = new Color(0.85f, 0.3f, 0.25f);

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
        }
    }
}
