using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает модель магического снаряда.
    ///
    /// В наборе KayKit снарядов нет вообще — только стрелы и болты. Стрела из
    /// посоха выглядит ошибкой, поэтому сгусток делаем сами: сфера, свой
    /// светящийся материал и точечный источник.
    ///
    /// Свет здесь не украшение. Летящий шар без него читается как шарик, а с
    /// ним — как то, что подсвечивает стены по дороге, и разница видна на
    /// вечерней локации сразу.
    /// </summary>
    public static class SpellBoltBuilder
    {
        private const string Folder = "Assets/_Game/Art/Effects";
        private const string PrefabPath = Folder + "/SpellBolt.prefab";
        private const string MaterialPath = Folder + "/M_SpellBolt.mat";

        private static readonly Color BoltColor = new Color(0.62f, 0.36f, 1f);

        [MenuItem("Tools/IsoRPG/Собрать магический снаряд", priority = 19)]
        public static GameObject Build()
        {
            EnsureFolder(Folder);

            var material = BuildMaterial();

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SpellBolt";
            go.transform.localScale = Vector3.one * 0.34f;

            // Коллайдер снимаем: попадание считает сам снаряд по расстоянию,
            // а физическая сфера цеплялась бы за стены и за самого стрелка.
            Object.DestroyImmediate(go.GetComponent<Collider>());

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightGo = new GameObject("Glow", typeof(Light));
            lightGo.transform.SetParent(go.transform, false);

            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = BoltColor;
            light.intensity = 3.2f;
            light.range = 5.5f;

            // Тени от снаряда не нужны: он живёт полсекунды, а теневая карта
            // стоит столько же, сколько у факела, который горит всю игру.
            light.shadows = LightShadows.None;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] Магический снаряд собран: " + PrefabPath);
            return prefab;
        }

        public static GameObject Load()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            return prefab != null ? prefab : Build();
        }

        private static Material BuildMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            var material = new Material(shader);
            material.name = "M_SpellBolt";

            // Unlit намеренно: сгусток должен светиться сам, а не зависеть от
            // того, стоит ли он в тени стены.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", BoltColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", BoltColor);

            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", BoltColor * 2.4f);

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = folder.Substring(0, folder.LastIndexOf('/'));
            string leaf = folder.Substring(folder.LastIndexOf('/') + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
