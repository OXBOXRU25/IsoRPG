using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Переводит материалы покупных наборов на шейдеры Universal.
    ///
    /// Зачем вообще: наборы собирают под встроенный рендер, а у нас
    /// Universal. Шейдера `Standard` в нём нет, и Unity рисует всё, что его
    /// просит, ярко-пурпурным. Со стороны это выглядит как «модели приехали
    /// битыми», хотя с моделями всё в порядке — не хватает одного шейдера.
    ///
    /// Своим скриптом, а не встроенным «конвертером рендер-конвейера»,
    /// по двум причинам. Встроенный ходит по ВСЕМУ проекту и трогает в том
    /// числе наши собственные материалы, а они уже настроены. И он молчит о
    /// том, что именно поменял, — а нам важно видеть список: если материал
    /// не перевёлся, это надо заметить сразу, а не искать потом пурпурное
    /// пятно в углу подземелья.
    /// </summary>
    public static class UrpMaterialFix
    {
        /// <summary>
        /// Где чиним — берём из общего каталога наборов.
        ///
        /// Раньше папки были выписаны здесь руками, и каждый новый набор
        /// приезжал пурпурным до тех пор, пока кто-нибудь не вспомнит
        /// дописать строку именно сюда. Каталог знает, что лежит в
        /// проекте, и отсутствующие папки отсеивает сам.
        ///
        /// Свои материалы по-прежнему не трогаем: в каталоге их нет.
        /// </summary>
        private static string[] Folders => PackCatalog.PresentFolders;

        /// <summary>
        /// Свойства старого шейдера и их имена в новом.
        ///
        /// Universal переименовал почти всё: цвет стал `_BaseColor`, основная
        /// текстура — `_BaseMap`. Просто сменить шейдер мало — материал
        /// потеряет и цвет, и текстуру, и станет ровно белым. Поэтому
        /// значения сначала запоминаем, потом переносим под новыми именами.
        /// </summary>
        private static readonly (string from, string to)[] Colors =
        {
            ("_Color", "_BaseColor"),
            ("_EmissionColor", "_EmissionColor"),
        };

        private static readonly (string from, string to)[] Textures =
        {
            ("_MainTex", "_BaseMap"),

            // Собственные шейдеры наборов держат альбедо под своими
            // именами: у скал Synty это _TriplanarAlbedo, у неба — _Moon_01.
            // Без этих строк перевод даёт ровно белую скалу — формально не
            // пурпурную, но одинаково негодную.
            ("_TriplanarAlbedo", "_BaseMap"),
            ("_TopAlbedo", "_BaseMap"),
            ("_Moon_01", "_BaseMap"),
            ("_Normal", "_BumpMap"),
            ("_TopNormal", "_BumpMap"),
            ("_BumpMap", "_BumpMap"),
            ("_MetallicGlossMap", "_MetallicGlossMap"),
            ("_OcclusionMap", "_OcclusionMap"),
            ("_EmissionMap", "_EmissionMap"),
        };

        [MenuItem("Tools/IsoRPG/Починить пурпурные материалы", priority = 44)]
        public static void Fix()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");

            if (lit == null)
            {
                Debug.LogError("[IsoRPG] Не нашёлся шейдер Universal Render Pipeline/Lit. " +
                               "Проект точно на Universal?");
                return;
            }

            int changed = 0, skipped = 0;
            var missing = new List<string>();
            var fromNames = new List<string>();

            foreach (string folder in Folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    missing.Add(folder);
                    continue;
                }

                // Частицы не наши: у них своё задание `fx-urp`.
                //
                // Перевод на Lit ломает эффекты молча — Lit не читает цвет
                // частицы и рисует её непрозрачной, отчего пламя, искры и дым
                // становятся одинаковыми белыми кляксами. Ни розового цвета,
                // ни ошибки в журнале: 31.08.2026 это нашлось только глазами
                // в игре, уже после того как костёр «поставили».
                if (folder.Contains("ParticleFX"))
                {
                    Debug.Log("[IsoRPG] Пропускаю " + folder +
                              " — эффекты чинит задание «fx-urp», Lit их ломает.");
                    continue;
                }

                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                    if (material == null) continue;

                    // Чиним ТОЛЬКО шейдеры встроенного рендера — те, что в
                    // Universal физически не рисуются. Всё остальное
                    // оставляем как есть.
                    //
                    // Раньше условие было обратным: «не Universal — значит
                    // чиним». Оно работало, пока покупные наборы приезжали на
                    // голом Standard. Новые Synty везут собственный Shader
                    // Graph ("Synty/…"), совместимый с Universal и умеющий
                    // вершинные цвета, свечение и панорамирование, — под
                    // старым условием он попадал под замену, и весь набор
                    // становился ровным пластиком. Пурпурным он при этом не
                    // был, то есть заметить подмену было бы нечем.
                    if (!NeedsFix(material.shader))
                    {
                        skipped++;
                        continue;
                    }

                    string wasShader = material.shader == null ? "(потерян)" : material.shader.name;

                    // Запоминаем ДО смены шейдера: после неё старых свойств
                    // у материала уже нет и спросить их будет не у кого.
                    var savedColors = new Dictionary<string, Color>();
                    var savedTextures = new Dictionary<string, Texture>();
                    var savedOffsets = new Dictionary<string, (Vector2 scale, Vector2 offset)>();

                    foreach (var (from, to) in Colors)
                        if (material.HasProperty(from)) savedColors[to] = material.GetColor(from);

                    foreach (var (from, to) in Textures)
                        if (material.HasProperty(from))
                        {
                            savedTextures[to] = material.GetTexture(from);
                            savedOffsets[to] = (material.GetTextureScale(from),
                                                material.GetTextureOffset(from));
                        }

                    float metallic = material.HasProperty("_Metallic")
                        ? material.GetFloat("_Metallic") : 0f;
                    float smoothness = material.HasProperty("_Glossiness")
                        ? material.GetFloat("_Glossiness") : 0.5f;

                    bool emissive = material.IsKeywordEnabled("_EMISSION");
                    bool cutout = material.IsKeywordEnabled("_ALPHATEST_ON");

                    // Полупрозрачность запоминаем отдельно от вырезания.
                    // Это разные вещи: вырезание оставляет пиксель либо
                    // целиком, либо никак (лохмотья, листва), а прозрачность
                    // смешивает (стекло, вода, световой конус, дым).
                    //
                    // Пока переносилось только вырезание, полупрозрачные
                    // материалы становились глухими: световой луч Elven Realm
                    // превратился в сплошной жёлтый куб на полкадра. Пурпурным
                    // он при этом не был — то есть по признаку «ищем розовое»
                    // поломка не находилась вовсе.
                    bool blended = material.IsKeywordEnabled("_ALPHABLEND_ON") ||
                                   material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") ||
                                   (material.HasProperty("_Mode") && material.GetFloat("_Mode") >= 2f) ||
                                   material.renderQueue >= 3000;

                    material.shader = lit;

                    foreach (var pair in savedColors)
                        if (material.HasProperty(pair.Key)) material.SetColor(pair.Key, pair.Value);

                    foreach (var pair in savedTextures)
                        if (material.HasProperty(pair.Key) && pair.Value != null)
                        {
                            material.SetTexture(pair.Key, pair.Value);
                            var uv = savedOffsets[pair.Key];
                            material.SetTextureScale(pair.Key, uv.scale);
                            material.SetTextureOffset(pair.Key, uv.offset);
                        }

                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);

                    // Свечение и вырезание по альфе — ключевыми словами, иначе
                    // шейдер их не включит, даже если текстура на месте. У
                    // упырей на этом держатся светящиеся глаза, а вырезание —
                    // лохмотья: без него у них по краям чёрная плёнка.
                    if (emissive) material.EnableKeyword("_EMISSION");

                    if (blended)
                    {
                        // Universal держит режим не ключевым словом, а набором
                        // чисел: тип поверхности, режим смешивания, оба
                        // множителя и запись глубины. Выставить одно и забыть
                        // остальные — получить прозрачность, которая рисуется
                        // поверх всего или не рисуется вовсе.
                        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
                        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);

                        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        material.DisableKeyword("_ALPHATEST_ON");
                        material.renderQueue = 3000;
                    }
                    else if (cutout)
                    {
                        material.EnableKeyword("_ALPHATEST_ON");
                        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
                        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
                        material.renderQueue = 2450;
                    }

                    EditorUtility.SetDirty(material);
                    changed++;

                    // Пишем, ЧТО именно перевели: без этого «перевёл 127»
                    // ничего не говорит, а пропущенный чужой шейдер
                    // обнаруживается только пурпурным пятном в игре.
                    if (!fromNames.Contains(wasShader)) fromNames.Add(wasShader);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (fromNames.Count > 0)
                Debug.Log("[IsoRPG] Переведены материалы с шейдеров: " +
                          string.Join(", ", fromNames));

            Debug.Log("[IsoRPG] Переведено на Universal: " + changed +
                      ", уже было: " + skipped +
                      (missing.Count > 0 ? ". Нет папок: " + string.Join(", ", missing) : "."));
        }

        /// <summary>
        /// Нужен ли материалу перевод: его шейдер из встроенного рендера
        /// или потерян вовсе.
        ///
        /// Утерянный шейдер (null) — тоже наш случай: Unity рисует такой
        /// материал тем же пурпурным, и со стороны это неотличимо от
        /// чужого конвейера.
        /// </summary>
        private static bool NeedsFix(Shader shader)
        {
            if (shader == null) return true;

            if (shader.name.StartsWith("Universal Render Pipeline")) return false;

            // Спрашиваем сам шейдер, а не его имя.
            //
            // По именам проверка ошибалась дважды и в обе стороны: сперва
            // переводила родные Shader Graph от Synty (они назывались
            // "Synty/…" и под правило «не Universal» подпадали), потом
            // пропускала их же шейдер скал SyntyStudios_RockTriplanar —
            // написанный на CGPROGRAM, то есть под встроенный рендер, и в
            // Universal просто пурпурный.
            //
            // Тег RenderPipeline у сабшейдера отвечает на вопрос прямо:
            // есть "UniversalPipeline" — шейдер умеет наш конвейер, нет —
            // не умеет, каким бы именем ни назывался.
            var data = ShaderUtil.GetShaderData(shader);

            for (int i = 0; i < data.SubshaderCount; i++)
            {
                var tag = data.GetSubshader(i).FindTagValue(new UnityEngine.Rendering.ShaderTagId("RenderPipeline"));
                if (tag.name == "UniversalPipeline") return false;
            }

            return true;
        }

        /// <summary>Семейства шейдеров встроенного рендера.</summary>
        private static readonly string[] BuiltIn =
        {
            "Legacy Shaders/", "Mobile/", "Nature/", "Particles/",
            "Reflective/", "Self-Illumin/", "Transparent/", "VertexLit"
        };
    }
}
