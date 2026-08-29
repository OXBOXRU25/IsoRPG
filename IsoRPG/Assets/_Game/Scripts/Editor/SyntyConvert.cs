using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Перевод материалов Synty на URP по ТОЧНОЙ таблице.
    ///
    /// Это замена всему, что я делал с материалами до сих пор — и замена
    /// вынужденная, оплаченная вечером кругов.
    ///
    /// Предыстория коротко. Официальный конвертер Unity сюда не годится: он
    /// умеет только встроенные шейдеры Unity, а у Synty свои собственные. Мой
    /// прежний перевод это понимал, но искал текстуру ПЕРЕБОРОМ имён —
    /// «попробуем _MainTex, потом _Texture, потом _LeafTex». Перебор
    /// промахивался: где-то подставлялась карта нормалей и объект синел,
    /// где-то не находилось ничего и объект белел.
    ///
    /// Гадать не нужно было ни дня: шейдеры лежат в проекте открытым текстом,
    /// и в них написано, как называется каждое свойство. Читаем и выписываем.
    ///
    /// <b>Главное про порядок.</b> Свойства читаются, пока материал ещё сидит
    /// на СВОЁМ шейдере. Поменяешь шейдер первым — и спрашивать будет уже
    /// некого: Unity сохранит старые значения, но найти нужное среди них
    /// можно только тем самым перебором, с которого всё и началось.
    /// </summary>
    public static class SyntyConvert
    {
        /// <summary>
        /// Что откуда брать. Имена выписаны из самих файлов шейдеров, а не
        /// подобраны: см. PNB_Core/Shaders.
        /// </summary>
        private struct Recipe
        {
            public string albedo;     // главная текстура цвета
            public string albedoAlt;  // запасная, если первой нет
            public string normal;     // карта нормалей
            public string tint;       // цвет-множитель
            public bool leafy;        // нужен ли вырезанный силуэт
        }

        private static readonly Dictionary<string, Recipe> Table =
            new Dictionary<string, Recipe>
        {
            // Деревья и кусты: листва и ствол в одном материале, обе смотрят
            // в общий атлас набора.
            ["SyntyStudios/VegitationShader"] = new Recipe
            {
                albedo = "_LeafTex", albedoAlt = "_TunkTex",
                normal = "_LeafNormalMap", tint = "", leafy = true
            },

            // Дальний план и простые объекты.
            ["SyntyStudios/Basic_LOD_Shader"] = new Recipe
            {
                albedo = "_Albedo", albedoAlt = "",
                normal = "_NormalMap", tint = "_AlbedoColour", leafy = false
            },

            // Камни и скалы: текстура боков и текстура верха. Берём бока —
            // это и есть тело камня; верх у Synty это подмешанная трава или
            // снег, и как основной цвет он даёт не тот объект.
            ["SyntyStudios/TriplanarBasic"] = new Recipe
            {
                albedo = "_Sides", albedoAlt = "_Top",
                normal = "_SidesNormal", tint = "", leafy = false
            },
            ["SyntyStudios/Triplanar_Basic"] = new Recipe
            {
                albedo = "_Sides", albedoAlt = "_Top",
                normal = "_SidesNormal", tint = "", leafy = false
            },
            ["SyntyStudios/Triplanar01"] = new Recipe
            {
                albedo = "_Sides", albedoAlt = "_Top",
                normal = "_SidesNormal", tint = "", leafy = false
            },

            // Упрощённый вариант растительности — им покрыта трава и мелочь.
            // Его отсутствие в таблице и оставляло поле в магенте, при том
            // что деревья уже были в порядке: шейдеры разные, а на вид это
            // одно и то же растение.
            ["SyntyStudios/VegitationShader_Basic"] = new Recipe
            {
                albedo = "_LeafTex", albedoAlt = "_TunkTex",
                normal = "_LeafNormalMap", tint = "", leafy = true
            },
        };

        /// <summary>Эти оставляем как есть: небо, облака, вода живут своей жизнью.</summary>
        private static readonly string[] Skip =
        {
            "SyntyStudios/Skybox", "SyntyStudios/SkyboxUnlit",
            "SyntyStudios/SkyboxUnlitNoFog",
            "SyntyStudios/CloudShader", "SyntyStudios/CloudShaderNoFog",
            "SyntyStudios/WaterShader", "SyntyStudios/WaterScrolling",
            "SyntyStudios/CrystalShader", "SyntyStudios/GlacierIce",
            "SyntyStudios/SulphurPools", "SyntyStudios/Aurora",
            "SyntyStudios/TexturePanner",
            "Synty/SkyDome",
        };

        /// <summary>
        /// Начала имён шейдеров, которые уже под URP и трогать их не надо.
        ///
        /// `Synty/Generic_*` — это Shader Graph, собранный под тот конвейер,
        /// что стоит в проекте. Он и так работает. В прошлый прогон мой
        /// конвертер насчитал их 510 «незнакомых» и напугал меня цифрой, хотя
        /// с ними всё в порядке.
        /// </summary>
        private static readonly string[] AlreadyFine =
        {
            "Synty/Generic", "Universal Render Pipeline/", "Shader Graphs/",
        };

        [MenuItem("Tools/IsoRPG/Материалы: перевести Synty по таблице", priority = 4)]
        public static void Run()
        {
            var folders = PackCatalog.PresentFolders;

            if (folders.Length == 0)
            {
                Debug.LogWarning("[IsoRPG] Ни одной папки набора не нашлось.");
                return;
            }

            var lit = Shader.Find("Universal Render Pipeline/Lit");

            if (lit == null)
            {
                Debug.LogError("[IsoRPG] Нет шейдера URP/Lit — конвейер не URP?");
                return;
            }

            int done = 0, leftAlone = 0, unknown = 0;
            var strangers = new HashSet<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Material", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null || material.shader == null) continue;

                string shader = material.shader.name;

                if (Skip.Contains(shader)) { leftAlone++; continue; }
                if (AlreadyFine.Any(p => shader.StartsWith(p))) { leftAlone++; continue; }

                // Шейдер уже умеет URP — не трогаем, как бы он ни назывался.
                //
                // У Synty одно и то же имя носят две версии шейдера: под
                // старый конвейер и под URP. Отличить их по имени нельзя, а
                // разница решающая: URP-версия сама рисует и листву, и ствол
                // из двух своих текстур, а мой перевод умеет только одну — и
                // ствол при этом пропадает.
                //
                // Спрашиваем сам шейдер, а не его название.
                if (Universal(material.shader)) { leftAlone++; continue; }

                if (!Table.TryGetValue(shader, out var recipe))
                {
                    // Уже переведённое и чужое не трогаем, но незнакомые
                    // шейдеры Synty называем: молчаливый пропуск и есть то,
                    // из-за чего половина набора оставалась белой.
                    if (shader.StartsWith("SyntyStudios") || shader.StartsWith("Synty/"))
                    {
                        strangers.Add(shader);
                        unknown++;
                    }

                    continue;
                }

                // Читаем ДО смены шейдера.
                var albedo = Get(material, recipe.albedo) ?? Get(material, recipe.albedoAlt);
                var normal = Get(material, recipe.normal);

                Color tint = Color.white;

                if (!string.IsNullOrEmpty(recipe.tint) && material.HasProperty(recipe.tint))
                    tint = material.GetColor(recipe.tint);

                material.shader = lit;

                if (albedo != null) material.SetTexture("_BaseMap", albedo);

                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }

                // Цвет-множитель держим белым, если он не задан осмысленно:
                // тёмный множитель поверх атласа уводит всё в грязь.
                material.SetColor("_BaseColor",
                    tint.maxColorComponent < 0.05f ? Color.white : tint);

                // Матовость. У Synty свои шейдеры плоские: блеск и отражения
                // им не нужны, а по умолчанию URP ставит и то, и другое —
                // отчего вся сцена начинает зеркалить небо и синеет.
                material.SetFloat("_Smoothness", 0f);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_EnvironmentReflections", 0f);
                material.SetFloat("_SpecularHighlights", 0f);
                material.DisableKeyword("_ENVIRONMENTREFLECTIONS_ON");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                material.DisableKeyword("_SPECULARHIGHLIGHTS_ON");
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");

                // Силуэт по прозрачности — только листве, и только если в
                // текстуре действительно есть альфа.
                if (recipe.leafy && HasAlpha(albedo))
                {
                    material.SetFloat("_AlphaClip", 1f);
                    material.SetFloat("_Cutoff", 0.5f);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.renderQueue = 2450;
                }

                EditorUtility.SetDirty(material);
                done++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Synty переведены по таблице: " + done +
                      ", оставлено своими " + leftAlone +
                      (unknown > 0 ? ", НЕЗНАКОМЫХ шейдеров " + unknown +
                                     ":\n    " + string.Join("\n    ", strangers) : ""));
        }

        /// <summary>Умеет ли шейдер наш конвейер — по его собственной метке.</summary>
        private static bool Universal(Shader shader)
        {
            var data = ShaderUtil.GetShaderData(shader);

            for (int i = 0; i < data.SubshaderCount; i++)
            {
                var tag = data.GetSubshader(i)
                              .FindTagValue(new UnityEngine.Rendering.ShaderTagId("RenderPipeline"));

                if (tag.name == "UniversalPipeline") return true;
            }

            return false;
        }

        private static Texture Get(Material material, string property)
        {
            if (string.IsNullOrEmpty(property)) return null;
            if (!material.HasProperty(property)) return null;

            return material.GetTexture(property);
        }

        private static bool HasAlpha(Texture texture)
        {
            if (texture == null) return false;

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture))
                           as TextureImporter;

            return importer != null && importer.DoesSourceTextureHaveAlpha();
        }
    }
}
