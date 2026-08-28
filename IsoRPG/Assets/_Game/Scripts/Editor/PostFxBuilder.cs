using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Постобработка: свечение, цветокоррекция, виньетка.
    ///
    /// До сих пор в проекте не было никакой — картинка выводилась «как
    /// посчиталось». Именно поэтому наши факелы и огонь выглядели плоскими
    /// пятнами, а на промо-кадрах наборов горн и зелья светятся: у них
    /// работает Bloom, растекающий яркие пиксели ореолом.
    ///
    /// Что ставим и почему именно это:
    ///
    /// **Bloom** — главное. Всё, что ярче единицы (огонь, свечи, эмиссия
    /// кристаллов), получает ореол. Порог держим чуть выше единицы: опустишь
    /// ниже — засветится вообще всё, включая белую штукатурку, и картинка
    /// поплывёт в молоко.
    ///
    /// **Tonemapping** — обязателен вместе с Bloom. Без него яркие места
    /// просто упираются в белый и выгорают плоскими пятнами; ACES сжимает
    /// яркости по-плёночному, и огонь остаётся огнём, а не белой кляксой.
    ///
    /// **Color Adjustments** — лёгкий контраст и капля насыщенности: у
    /// low-poly палитра плоская по определению, и небольшой контраст ей
    /// идёт.
    ///
    /// **Vignette** — совсем слабая. Она собирает взгляд к центру, где
    /// герой. Сильная виньетка в изометрии сразу читается как «эффект», а
    /// не как свет.
    ///
    /// Профиль сохраняется ассетом, чтобы Павел мог крутить ползунки сам,
    /// не трогая код.
    /// </summary>
    public static class PostFxBuilder
    {
        private const string ProfilePath = "Assets/_Game/Settings/PostFx.asset";

        [MenuItem("Tools/IsoRPG/Постобработка: включить", priority = 55)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            var profile = CreateProfile();

            // Объект Volume в сцене. Ищем существующий, чтобы повторный
            // вызов не плодил копии: два Volume с одним профилем складывают
            // эффекты и дают вдвое больше свечения, чем настроено.
            var existing = GameObject.Find("PostFx");
            var go = existing != null ? existing : new GameObject("PostFx");

            var volume = go.GetComponent<Volume>();
            if (volume == null) volume = go.AddComponent<Volume>();

            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;

            EditorUtility.SetDirty(go);

            // Камере надо разрешить постобработку явно — по умолчанию она
            // выключена, и Volume в сцене остаётся без действия. Симптом
            // обманчивый: настройки есть, профиль назначен, эффекта нет.
            int cameras = 0;

            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var data = camera.GetUniversalAdditionalCameraData();
                if (data == null) continue;

                // Миникарте постобработка не нужна: свечение на карте
                // превращается в кашу, а стоит она столько же.
                if (camera.name.Contains("Minimap")) continue;

                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;

                EditorUtility.SetDirty(camera);
                cameras++;
            }

            Debug.Log("[IsoRPG] Постобработка включена: свечение, тонмаппинг, контраст, " +
                      "виньетка. Камер настроено " + cameras +
                      ". Профиль лежит в " + ProfilePath + " — ползунки там.");
        }

        [MenuItem("Tools/IsoRPG/Постобработка: выключить", priority = 56)]
        public static void Clear()
        {
            var go = GameObject.Find("PostFx");
            if (go != null) Object.DestroyImmediate(go);

            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var data = camera.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = false;
            }

            Debug.Log("[IsoRPG] Постобработка выключена.");
        }

        // ------------------------------------------------------------------

        private static VolumeProfile CreateProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.85f));

            var tone = profile.Add<Tonemapping>(true);
            tone.active = true;
            tone.mode.Override(TonemappingMode.ACES);

            var colour = profile.Add<ColorAdjustments>(true);
            colour.active = true;
            colour.postExposure.Override(0.15f);
            colour.contrast.Override(12f);
            colour.saturation.Override(8f);

            var vignette = profile.Add<Vignette>(true);
            vignette.active = true;
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.6f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            // Возвращаем сам объект, а не результат загрузки: сразу после
            // CreateAsset база ещё не проиндексирована, и LoadAssetAtPath
            // отдаёт пустую ссылку.
            return profile;
        }
    }
}
