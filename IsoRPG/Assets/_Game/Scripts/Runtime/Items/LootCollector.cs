using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.Items
{
    /// <summary>
    /// Подбирает добычу с ближайших трупов автоматически, когда игрок
    /// оказывается рядом.
    ///
    /// Клик по телу оказался ненадёжным: труп теряет коллайдер при смерти,
    /// и попасть по нему выходило не всегда. Автоматический подбор снимает
    /// вопрос целиком — и, честно говоря, в игре про быстрые бои это ещё и
    /// удобнее: не надо целиться в лежащее тело после каждой драки.
    ///
    /// Ручной обыск вернём, когда появится окно лута с выбором предметов.
    /// </summary>
    public sealed class LootCollector : MonoBehaviour
    {
        [Tooltip("На каком расстоянии подбираем добычу.")]
        [SerializeField] private float pickupRadius = 2.6f;

        [Tooltip("Как часто осматриваться, в секундах.")]
        [SerializeField] private float scanInterval = 0.25f;

        private static readonly Color GoldColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);

        private Inventory inventory;
        private float nextScanTime;

        private void Awake() => inventory = GetComponent<Inventory>();

        private void Update()
        {
            if (inventory == null || Time.time < nextScanTime) return;
            nextScanTime = Time.time + scanInterval;

            // Ищем трупы вокруг. Коллайдеры включаем в поиск все, включая
            // триггеры: у мёртвого тела коллайдер может быть уже отключён,
            // но сам объект на месте.
            var hits = Physics.OverlapSphere(transform.position, pickupRadius,
                                             ~0, QueryTriggerInteraction.Collide);

            foreach (var hit in hits)
            {
                var loot = hit.GetComponentInParent<LootSource>();
                if (loot == null || !loot.HasLoot) continue;

                Collect(loot);
            }
        }

        private void Collect(LootSource loot)
        {
            if (!loot.TakeAll(inventory, out int gold, out var items)) return;

            if (gold > 0)
            {
                LootPopup.Show(loot.transform.position, gold + " золота", GoldColor);
                CombatLog.GainedGold(gold);
            }

            foreach (var stack in items)
            {
                LootPopup.Show(loot.transform.position, stack.ToString(), stack.Item.RarityColor);
                CombatLog.Looted(stack.ToString(), stack.Item.RarityColor);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.8f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
#endif
    }
}
