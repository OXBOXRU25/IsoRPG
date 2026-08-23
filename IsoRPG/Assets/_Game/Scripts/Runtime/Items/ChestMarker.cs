using UnityEngine;

namespace IsoRPG.Items
{
    /// <summary>
    /// Вопросительный знак над запертым сундуком, когда ключ уже в сумке.
    ///
    /// Ключ, лежащий в рюкзаке без единой подсказки, — это загадка, которую
    /// игра не задавала. Сундук в углу тёмной комнаты ничем не отличается от
    /// декорации: по нему не пробуют кликать, потому что по бочкам и ящикам
    /// рядом кликать бессмысленно.
    ///
    /// Знак тот же, что у торговца при сдаче квеста, и это намеренно: игрок
    /// уже выучил, что жёлтый вопрос означает «подойди и нажми». Второй символ
    /// для того же смысла пришлось бы учить заново.
    /// </summary>
    [RequireComponent(typeof(TreasureChest))]
    public sealed class ChestMarker : MonoBehaviour
    {
        private static readonly Color MarkColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);

        [Tooltip("Ключ, при котором знак появляется.")]
        [SerializeField] private ItemDefinition key;

        [Tooltip("На какой высоте висит знак.")]
        [SerializeField] private float height = 1.6f;

        [Tooltip("Материал знака. Ассетом, иначе шейдер может не попасть в сборку.")]
        [SerializeField] private Material markerMaterial;

        public void SetupMaterial(Material material) => markerMaterial = material;

        /// <summary>Как высоко подпрыгивает. Немного: это метка, а не фейерверк.</summary>
        private const float BobAmplitude = 0.16f;

        private TreasureChest chest;
        private Inventory inventory;
        private GameObject marker;
        private Transform body;

        public void Setup(ItemDefinition requiredKey) => key = requiredKey;

        private void Awake()
        {
            chest = GetComponent<TreasureChest>();
            Build();
        }

        private void Update()
        {
            // Сумку ищем лениво: игрок появляется в сцене тогда же, когда и
            // сундук, и в Awake его может ещё не быть.
            if (inventory == null)
            {
                inventory = FindFirstObjectByType<Inventory>();
                if (inventory == null) return;
            }

            bool show = chest != null && !chest.IsOpen
                        && key != null && inventory.CountOf(key) > 0;

            if (marker.activeSelf != show) marker.SetActive(show);
            if (!show) return;

            // Подскок по параболе и медленный поворот — ровно как у знака
            // торговца, чтобы читалось как одно и то же приглашение.
            float jump = Mathf.PingPong(Time.time * 1.6f, 1f);
            body.localPosition = new Vector3(0f, jump * jump * BobAmplitude, 0f);
            body.Rotate(Vector3.up, 55f * Time.deltaTime, Space.Self);
        }

        private void Build()
        {
            marker = new GameObject("ChestMarker");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = Vector3.up * height;

            body = new GameObject("Body").transform;
            body.SetParent(marker.transform, false);

            // Крючок вопроса собран из двух кубиков и шарика: три примитива
            // дешевле любой модели и читаются с высоты камеры не хуже.
            MakePart(new Vector3(0f, 0.34f, 0f), new Vector3(0.26f, 0.1f, 0.1f));
            MakePart(new Vector3(0.12f, 0.22f, 0f), new Vector3(0.1f, 0.26f, 0.1f));
            MakePart(new Vector3(0f, 0.08f, 0f), new Vector3(0.1f, 0.16f, 0.1f));
            MakeDot(new Vector3(0f, -0.12f, 0f));

            marker.SetActive(false);
        }

        private void MakePart(Vector3 at, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(body, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;

            Paint(go);
        }

        private void MakeDot(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(body, false);
            go.transform.localPosition = at;
            go.transform.localScale = Vector3.one * 0.13f;

            Paint(go);
        }

        private void Paint(GameObject go)
        {
            // Коллайдер снимаем: знак висит ровно там, куда игрок целится
            // мышью, и ловить клики вместо самого сундука ему нельзя.
            Destroy(go.GetComponent<Collider>());

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Тот же случай, что и у знака квеста: копия материала примитива
            // тянет за собой шейдер, которого может не оказаться в сборке.
            var material = markerMaterial != null
                ? new Material(markerMaterial)
                : new Material(renderer.sharedMaterial);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", MarkColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", MarkColor);

            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", MarkColor * 1.6f);

            renderer.material = material;
        }
    }
}
