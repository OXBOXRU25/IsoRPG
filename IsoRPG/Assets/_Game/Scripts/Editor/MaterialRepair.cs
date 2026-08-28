using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Возвращает материалам потерянные текстуры после перевода на URP.
    ///
    /// Беда тихая и оттого дорогая. Перевод меняет шейдер, а текстура при
    /// этом остаётся лежать в СТАРОМ свойстве: у Synty атлас зовётся
    /// `_LeafTex`, `_TunkTex`, `_Texture` — как автор шейдера назвал, так и
    /// лежит. URP ищет `_BaseMap`, не находит ничего и рисует ровным белым.
    ///
    /// Ни ошибки, ни предупреждения: материал исправен, шейдер исправен,
    /// текстура на диске цела. Просто весь набор становится белым, и это
    /// видно только глазами.
    ///
    /// Чинится тем, что старые свойства Unity НЕ выбрасывает — они лежат в
    /// сохранённых свойствах материала. Читаем их напрямую через
    /// SerializedObject, а не спрашиваем шейдер: шейдер про них уже не знает
    /// и вернёт пусто.
    ///
    /// Выбор нужной текстуры — по имени файла, а не по имени свойства.
    /// Имена свойств у каждого автора свои и угадывать их бесполезно, а вот
    /// служебные карты почти всегда честно названы: Normal, Noise, Mask,
    /// Dither, Occlusion. Их исключаем, из остатка берём первую.
    /// </summary>
    public static class MaterialRepair
    {
        /// <summary>Что текстурой цвета быть не может.</summary>
        /// <summary>
        /// Что текстурой цвета быть не может.
        ///
        /// Список короткий намеренно. В первой версии тут было слово «lod», и
        /// оно выкинуло ровно ту текстуру, которая нужна карточкам уровней
        /// детализации: у Synty дальний план подменяется плоской картинкой из
        /// LOD-атласа. В игре это выглядело как белые коробки, мигающие на
        /// расстоянии, — то есть мой же фильтр и создал баг.
        ///
        /// Правило: отсеивать только то, что заведомо служебное по СМЫСЛУ
        /// (карта нормалей, шум, маска), а не по совпадению слова.
        /// </summary>
        private static readonly string[] NotColour =
        {
            "normal", "noise", "mask", "dither", "occlusion", "smoothness",
            "metallic", "height", "emission", "flow", "panning", "snow",
        };

        /// <summary>
        /// Порядок предпочтения по имени свойства — на случай, когда после
        /// отсева осталось несколько кандидатов.
        /// </summary>
        private static readonly string[] Preferred =
        {
            "_MainTex", "_BaseMap", "_Texture", "_TextureSample0",
            "_Albedo", "_Diffuse", "_LeafTex", "_TriplanarAlbedo", "_TopAlbedo",
        };

        [MenuItem("Tools/IsoRPG/Материалы: вернуть потерянные текстуры", priority = 5)]
        public static void Fix()
        {
            var folders = PackCatalog.PresentFolders;

            if (folders.Length == 0)
            {
                Debug.LogWarning("[IsoRPG] Ни одной папки набора не нашлось.");
                return;
            }

            int looked = 0, fixedUp = 0, hopeless = 0;
            var samples = new List<string>();
            var beaten = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Material", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null || material.shader == null) continue;

                looked++;

                // Чиним только то, что реально осталось без цвета.
                if (!material.HasProperty("_BaseMap")) continue;

                var current = material.GetTexture("_BaseMap");

                // Занятый _BaseMap ещё не значит «всё хорошо».
                //
                // Прошлый заход мог положить туда карту нормалей — и тогда
                // материал синий, а починка его пропускает как исправный.
                // Проверяем, что там лежит, и выбиваем негодное.
                if (current != null)
                {
                    var was = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(current))
                              as TextureImporter;

                    if (was == null || was.textureType != TextureImporterType.NormalMap) continue;

                    material.SetTexture("_BaseMap", null);
                }

                var found = Rescue(material);

                if (found == null)
                {
                    hopeless++;

                    // Называем не осиленных поимённо. Число «не вышло у 160»
                    // ничего не даёт: чтобы понять закономерность, нужны
                    // имена — по ним сразу видно, что это, скажем, все
                    // карточки уровней детализации.
                    if (hopeless <= 12)
                        beaten.Add(System.IO.Path.GetFileNameWithoutExtension(path));

                    continue;
                }

                material.SetTexture("_BaseMap", found);

                // Цвет базы выкручиваем в белый: если автор красил материал
                // цветом поверх текстуры, после подстановки атласа он будет
                // умножаться и уводить всё в грязь.
                if (material.HasProperty("_BaseColor"))
                {
                    var tint = material.GetColor("_BaseColor");
                    material.SetColor("_BaseColor", new Color(1f, 1f, 1f, tint.a));
                }

                EditorUtility.SetDirty(material);
                fixedUp++;

                if (samples.Count < 6)
                    samples.Add(System.IO.Path.GetFileNameWithoutExtension(path) +
                                " ← " + found.name);
            }

            int matte = Matte(folders);
            int cut = Cutouts(folders);

            Debug.Log("[IsoRPG] Блеск погашен у " + matte + " материалов.");

            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Прозрачность включена у " + cut + " материалов.");

            Debug.Log("[IsoRPG] Материалы: просмотрено " + looked +
                      ", возвращено текстур " + fixedUp +
                      (hopeless > 0 ? ", не нашлось подходящей у " + hopeless : "") +
                      (samples.Count > 0 ? "\n  починены:\n    " + string.Join("\n    ", samples) : "") +
                      (beaten.Count > 0 ? "\n  не осилены:\n    " + string.Join("\n    ", beaten) : ""));
        }

        /// <summary>
        /// Гасит глянец и отражения — делает материалы матовыми.
        ///
        /// Это третья и, похоже, главная часть той же поломки. Собственные
        /// шейдеры Synty матовые: они рисуют плоский цвет из атласа и всё.
        /// URP/Lit, на который их переводят, по умолчанию ставит глянец 0.5 и
        /// включает отражения окружения — то есть каждый камень начинает
        /// зеркалить небо.
        ///
        /// На картинке это выглядит совершенно не как «блестит»: сцена уходит
        /// в холодную синеву, потому что отражается небо, и понять причину по
        /// виду нельзя. Я дважды искал её в текстурах и в прозрачности, и
        /// оба раза мимо.
        ///
        /// Отражения гасим явным ключевым словом, а не только числом:
        /// нулевой глянец сам по себе отражения не выключает, он лишь делает
        /// их размытыми — а размытое небо это ровно та синева и есть.
        /// </summary>
        private static int Matte(string[] folders)
        {
            int touched = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Material", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null || material.shader == null) continue;
                if (!material.shader.name.StartsWith("Universal Render Pipeline")) continue;

                // Воду и стекло оставляем блестящими — им положено.
                string what = (path + "/" + material.name).ToLowerInvariant();
                if (what.Contains("water") || what.Contains("glass") ||
                    what.Contains("crystal") || what.Contains("ice")) continue;

                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

                if (material.HasProperty("_EnvironmentReflections"))
                    material.SetFloat("_EnvironmentReflections", 0f);

                if (material.HasProperty("_SpecularHighlights"))
                    material.SetFloat("_SpecularHighlights", 0f);

                material.DisableKeyword("_ENVIRONMENTREFLECTIONS_ON");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                material.DisableKeyword("_SPECULARHIGHLIGHTS_ON");
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");

                EditorUtility.SetDirty(material);
                touched++;
            }

            return touched;
        }

        /// <summary>
        /// Включает вырезание по прозрачности там, где текстура с альфой.
        ///
        /// Это вторая половина той же поломки, и она заметнее первой.
        /// Листва, трава и карточки дальнего плана нарисованы прямоугольником
        /// с прозрачным фоном: силуэт задаётся альфа-каналом. После перевода
        /// на URP материал остаётся «непрозрачным», и вместо листьев рисуется
        /// весь прямоугольник целиком — белые коробки, мигающие на
        /// расстоянии, и россыпь белых точек под ногами.
        ///
        /// Решаем ЗАМЕРОМ, а не списком имён: спрашиваем у самой текстуры,
        /// есть ли у неё альфа-канал. Есть — значит силуэт в ней, и вырезание
        /// нужно.
        ///
        /// Берём именно вырезание (cutout), а не полупрозрачность: у неё нет
        /// порядка отрисовки, и шесть тысяч травинок начали бы просвечивать
        /// друг сквозь друга. Вырезание пишет глубину как обычная геометрия.
        /// </summary>
        private static int Cutouts(string[] folders)
        {
            int touched = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Material", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null || material.shader == null) continue;
                if (!material.HasProperty("_BaseMap")) continue;

                var texture = material.GetTexture("_BaseMap");
                if (texture == null) continue;

                bool leafy = Leafy(path, material.name);

                // СНАЧАЛА откат для всех, кто листвой не является.
                //
                // Первая версия включала вырезание всем, у кого в текстуре
                // есть альфа-канал, — и это была ошибка в самой посылке. У
                // Synty в альфе атласа лежит не силуэт, а ГЛЯНЕЦ. Камни
                // получили вырезание и уехали в синий металл, то есть мой
                // «замер» мерил не то, что я думал.
                //
                // Наличие альфы не отвечает на вопрос «нужен ли силуэт».
                // Отвечает на него роль материала: листва, трава, цветы,
                // карточки дальнего плана.
                if (!leafy)
                {
                    if (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f)
                    {
                        material.SetFloat("_AlphaClip", 0f);
                        material.DisableKeyword("_ALPHATEST_ON");
                        material.renderQueue = -1;

                        EditorUtility.SetDirty(material);
                        touched++;
                    }

                    continue;
                }

                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture))
                               as TextureImporter;

                if (importer == null || !importer.DoesSourceTextureHaveAlpha()) continue;

                // Намеренно полупрозрачное не трогаем: стекло, вода.
                if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f) continue;
                if (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f) continue;

                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = 2450;

                EditorUtility.SetDirty(material);
                touched++;
            }

            return touched;
        }

        /// <summary>
        /// Листва ли это — то есть нужен ли материалу вырезанный силуэт.
        ///
        /// Судим по папке и имени, а не по содержимому текстуры. Звучит как
        /// шаг назад от замера, но это не он: альфа-канал отвечает на вопрос
        /// «есть ли тут дополнительные данные», а нам нужен ответ на «что это
        /// за материал». Роль материала записана ровно в его имени и месте —
        /// авторы наборов раскладывают листву и карточки отдельно.
        /// </summary>
        private static bool Leafy(string path, string name)
        {
            string where = (path + "/" + name).ToLowerInvariant();

            return where.Contains("lod_card") || where.Contains("/card")
                || where.Contains("card_")
                || where.Contains("/plants/")
                || where.Contains("leaf") || where.Contains("leaves")
                || where.Contains("grass") || where.Contains("flower")
                || where.Contains("clover") || where.Contains("fern")
                || where.Contains("bush") || where.Contains("plant")
                || where.Contains("vine") || where.Contains("lill")
                || where.Contains("crop") || where.Contains("wind");
        }

        /// <summary>
        /// Достаёт из сохранённых свойств материала ту текстуру, что похожа
        /// на цвет.
        /// </summary>
        private static Texture Rescue(Material material)
        {
            var so = new SerializedObject(material);
            var envs = so.FindProperty("m_SavedProperties.m_TexEnvs");

            if (envs == null) return null;

            var candidates = new List<(string prop, Texture texture)>();

            for (int i = 0; i < envs.arraySize; i++)
            {
                var entry = envs.GetArrayElementAtIndex(i);

                string prop = entry.FindPropertyRelative("first").stringValue;
                var texture = entry.FindPropertyRelative("second.m_Texture")
                                   .objectReferenceValue as Texture;

                if (texture == null) continue;

                string file = texture.name.ToLowerInvariant();
                string name = prop.ToLowerInvariant();

                if (NotColour.Any(bad => file.Contains(bad) || name.Contains(bad))) continue;

                // Спрашиваем саму текстуру, не карта ли она нормалей.
                //
                // Имя врёт: у части наборов карта нормалей названа без слова
                // «normal», и подставленная вместо цвета она красит объект в
                // сине-фиолетовый — это её собственная расцветка, там в
                // каналах лежит направление, а не цвет. Ровно так у нас
                // посинели камни, и я дважды искал причину в блеске.
                //
                // Тип импорта — факт, а не догадка.
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture))
                               as TextureImporter;

                if (importer != null && importer.textureType == TextureImporterType.NormalMap)
                    continue;

                candidates.Add((prop, texture));
            }

            if (candidates.Count == 0) return null;

            foreach (var wanted in Preferred)
            {
                var hit = candidates.FirstOrDefault(c => c.prop == wanted);
                if (hit.texture != null) return hit.texture;
            }

            return candidates[0].texture;
        }
    }
}
