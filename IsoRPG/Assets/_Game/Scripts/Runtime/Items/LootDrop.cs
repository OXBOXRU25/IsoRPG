using System.Collections.Generic;
using UnityEngine;

namespace IsoRPG.Items
{
    /// <summary>
    /// Мешок с добычей, лежащий на земле.
    ///
    /// Раньше добыча забиралась автоматически, стоило подойти. Работало, но
    /// убивало половину смысла: игрок не видел, ЧТО выпало, и ничего не решал.
    /// Мешок на земле возвращает и то и другое — видно, что упало, и можно
    /// взять не всё.
    ///
    /// Один мешок на существо, а не кучка на предмет: пять моделей, лежащих
    /// рядом, читаются как одна, и половину добычи игрок просто не заметит.
    /// </summary>
    public sealed class LootDrop : MonoBehaviour
    {
        [SerializeField] private int gold;
        [SerializeField] private List<ItemStack> contents = new List<ItemStack>();

        [Tooltip("Насколько высоко покачивается, чтобы притянуть взгляд.")]
        [SerializeField] private float bobHeight = 0.1f;

        [SerializeField] private float bobSpeed = 2f;

        private Vector3 restPosition;
        private float phase;

        public int Gold => gold;
        public IReadOnlyList<ItemStack> Contents => contents;
        public bool IsEmpty => gold <= 0 && contents.Count == 0;

        /// <summary>Содержимое изменилось — окну пора перерисоваться.</summary>
        public event System.Action Changed;

        /// <summary>Мешок опустел и сейчас исчезнет.</summary>
        public event System.Action Emptied;

        public void Fill(int amount, IEnumerable<ItemStack> items)
        {
            gold = amount;

            contents.Clear();
            if (items != null) contents.AddRange(items);
        }

        private void Start()
        {
            restPosition = transform.position;

            // Своя фаза: одинаково качающиеся мешки выглядят механизмом.
            phase = Random.Range(0f, 10f);
        }

        private void Update()
        {
            // Покачивание — единственное движение в кадре после боя. Глаз
            // ищет движение, поэтому мешок находится сам, без подсветки.
            float bob = Mathf.Sin((Time.time + phase) * bobSpeed) * bobHeight;
            transform.position = restPosition + Vector3.up * (bobHeight + bob);
        }

        /// <summary>Забрать золото. Возвращает, сколько взяли.</summary>
        public int TakeGold(Inventory inventory)
        {
            if (gold <= 0 || inventory == null) return 0;

            int taken = gold;
            inventory.AddGold(taken);
            gold = 0;

            Changed?.Invoke();
            CheckEmpty();

            return taken;
        }

        /// <summary>
        /// Забрать один предмет по номеру в списке.
        ///
        /// Не поместившееся остаётся в мешке: терять добычу из-за полной
        /// сумки нельзя, это самый обидный вид потери в играх такого рода.
        /// </summary>
        public bool TakeItem(int index, Inventory inventory, out ItemStack taken)
        {
            taken = ItemStack.Empty;

            if (inventory == null) return false;
            if (index < 0 || index >= contents.Count) return false;

            var stack = contents[index];
            int leftover = inventory.Add(stack);

            if (leftover >= stack.Count) return false;   // не влезло совсем

            taken = new ItemStack(stack.Item, stack.Count - leftover);

            if (leftover <= 0) contents.RemoveAt(index);
            else contents[index] = new ItemStack(stack.Item, leftover);

            Changed?.Invoke();
            CheckEmpty();

            return true;
        }

        private void CheckEmpty()
        {
            if (!IsEmpty) return;

            Emptied?.Invoke();

            // Пустой мешок убираем сразу: лежащий пустым он выглядит как
            // добыча, за которой ещё стоит идти.
            Destroy(gameObject);
        }
    }
}
