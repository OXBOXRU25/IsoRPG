using System.IO;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Рисует текстуру земли.
    ///
    /// Земля до этого была плоскостью, залитой одним цветом. На скриншоте это
    /// читается как дыра: всё вокруг имеет форму и оттенки, а под ногами —
    /// ровная заливка, по которой глазу не за что зацепиться. Заодно пропадает
    /// ощущение движения: идёшь, а фон под тобой не меняется.
    ///
    /// Почему рисуем, а не берём готовую. Фотографическая текстура травы рядом
    /// с низкополигональными моделями смотрится наклейкой — у неё другая
    /// плотность деталей и другой источник света. Нужна стилизованная, в той же
    /// приглушённой палитре, а такую проще сделать, чем найти.
    ///
    /// Камешков в текстуре нет намеренно: две попытки показали, что крупные
    /// читаются как мусор, а мелкие — как белая пыль на изображении. Камень
    /// должен иметь объём и тень, а нарисованное на плоскости пятно их не
    /// имеет. Настоящие камни есть моделями и разбросаны по поляне.
    ///
    /// Почему кодом, а не в редакторе изображений. Цвета земли ещё будут
    /// подбираться под освещение, и каждая правка — это перерисовать и
    /// переимпортировать. Здесь достаточно поменять число и нажать пункт меню.
    /// </summary>
    public static class GroundTextureBuilder
    {
        private const string Folder = "Assets/_Game/Art/Textures";
        private const string TexturePath = Folder + "/T_Ground.png";
        private const string MaterialPath = "Assets/_Game/Art/Materials/M_Ground.mat";

        private const int Size = 1024;

        /// <summary>
        /// Сколько метров занимает один повтор текстуры.
        ///
        /// Двадцать — это результат замера, а не вкус: камера показывает
        /// примерно 32 метра по ширине, поэтому при таком тайле в кадр попадает
        /// полтора повтора. Мельче — начинает рябить и выдавать повторение,
        /// крупнее — пятна становятся больше экрана и текстура снова читается
        /// как заливка.
        /// </summary>
        public const float MetersPerTile = 20f;

        // --- Палитра ---------------------------------------------------------
        //
        // Взята с моделей набора: приглушённая, без чистых тонов. Земля темнее
        // и желтее травы, камни уходят в серо-бежевый.

        private static readonly Color GrassDark = new Color32(0x46, 0x60, 0x2E, 0xFF);
        private static readonly Color GrassMid = new Color32(0x5E, 0x7C, 0x3E, 0xFF);
        private static readonly Color GrassLight = new Color32(0x6F, 0x8D, 0x4A, 0xFF);
        private static readonly Color Dirt = new Color32(0x6A, 0x59, 0x40, 0xFF);
        private static readonly Color DirtLight = new Color32(0x7C, 0x6B, 0x4E, 0xFF);

        [MenuItem("Tools/IsoRPG/Создать текстуру земли", priority = 18)]
        public static void Build()
        {
            var texture = Draw();

            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
            Configure();
            ApplyToMaterial();

            Debug.Log("[IsoRPG] Текстура земли готова: " + TexturePath +
                      ", один повтор — " + MetersPerTile + " м.");
        }

        // ==================================================================

        private static Texture2D Draw()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = PixelAt(x, y);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }

        private static Color PixelAt(int x, int y)
        {
            // Крупные пятна: где трава гуще, а где вытоптано до земли.
            float patches = Fbm(x, y, 3.0f, 3);

            // Мелкая зернистость — она не меняет цвет, только светлоту, иначе
            // земля начинает пестрить и спорит с моделями.
            float grain = Fbm(x, y, 24f, 2);

            // Прожилки: вытянутые светлые полосы, будто трава примята. Даются
            // сдвигом координат по одной оси — дёшево и заметно.
            float streaks = Fbm(x * 0.35f, y * 1.4f, 9f, 2);

            // Трава: от тёмной к средней, светлые прожилки поверх.
            var grass = Color.Lerp(GrassDark, GrassMid,
                                   Mathf.InverseLerp(0.38f, 0.78f, patches));
            grass = Color.Lerp(grass, GrassLight, streaks * 0.45f);

            var dirt = Color.Lerp(Dirt, DirtLight, grain * 0.7f);

            // Переход к земле плавный, но не размытый. Резкая граница делала
            // проплешину похожей на вырезанную ножницами, а слишком широкая
            // зона смешивания превращала её в туманное пятно без формы.
            float dirtiness = Smoothstep(0.44f, 0.34f, patches);
            var colour = Color.Lerp(grass, dirt, dirtiness);

            // Общая вариация светлоты. Диапазон узкий намеренно: широкий
            // превращает землю в мрамор.
            float shade = 0.94f + grain * 0.12f;

            return new Color(colour.r * shade, colour.g * shade, colour.b * shade, 1f);
        }

        /// <summary>
        /// Плавная ступенька: на обоих краях производная нулевая, поэтому
        /// переход не выдаёт себя линией. Края принимаются в любом порядке —
        /// от большего к меньшему тоже.
        /// </summary>
        private static float Smoothstep(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        // --- Шум -------------------------------------------------------------

        /// <summary>Сумма октав. Каждая следующая вдвое мельче и вдвое слабее.</summary>
        private static float Fbm(float x, float y, float frequency, int octaves)
        {
            float sum = 0f;
            float amplitude = 1f;
            float total = 0f;

            for (int i = 0; i < octaves; i++)
            {
                sum += Tileable(x, y, frequency) * amplitude;
                total += amplitude;

                frequency *= 2f;
                amplitude *= 0.5f;
            }

            return sum / total;
        }

        /// <summary>
        /// Шум, который стыкуется сам с собой по краям.
        ///
        /// Обычный PerlinNoise не периодичен, и текстура из него на стыке даёт
        /// видимый шов — а земля повторяется по одиннадцать раз в каждую
        /// сторону, так что шов превратился бы в сетку через всю поляну.
        ///
        /// Приём: берём четыре сэмпла — сам пиксель и его отражения за каждым
        /// краем — и смешиваем с весами, обратными расстоянию до края. У самого
        /// края вес полностью переходит к противоположной стороне, поэтому
        /// значения совпадают.
        /// </summary>
        private static float Tileable(float x, float y, float frequency)
        {
            // Смещение в заведомо положительную область — не украшение,
            // а обязательное условие.
            //
            // Mathf.PerlinNoise у Unity зеркалит отрицательные координаты:
            // значение в точке -x совпадает со значением в +x. А приём
            // бесшовности как раз и берёт сэмплы за левым и нижним краем,
            // то есть в минусе. В итоге текстура вышла симметричной —
            // одинаковые пятна по всем четырём углам, — а от смешивания
            // почти одинаковых значений пропал и контраст: получилось
            // размытое зелёное поле вместо травы.
            //
            // Тысяча взята с запасом: самая высокая частота у нас 24 с двумя
            // октавами, то есть период не превышает 48, и после вычитания
            // координата остаётся далеко в плюсе.
            const float Origin = 1000f;

            float period = frequency;
            float u = x / Size * frequency + Origin;
            float v = y / Size * frequency + Origin;

            float a = Mathf.PerlinNoise(u, v);
            float b = Mathf.PerlinNoise(u - period, v);
            float c = Mathf.PerlinNoise(u, v - period);
            float d = Mathf.PerlinNoise(u - period, v - period);

            // Веса берём от координаты без смещения: оно нужно только
            // для выборки шума, а доля пути до края от него не зависит.
            float wu = (u - Origin) / period;
            float wv = (v - Origin) / period;

            return a * (1f - wu) * (1f - wv)
                 + b * wu * (1f - wv)
                 + c * (1f - wu) * wv
                 + d * wu * wv;
        }

        // --- Импорт и материал ------------------------------------------------

        private static void Configure()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null) return;

            // Repeat обязателен: с Clamp плоскость получит один растянутый
            // повтор и по краям — размазанные полосы в пиксель шириной.
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;

            // Анизотропия — против мыла на дальней части поляны: земля уходит
            // от камеры почти горизонтально, и без неё горизонт превращается
            // в кашу.
            importer.anisoLevel = 8;

            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            importer.SaveAndReimport();
        }

        private static void ApplyToMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (material == null)
            {
                Debug.LogWarning("[IsoRPG] Материал земли ещё не создан — " +
                                 "текстура ляжет при следующей сборке сцены.");
                return;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            material.SetTexture("_BaseMap", texture);

            // Цвет ставим белым: он умножается на текстуру, и прежний зелёный
            // сделал бы её вдвое темнее. Это самая частая ошибка при переходе
            // с заливки на текстуру, и выглядит она как «текстура не легла».
            material.SetColor("_BaseColor", Color.white);

            SandboxSceneBuilder.ApplyGroundTiling(material);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }
    }
}
