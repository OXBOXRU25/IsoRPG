using UnityEngine;

namespace IsoRPG.Items
{
    /// <summary>
    /// Выбрасывает вещи из сумки на землю.
    ///
    /// Кладём выброшенное в такой же мешок, какой остаётся после монстра, а не
    /// в отдельный «выброшенный предмет». Причина простая: игрок уже знает, что
    /// мешок на земле — это то, что можно подобрать. Второй вид лежащей вещи
    /// пришлось бы объяснять заново, а поведение у него было бы то же самое.
    ///
    /// Мешок появляется под ногами, а не там, где отпустили мышь. Место в
    /// интерфейсе и место в мире — разные вещи, и попытка их связать даёт
    /// сюрпризы: предмет улетал бы сквозь стену, если окно оказалось поверх
    /// неё.
    /// </summary>
    public sealed class ItemDropper : MonoBehaviour
    {
        [Tooltip("Модель мешка. Та же, что у добычи с монстров.")]
        [SerializeField] private GameObject bagModel;

        [Tooltip("Материал силуэта — чтобы мешок было видно за препятствием.")]
        [SerializeField] private Material silhouetteMaterial;

        [Tooltip("На сколько метров от ног отлетает выброшенное.")]
        [SerializeField] private float throwDistance = 1.1f;

        private Inventory inventory;

        public void Setup(GameObject model, Material silhouette)
        {
            bagModel = model;
            silhouetteMaterial = silhouette;
        }

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
        }

        /// <summary>
        /// Выбрасывает содержимое ячейки. Возвращает, получилось ли.
        /// </summary>
        public bool DropSlot(int index)
        {
            if (inventory == null) return false;

            var stack = inventory.GetSlot(index);
            if (stack.IsEmpty || stack.Item == null) return false;

            // Забираем из сумки до создания мешка: если создание сорвётся,
            // лучше оставить вещь на месте, чем удвоить её.
            var dropped = inventory.TakeFrom(index);
            if (dropped.IsEmpty || dropped.Item == null) return false;

            Spawn(dropped);

            // Звук подбора наоборот не нужен — тот же глухой стук годится
            // и когда вещь кладут на землю.
            IsoRPG.Audio.Sfx.Pickup(transform.position);

            return true;
        }

        private void Spawn(ItemStack stack)
        {
            // Бросаем перед собой, а не строго под ноги: мешок под персонажем
            // прячется за его же моделью, и кажется, что вещь пропала.
            Vector3 at = transform.position + transform.forward * throwDistance;

            // Небольшой разброс, чтобы выброшенные подряд вещи не слипались
            // в одну точку и их можно было различить и подобрать по отдельности.
            Vector2 scatter = Random.insideUnitCircle * 0.35f;
            at += new Vector3(scatter.x, 0f, scatter.y);

            GameObject go;

            if (bagModel != null)
            {
                go = Instantiate(bagModel, at,
                                 Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = at;
                go.transform.localScale = Vector3.one * 0.35f;
            }

            go.name = "Dropped_" + stack.Item.displayName;

            // Коллайдер триггером: по мешку кликают, но он не должен толкать
            // персонажей и перегораживать дорогу.
            foreach (var old in go.GetComponentsInChildren<Collider>())
                Destroy(old);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.up * 0.3f;
            box.size = new Vector3(0.9f, 0.9f, 0.9f);

            var drop = go.AddComponent<LootDrop>();
            drop.Fill(0, new[] { stack });

            var silhouette = go.AddComponent<IsoRPG.Combat.SilhouetteVisual>();
            if (silhouetteMaterial != null) silhouette.Setup(silhouetteMaterial);
            silhouette.SetHeight(0.55f);
        }
    }
}
