using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.Items
{
    /// <summary>
    /// Живая модель персонажа в окне снаряжения.
    ///
    /// Не портрет и не картинка: это настоящая копия героя, которой отдана та
    /// же экипировка. Надел другой кинжал — он появился в руке модели, и
    /// проверять это не надо, потому что оружие вешает тот же компонент, что
    /// и на самом персонаже.
    ///
    /// Стоит копия не в кадре, а в «студии» далеко под картой, на своём слое.
    /// Так её видит только своя камера, а мир не видит вовсе — она не отбросит
    /// тень посреди поляны и не попадёт под удар.
    ///
    /// Своё освещение — по той же причине: витрина не должна темнеть с
    /// наступлением ночи.
    /// </summary>
    public sealed class CharacterPreview : MonoBehaviour
    {
        /// <summary>
        /// Слой студии. Занят только ею: солнце сцены его не освещает, боевые
        /// лучи по нему не стреляют, основная камера его не рисует.
        /// </summary>
        public const int PreviewLayer = 9;

        /// <summary>Далеко под картой: там ничего нет и не будет.</summary>
        private static readonly Vector3 StageOrigin = new Vector3(0f, -500f, 0f);

        private const int TextureWidth = 280;
        private const int TextureHeight = 420;

        [Tooltip("Модель героя. Ту же, что и в мире.")]
        [SerializeField] private GameObject modelPrefab;

        [Tooltip("Контроллер анимаций: без него модель встанет в позу T.")]
        [SerializeField] private RuntimeAnimatorController animatorController;

        private Camera stageCamera;
        private RenderTexture texture;

        /// <summary>
        /// Сама модель — её крутит игрок мышью.
        ///
        /// Крутим модель, а не камеру: свет расставлен вокруг сцены и должен
        /// оставаться на месте. Поверни камеру — и герой поедет из света в
        /// тень, что читается как поломка освещения, а не как поворот.
        /// </summary>
        private Transform modelPivot;

        /// <summary>Стартовый доворот — к нему же возвращаемся при открытии.</summary>
        private const float BaseYaw = 20f;

        private float yaw = BaseYaw;

        public RenderTexture Texture => texture;

        /// <summary>
        /// Довернуть героя вокруг вертикальной оси. Градусы, знак — сторона.
        /// </summary>
        public void Spin(float degrees)
        {
            if (modelPivot == null) return;

            yaw += degrees;
            modelPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void Setup(GameObject model, RuntimeAnimatorController controller)
        {
            modelPrefab = model;
            animatorController = controller;
        }

        /// <summary>
        /// Рисуем, только пока окно открыто. Камера, работающая в фоне ради
        /// закрытой панели, — самая обидная трата кадра, какая бывает.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (stageCamera != null) stageCamera.enabled = visible;
        }

        /// <summary>
        /// Текстуру заводим в Awake, а студию — в Start.
        ///
        /// Окно снаряжения строится в своём Awake и берёт ссылку на текстуру
        /// сразу. Порядок Awake между компонентами Unity не гарантирует,
        /// поэтому текстура обязана существовать раньше всего остального —
        /// иначе окно однажды соберётся с пустой картинкой, и виноват будет
        /// порядок, которого никто не выбирал.
        /// </summary>
        private void Awake()
        {
            texture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
            };
        }

        private void Start()
        {
            // Модель берём у живого героя, а не из своего поля.
            //
            // 01.09.2026 Павлон увидел в окне персонажа СТАРУЮ модель — ту,
            // что стояла на прежней арене: поле заполнил её сборщик, а на
            // новой арене героя сменили, и витрина об этом не узнала. Это
            // тот же класс ошибки, что с лошадью-стеной: список собран в двух
            // местах, и второе место повторяло первое по памяти. Пока витрина
            // смотрит на самого героя, разойтись они не могут в принципе.
            var live = LiveHeroModel();
            if (live != null) modelPrefab = live;

            if (modelPrefab == null) return;

            Build();
            SetVisible(false);
        }

        /// <summary>
        /// Модель героя из сцены: узел со скелетной сеткой под игроком.
        ///
        /// Ищем скелетную сетку, а не имя и не префаб: у любого героя она
        /// есть, как бы его ни звали и из какого набора он ни был собран.
        /// </summary>
        private GameObject LiveHeroModel()
        {
            var owner = GetComponentInParent<IsoRPG.Player.PlayerInputRouter>();
            if (owner == null) return null;

            var skin = owner.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null) return null;

            // Поднимаемся до узла, на котором висит аниматор: это и есть
            // корень модели, а сама сетка лежит у него ребёнком.
            var node = skin.transform;
            while (node.parent != null && node.parent != owner.transform && node.GetComponent<Animator>() == null)
                node = node.parent;

            return node.gameObject;
        }

        private void OnDestroy()
        {
            if (texture == null) return;

            texture.Release();
            Destroy(texture);
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            var stage = new GameObject("CharacterPreviewStage");
            stage.transform.position = StageOrigin;

            var model = Instantiate(modelPrefab, stage.transform);
            model.transform.localPosition = Vector3.zero;

            // Лицом к камере: в окне снаряжения смотрят на грудь и руки, а не
            // на затылок. Камера глядит вдоль минус-Z, значит герою нужен
            // нулевой угол; двадцать градусов доворота убирают эффект
            // паспортного фото, не пряча ни одной руки.
            model.transform.localRotation = Quaternion.Euler(0f, BaseYaw, 0f);

            // Запоминаем, чтобы игрок мог довернуть героя мышью.
            modelPivot = model.transform;
            yaw = BaseYaw;

            SetLayer(model, PreviewLayer);

            var animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                if (animatorController != null)
                    animator.runtimeAnimatorController = animatorController;

                // Обязательно и всегда, а не только когда подали контроллер.
                //
                // Unity решает, анимировать ли модель, по тому, видит ли её
                // хоть одна ОБЫЧНАЯ камера. Нашу студию видит только своя,
                // поэтому по умолчанию герой замирает в первом кадре покоя:
                // картинка есть, дыхания нет, и выглядит это как фотография
                // вместо живой модели.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            // Оружие в руках копии ведёт тот же компонент, что и у героя, и
            // читает ТУ ЖЕ экипировку. Никакой синхронизации писать не нужно:
            // её просто нет — источник один.
            var visual = model.AddComponent<WeaponVisual>();
            visual.Setup(GetComponent<Equipment>(), PreviewLayer);

            var bounds = Measure(model);

            BuildCamera(stage.transform, bounds);
            BuildLights(stage.transform, bounds);
        }

        private void BuildCamera(Transform stage, Bounds bounds)
        {
            var go = new GameObject("PreviewCamera", typeof(Camera));
            go.transform.SetParent(stage, false);

            stageCamera = go.GetComponent<Camera>();
            stageCamera.orthographic = true;
            stageCamera.cullingMask = 1 << PreviewLayer;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            stageCamera.targetTexture = texture;

            // Не участвует в звуке и не спорит с главной камерой за порядок.
            stageCamera.depth = -10;

            float height = Mathf.Max(0.5f, bounds.size.y);

            // Поле зрения чуть шире роста: при точной подгонке макушка
            // срезается краем, потому что габариты считаются по мешу, а
            // капюшон и оружие торчат за него.
            // Поле зрения — 1.1 роста: при 0.9 герой физически не влезал,
            // и ноги срезало краем кадра (Павлон 01.09.2026: «ноги
            // обрезаны»). Запас в десятую долю оставляем на капюшон и
            // оружие: габариты считаются по мешу, а они торчат за него.
            stageCamera.orthographicSize = height * 0.63f;

            // Смотрим выше середины — тогда фигура садится ниже в кадре и
            // под ногами остаётся опора, а не обрез.
            var focus = new Vector3(bounds.center.x, bounds.min.y + height * 0.46f, bounds.center.z);

            // Взгляд слегка сверху: строго сбоку фигура выглядит плоской.
            go.transform.rotation = Quaternion.Euler(6f, 180f, 0f);
            go.transform.position = focus - go.transform.forward * 12f;
        }

        /// <summary>
        /// Свет витрины — точечные источники, а не направленные.
        ///
        /// Направленный свет освещает всю сцену независимо от того, где он
        /// стоит, а маска слоёв у него в URP работает непредсказуемо: два
        /// таких источника в студии перекрасили закат на всей карте. Точечный
        /// же ограничен радиусом и до мира не достаёт физически — за пятьсот
        /// метров ему просто нечего осветить.
        /// </summary>
        private void BuildLights(Transform stage, Bounds bounds)
        {
            float height = Mathf.Max(0.5f, bounds.size.y);
            var center = new Vector3(bounds.center.x, bounds.min.y + height * 0.55f, bounds.center.z);

            MakeLamp(stage, "Key", center + new Vector3(-1.6f, 1.4f, -2.2f),
                     new Color(1f, 0.95f, 0.86f), 4.2f, 9f);

            // Контровой сзади: без него тёмный капюшон сливается с прозрачным
            // фоном, и вместо героя в окне видно дыру.
            MakeLamp(stage, "Rim", center + new Vector3(1.9f, 1.2f, 2.4f),
                     new Color(0.62f, 0.72f, 1f), 3.4f, 9f);

            // Заполняющий снизу-спереди: убирает провал под капюшоном.
            MakeLamp(stage, "Fill", center + new Vector3(1.2f, -0.4f, -2f),
                     new Color(0.9f, 0.9f, 0.95f), 1.8f, 8f);
        }

        private static void MakeLamp(Transform stage, string name, Vector3 position,
                                     Color color, float intensity, float range)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(stage, false);
            go.transform.position = position;

            var light = go.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;

            // Тени витрине не нужны: фигура одна, ронять тень не на что, а
            // три теневых источника стоят кадра на ровном месте.
            light.shadows = LightShadows.None;
        }

        private static Bounds Measure(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            return bounds;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }
    }
}
