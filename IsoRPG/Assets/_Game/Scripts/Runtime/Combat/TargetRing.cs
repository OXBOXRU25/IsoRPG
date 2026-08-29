using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Кольцо под ногами выбранной цели.
    ///
    /// В WoW выбранный противник обведён кругом на земле, и это не
    /// украшение: панель цели наверху отвечает на вопрос «кто выбран», а
    /// кольцо — на вопрос «который из них», когда перед тобой четверо
    /// одинаковых скелетов. Без кольца в бою приходится сверять полоску
    /// здоровья на панели с полосками над головами — и это ровно та возня,
    /// из-за которой бой ощущается неудобным, хотя механика в порядке.
    ///
    /// Цвет говорит об отношении: красное — враг, зелёное — свой. Одного
    /// взгляда под ноги хватает, чтобы понять, ударит ли нажатие способности
    /// или вылечит.
    ///
    /// Кольцо рисуется картинкой, нарисованной кодом. Отдельный файл заводить
    /// незачем: круг с мягким краем — это два десятка строк, зато он не
    /// зависит от набора и не потеряется при переезде на другой арт.
    /// </summary>
    public sealed class TargetRing : MonoBehaviour
    {
        [Tooltip("Ширина пятна относительно ширины цели. 1 — ровно по модели.")]
        public float Scale = 0.85f;

        [Tooltip("На сколько приподнято над землёй, чтобы не спорить с полом.")]
        public float Lift = 0.06f;

        private TargetSelector selector;
        private Transform ring;
        private MeshRenderer ringRenderer;
        private Targetable shown;

        // Тусклее и прозрачнее прежнего: пятно под ногами подсказывает, а
        // не кричит. Первая версия светила ярче самого монстра и читалась
        // как лужа краски.
        private static readonly Color Hostile = new Color(1f, 0.25f, 0.18f, 0.9f);
        private static readonly Color Friendly = new Color(0.4f, 1f, 0.45f, 0.85f);

        private void Awake()
        {
            selector = GetComponent<TargetSelector>();

            if (selector == null)
            {
                enabled = false;
                return;
            }

            Build();
        }

        private void LateUpdate()
        {
            if (selector == null || ring == null) return;

            var target = selector.Current;

            if (target == null || !target.IsAlive)
            {
                if (ringRenderer.enabled) ringRenderer.enabled = false;
                shown = null;
                return;
            }

            if (target != shown)
            {
                shown = target;

                var self = GetComponent<Targetable>();
                bool hostile = self == null || target.IsHostileTo(self.Faction);

                var colour = hostile ? Hostile : Friendly;
                var material = ringRenderer.sharedMaterial;

                // Цвет — тоже по имени свойства Universal.
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
                if (material.HasProperty("_Color")) material.SetColor("_Color", colour);

                // Размер по самой цели: кольцо вокруг крысы и вокруг тролля
                // одного диаметра выглядит как ошибка.
                float width = 1f;

                var renderers = target.GetComponentsInChildren<Renderer>();

                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                    width = Mathf.Max(bounds.size.x, bounds.size.z);
                }

                ring.localScale = Vector3.one * Mathf.Max(width * Scale, 0.6f);
            }

            if (!ringRenderer.enabled) ringRenderer.enabled = true;

            // Кольцо кладём на землю цели, а не на её середину: у летающих и
            // высоких существ середина далеко от ног.
            Vector3 at = target.transform.position;
            ring.position = new Vector3(at.x, at.y + Lift, at.z);
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "TargetRing";

            Object.Destroy(go.GetComponent<Collider>());

            ring = go.transform;
            ring.rotation = Quaternion.Euler(90f, 0f, 0f);

            ringRenderer = go.GetComponent<MeshRenderer>();
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;
            ringRenderer.enabled = false;

            // Берём шейдер, у которого прозрачность из коробки.
            //
            // С Universal/Unlit кольцо дважды вышло сплошным квадратом:
            // текстуру он берёт из _BaseMap, но прозрачность включается
            // набором из пяти свойств и ключевого слова, и хоть одно
            // несовпадение — альфа игнорируется молча. Particles/Unlit и
            // Sprites/Default прозрачны по своей природе: там нечему не
            // сработать.
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

            var material = new Material(shader);

            var texture = RingTexture();

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            material.mainTexture = texture;

            // Смешивание задаём в любом случае: у частиц оно уже такое, у
            // запасных шейдеров — не факт.
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3100;

            ringRenderer.sharedMaterial = material;
        }

        /// <summary>
        /// Кольцо с мягкими краями, нарисованное кодом.
        ///
        /// Мягкость обязательна: жёсткая граница круга рисуется лесенкой и
        /// заметна даже на земле под ногами.
        /// </summary>
        private static Texture2D RingTexture()
        {
            const int Size = 128;
            // Плотный обод плюс лёгкая заливка — как в WoW.
            //
            // Чистое кольцо читалось как обод, нарисованный на земле;
            // сплошная заливка — как лужа краски. У Blizzard сделано
            // третьим способом: яркий край очерчивает, кто выбран, а слабая
            // середина привязывает пятно к существу, не заслоняя землю.
            const float Outer = 0.48f;
            const float Edge = 0.34f;
            const float Soft = 0.05f;

            const float FillAlpha = 0.22f;
            const float EdgeAlpha = 1f;

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f) / Size - 0.5f;
                    float dy = (y + 0.5f) / Size - 0.5f;

                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    // Внутри слабая заливка, у кромки плотный обод,
                    // снаружи ничего.
                    float inside = Mathf.SmoothStep(0f, 1f, (Outer - r) / Soft);
                    float rim = Mathf.SmoothStep(0f, 1f, (r - Edge) / Soft) * inside;

                    float alpha = Mathf.Max(inside * FillAlpha, rim * EdgeAlpha);

                    pixels[y * Size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }
    }
}
