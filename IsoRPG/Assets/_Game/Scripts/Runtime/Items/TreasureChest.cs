using UnityEngine;
using IsoRPG.Localization;
using IsoRPG.Combat;

namespace IsoRPG.Items
{
    /// <summary>
    /// Запертый сундук: открывается ключом, отдаёт содержимое один раз.
    ///
    /// Награда за босса лежит здесь, а не выпадает из него. Разница в том, что
    /// игрок делает: из скелета кольцо просто появляется строкой в списке
    /// добычи, а тут он находит ключ, видит сундук в углу комнаты и открывает
    /// его сам. Тот же предмет, но событие вместо записи.
    ///
    /// Крышка поворачивается по-настоящему — в модели KayKit она отдельным
    /// узлом. Подмена модели на «открытую» выглядела бы рывком, а поворот на
    /// петле читается как то, что игрок сделал руками.
    /// </summary>
    public sealed class TreasureChest : MonoBehaviour
    {
        private static readonly Color KeyColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);

        /// <summary>Имя узла крышки в модели набора.</summary>
        private const string LidNode = "chest_lid";

        [Tooltip("Ключ, который его отпирает. Пусто — открыт для всех.")]
        [SerializeField] private ItemDefinition key;

        [Tooltip("Тратится ли ключ. Обычно да: он от этого замка и больше ни от чего.")]
        [SerializeField] private bool consumeKey = true;

        [Tooltip("На сколько градусов откидывается крышка.")]
        [SerializeField] private float lidAngle = 104f;

        [Tooltip("Насколько близко надо подойти.")]
        [SerializeField] private float reach = 2.6f;

        private Transform lid;
        private Quaternion lidClosed;
        private bool open;

        public bool IsOpen => open;
        public float Reach => reach;

        public void Setup(ItemDefinition requiredKey) => key = requiredKey;

        /// <summary>Чем сундук помечен в сохранении. Имя объекта уникально.</summary>
        private string Key => name;

        private void Start()
        {
            // Открытый сундук должен остаться открытым и пустым: иначе
            // достаточно перезайти в игру, чтобы получить вторую награду.
            if (IsoRPG.Save.SaveService.Instance != null &&
                IsoRPG.Save.SaveService.Instance.IsChestOpened(Key))
            {
                open = true;
                if (lid != null) lid.localRotation = lidClosed * Quaternion.Euler(-lidAngle, 0f, 0f);
            }
        }

        private void Awake()
        {
            lid = FindLid(transform);
            if (lid != null) lidClosed = lid.localRotation;
        }

        /// <summary>
        /// Попытка открыть. Возвращает false, если не вышло, — вызывающий
        /// при этом ничего не делает: сундук сам объяснил игроку причину.
        /// </summary>
        public bool TryOpen(GameObject who)
        {
            if (open) return false;

            var inventory = who != null ? who.GetComponent<Inventory>() : null;

            if (key != null)
            {
                if (inventory == null || inventory.CountOf(key) <= 0)
                {
                    // Молчащий замок читается как сломанный сундук. Говорим,
                    // ЧТО именно нужно, а не просто «заперто».
                    CombatLog.Add(Loc.F("Заперто. Нужен предмет: {0}", Loc.T(key.displayName)), LogKind.System);
                    return false;
                }

                if (consumeKey) inventory.Remove(key, 1);
            }

            Open();
            return true;
        }

        private void Open()
        {
            open = true;

            if (lid != null) lid.localRotation = lidClosed * Quaternion.Euler(-lidAngle, 0f, 0f);

            if (IsoRPG.Save.SaveService.Instance != null)
                IsoRPG.Save.SaveService.Instance.MarkChestOpened(Key);

            IsoRPG.Audio.Sfx.Play(IsoRPG.Audio.Sfx.Bank?.equip, transform.position, 0.6f, 0.06f);
            CombatLog.Add("Сундук открыт.", LogKind.Loot);

            // Содержимое отдаём через обычный источник добычи: то же окно, те
            // же правила, тот же мешок. Отдельная «панель сундука» была бы
            // вторым интерфейсом для того же действия.
            var loot = GetComponent<LootSource>();
            if (loot != null) loot.ForceDrop();
        }

        private static Transform FindLid(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == LidNode) return t;

            return null;
        }
    }
}
