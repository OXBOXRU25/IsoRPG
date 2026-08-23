using System;
using System.Collections.Generic;
using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.Items
{
    /// <summary>
    /// Добыча на трупе. Генерируется в момент смерти и ждёт, пока подберут.
    ///
    /// Генерация именно при смерти, а не при подборе: иначе содержимое трупа
    /// менялось бы при каждом клике, и «не то выпало» превращалось бы в
    /// перезагрузку клика. Что выпало — то выпало.
    /// </summary>
    public sealed class LootSource : MonoBehaviour
    {
        [Tooltip("Что может выпасть. Пусто — монстр не даёт добычи.")]
        [SerializeField] private LootTable table;

        [Tooltip("Модель мешка с добычей.")]
        [SerializeField] private GameObject itemModel;

        [Tooltip("Материал силуэта, чтобы мешок было видно за препятствиями.")]
        [SerializeField] private Material silhouetteMaterial;

        [Tooltip("Насколько широко разбрасывается добыча вокруг тела.")]
        [SerializeField] private float spillRadius = 0.7f;

        [Tooltip("Высота значка мешка над трупом.")]
        [SerializeField] private float markerHeight = 1.6f;

        private readonly List<ItemStack> contents = new List<ItemStack>();
        private int gold;
        private bool generated;
        private GameObject marker;

        public bool HasLoot => generated && (gold > 0 || contents.Count > 0);
        public int Gold => gold;
        public IReadOnlyList<ItemStack> Contents => contents;

        /// <summary>Добыча появилась на трупе — интерфейсу пора показать значок.</summary>
        public event Action LootReady;

        /// <summary>Всё забрали.</summary>
        public event Action LootTaken;

        public void Setup(LootTable newTable) => table = newTable;

        public void SetupModels(GameObject bag, Material silhouette)
        {
            itemModel = bag;
            silhouetteMaterial = silhouette;
        }

        /// <summary>
        /// Сбросить добычу перед возрождением: монстр встаёт заново, и в
        /// следующий раз с него должно упасть что-то своё, а не остатки
        /// прошлой смерти.
        /// </summary>
        public void ResetLoot()
        {
            contents.Clear();
            gold = 0;
            generated = false;

            if (marker != null)
            {
                Destroy(marker);
                marker = null;
            }
        }

        private void Awake()
        {
            var health = GetComponent<Health>();
            if (health != null) health.Died += OnDied;
        }

        private void OnDestroy()
        {
            var health = GetComponent<Health>();
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied(GameObject killer)
        {
            if (generated) return;

            // Таблицы нет — сказать вслух. Молча оставлять монстра без добычи
            // нельзя: снаружи это неотличимо от невезения с шансами.
            if (table == null)
            {
                Debug.LogWarning($"[IsoRPG] У «{name}» нет таблицы добычи — падать нечему.");
                return;
            }

            generated = true;
            gold = table.RollGold();
            contents.AddRange(table.RollItems());

            Debug.Log($"[IsoRPG] Добыча с «{name}»: {gold} золота, предметов {contents.Count}");

            // И в игровой лог тоже: консоль Unity игрок не видит, а знать
            // о выпавшем он должен сразу.
            if (gold > 0 || contents.Count > 0)
                CombatLog.Add($"С «{name}» выпало: {gold} золота, предметов {contents.Count}",
                              LogKind.Loot);

            if (HasLoot)
            {
                SpillOnGround();
                LootReady?.Invoke();
            }
            else
            {
                Debug.Log($"[IsoRPG] С «{name}» ничего не выпало — не повезло с шансами.");
            }
        }

        /// <summary>
        /// Роняет добычу на землю кучками вокруг тела.
        ///
        /// Вразброс, а не в одну точку: сложенные друг в друга модели
        /// читаются как один предмет, и игрок уходит, забрав половину.
        /// Радиус небольшой — добыча должна лежать у тела, а не разлетаться
        /// по локации.
        /// </summary>
        private void SpillOnGround()
        {
            Vector2 flat = UnityEngine.Random.insideUnitCircle * spillRadius;
            Vector3 at = transform.position + new Vector3(flat.x, 0f, flat.y);

            GameObject go;

            if (itemModel != null)
            {
                go = Instantiate(itemModel, at,
                                 Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            }
            else
            {
                // Модели нет — ставим приметный кубик. Без него добыча выпала
                // бы невидимой, и это неотличимо от её отсутствия.
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = at;
                go.transform.localScale = Vector3.one * 0.35f;
            }

            go.name = "Loot_" + name;

            // Коллайдер нужен: по мешку кликают. Делаем его триггером, чтобы
            // он не мешал навигации и не толкал персонажей.
            foreach (var old in go.GetComponentsInChildren<Collider>())
                Destroy(old);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.up * 0.3f;
            box.size = new Vector3(0.9f, 0.9f, 0.9f);

            var drop = go.AddComponent<LootDrop>();
            drop.Fill(gold, contents);

            // Силуэт: мешок легко падает за стену или в траву, и без силуэта
            // добыча просто теряется. Компонент тот же, что у персонажей.
            var silhouette = go.AddComponent<IsoRPG.Combat.SilhouetteVisual>();
            if (silhouetteMaterial != null) silhouette.Setup(silhouetteMaterial);

            // Мешок лежит на земле: мерить его перекрытие на высоте человека
            // бессмысленно — там воздух.
            silhouette.SetHeight(0.55f);

            gold = 0;
            contents.Clear();
        }

        /// <summary>
        /// Забрать всё в сумку. Возвращает, что удалось взять.
        ///
        /// Не поместившееся остаётся на трупе — терять добычу из-за полной
        /// сумки нельзя, это самый обидный вид потери в играх такого рода.
        /// </summary>
        public bool TakeAll(Inventory inventory, out int takenGold, out List<ItemStack> takenItems)
        {
            takenGold = 0;
            takenItems = new List<ItemStack>();

            if (!HasLoot || inventory == null) return false;

            if (gold > 0)
            {
                inventory.AddGold(gold);
                takenGold = gold;
                gold = 0;
            }

            for (int i = contents.Count - 1; i >= 0; i--)
            {
                var stack = contents[i];
                int leftover = inventory.Add(stack);

                if (leftover <= 0)
                {
                    takenItems.Add(stack);
                    contents.RemoveAt(i);
                }
                else if (leftover < stack.Count)
                {
                    takenItems.Add(new ItemStack(stack.Item, stack.Count - leftover));
                    contents[i] = new ItemStack(stack.Item, leftover);
                }
            }

            if (!HasLoot)
            {
                HideMarker();
                LootTaken?.Invoke();
            }

            return takenGold > 0 || takenItems.Count > 0;
        }

        /// <summary>
        /// Включить коллайдеры обратно — по трупу с добычей надо кликать.
        ///
        /// Выполняется в конце кадра: обработчик смерти может отключить их
        /// после нас, и тогда порядок вызовов решал бы, работает ли подбор.
        /// Так результат не зависит от того, кто первым подписался.
        /// </summary>
        private void RestoreClickability()
        {
            foreach (var collider in GetComponentsInChildren<Collider>())
                collider.enabled = true;

            StartCoroutine(RestoreNextFrame());
        }

        private System.Collections.IEnumerator RestoreNextFrame()
        {
            yield return null;

            if (!HasLoot) yield break;

            foreach (var collider in GetComponentsInChildren<Collider>())
                collider.enabled = true;
        }

        /// <summary>
        /// Значок мешка над трупом — иначе игрок не отличит труп с добычей
        /// от пустого и будет кликать по всем подряд.
        /// </summary>
        private void ShowMarker()
        {
            if (marker != null) return;

            marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "LootMarker";
            Destroy(marker.GetComponent<Collider>());

            marker.transform.SetParent(transform);
            marker.transform.localPosition = Vector3.up * markerHeight;
            marker.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);
            marker.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

            var renderer = marker.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(renderer.sharedMaterial)
            {
                color = new Color32(0xC8, 0xA0, 0x40, 0xFF)
            };
        }

        private void HideMarker()
        {
            if (marker != null) marker.SetActive(false);
        }
    }
}
